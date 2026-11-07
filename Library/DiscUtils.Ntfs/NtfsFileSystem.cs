//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using DiscUtils.Core.WindowsSecurity.AccessControl;
using DiscUtils.Internal;
using DiscUtils.Streams;
using System.Linq;
using DiscUtils.Streams.Compatibility;
using System.Buffers;
using LTRData.Extensions.Buffers;

using DirectoryIndexEntry =
    System.Collections.Generic.KeyValuePair<DiscUtils.Ntfs.FileNameRecord, DiscUtils.Ntfs.FileRecordReference>;
using System.Collections.Concurrent;
using DiscUtils.Ntfs.Internals;
using LTRData.Extensions.Formatting;

namespace DiscUtils.Ntfs;

/// <summary>
/// Class for accessing NTFS file systems.
/// </summary>
public class NtfsFileSystem : DiscFileSystem, IClusterBasedFileSystem,
    IFileSystemWithClusterMap, IWindowsFileSystem, IDiagnosticTraceable
{
    private const FileAttributes NonSettableFileAttributes =
        FileAttributes.Directory | FileAttributes.Offline | FileAttributes.ReparsePoint;

    private readonly NtfsContext _context;

    // Top-level file system structures

    // Working state
    private readonly ObjectCache<long, File> _fileCache;

    public VolumeInformation VolumeInfo { get; }

    public override Stream RawStream => _context.RawStream;

    /// <summary>
    /// Initializes a new instance of the NtfsFileSystem class.
    /// </summary>
    /// <param name="stream">The stream containing the NTFS file system.</param>
    /// <remarks>
    /// A writable stream requires a clean journal. On a read-only stream, journal replay is applied in memory
    /// when supported. Use the overload with <see cref="NtfsJournalMode.Replay"/> to authorize writes at open.
    /// When opening through <see cref="FileSystemManager"/>, set the <see cref="FileSystemParameters"/> entry
    /// <c>NtfsJournalMode</c> to <c>Replay</c>, <c>Refuse</c> or <c>Ignore</c> to override the default for that open.
    /// </remarks>
    public NtfsFileSystem(Stream stream)
        : this(stream, stream?.CanWrite == true ? NtfsJournalMode.Refuse : NtfsJournalMode.Replay)
    {
    }

    /// <summary>
    /// Initializes a new instance of the NtfsFileSystem class.
    /// </summary>
    /// <param name="stream">The stream containing the NTFS file system.</param>
    /// <param name="journalMode">What to do with the journal of a volume Windows did not shut down cleanly.</param>
    /// <exception cref="IOException">The journal needs a replay that <paramref name="journalMode"/> does not allow,
    /// or that DiscUtils cannot do on a writable stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="journalMode"/> is not a defined mode.</exception>
    /// <seealso cref="NtfsJournal.Replay(Stream)"/>
    public NtfsFileSystem(Stream stream, NtfsJournalMode journalMode)
        : base(new NtfsOptions())
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        stream = HandleJournal(stream, journalMode, out var journal);
        JournalInfo = journal;

        _context = new NtfsContext
        {
            RawStream = stream,
            Options = NtfsOptions,

            GetFileByIndex = GetFile,
            GetFileByRef = GetFile,
            GetDirectoryByRef = GetDirectory,
            GetDirectoryByIndex = GetDirectory,
            AllocateFile = AllocateFile,
            ForgetFile = ForgetFile,
            ReadOnly = !stream.CanWrite
        };

        _fileCache = new ObjectCache<long, File>();

        stream.Position = 0;
        Span<byte> bytes = stackalloc byte[512];
        stream.ReadExactly(bytes);

        _context.BiosParameterBlock = BiosParameterBlock.FromBytes(bytes);
        if (!_context.BiosParameterBlock.IsValid(stream.Length))
        {
            throw new InvalidFileSystemException("BIOS Parameter Block is invalid for an NTFS file system");
        }

        if (NtfsOptions.ReadCacheEnabled)
        {
            var cacheSettings = new BlockCacheSettings
            {
                BlockSize = _context.BiosParameterBlock.BytesPerCluster
            };
            if (cacheSettings.ReadCacheSize < cacheSettings.BlockSize)
            {
                cacheSettings.ReadCacheSize = cacheSettings.BlockSize;
            }

            if (cacheSettings.LargeReadSize < cacheSettings.BlockSize)
            {
                cacheSettings.LargeReadSize = cacheSettings.BlockSize;
            }

            if (cacheSettings.OptimumReadSize < cacheSettings.BlockSize)
            {
                cacheSettings.OptimumReadSize = cacheSettings.BlockSize;
            }

            _context.RawStream = new BlockCacheStream(SparseStream.FromStream(stream, Ownership.None),
                Ownership.None, cacheSettings);
        }

        // Bootstrap the Master File Table
        _context.Mft = new MasterFileTable(_context);
        var mftFile = new File(_context, _context.Mft.GetBootstrapRecord());
        _fileCache[MasterFileTable.MftIndex] = mftFile;
        _context.Mft.Initialize(mftFile);

        // Get volume information (includes version number)
        var volumeInfoFile = GetFile(MasterFileTable.VolumeIndex);
        VolumeInfo =
            volumeInfoFile.GetStream(AttributeType.VolumeInformation, null)?.GetContent<VolumeInformation>();

        // Initialize access to the other well-known metadata files
        _context.ClusterBitmap = new ClusterBitmap(GetFile(MasterFileTable.BitmapIndex));
        _context.AttributeDefinitions = new AttributeDefinitions(GetFile(MasterFileTable.AttrDefIndex));
        _context.UpperCase = new UpperCase(GetFile(MasterFileTable.UpCaseIndex));

        if (VolumeInfo.Version >= VolumeInformation.VersionW2k)
        {
            _context.SecurityDescriptors = new SecurityDescriptors(GetFile(MasterFileTable.SecureIndex));

            if (GetDirectoryEntry(@"$Extend\$ObjId") is { } objIdFile)
            {
                _context.ObjectIds = new ObjectIds(GetFile(objIdFile.Reference));
            }

            if (GetDirectoryEntry(@"$Extend\$Reparse") is { } reparse)
            {
                _context.ReparsePoints = new ReparsePoints(GetFile(reparse.Reference));
            }

            if (GetDirectoryEntry(@"$Extend\$Quota") is { } quota)
            {
                _context.Quotas = new Quotas(GetFile(quota.Reference));
            }
        }

    }

    /// <summary>
    /// Gets what was found in the journal when the volume was opened, and whether it was replayed;
    /// <c>null</c> with <see cref="NtfsJournalMode.Ignore"/>.
    /// </summary>
    public NtfsJournalInfo JournalInfo { get; }

    private static Stream HandleJournal(Stream stream, NtfsJournalMode journalMode, out NtfsJournalInfo info)
    {
        info = null;
        switch (journalMode)
        {
            case NtfsJournalMode.Ignore:
                return stream;

            case NtfsJournalMode.Refuse:
                info = NtfsJournal.Analyze(stream);
                if (info.Status == NtfsJournalStatus.Unsupported)
                {
                    throw new IOException($"The NTFS journal cannot be replayed: {info.Detail}");
                }

                if (info.Status == NtfsJournalStatus.NeedsReplay || (stream.CanWrite && info.Status == NtfsJournalStatus.NothingToReplay))
                {
                    throw new IOException($"The NTFS journal must be replayed or reset before this volume can be opened ({info.Detail})");
                }

                return stream;

            case NtfsJournalMode.Replay:
                if (stream.CanWrite)
                {
                    // Like Windows at mount: replay onto the volume, then reset the journal.
                    info = NtfsJournal.Replay(stream);
                    return stream;
                }

                // Read-only: show the replayed metadata from memory, never write the volume.
                info = NtfsJournal.ReplayInMemory(stream, out var inMemory);
                return inMemory ?? stream;

            default:
                throw new ArgumentOutOfRangeException(nameof(journalMode), journalMode, "Unknown NTFS journal mode");
        }
    }

    private bool CreateShortNames => _context.Options.ShortNameCreation == ShortFileNameOption.Enabled
                   || (_context.Options.ShortNameCreation == ShortFileNameOption.UseVolumeFlag
                       && (VolumeInfo.Flags & VolumeInformationFlags.DisableShortNameCreation) == 0);

    /// <summary>
    /// Gets the friendly name for the file system.
    /// </summary>
    public override string FriendlyName => "Microsoft NTFS";

    /// <summary>
    /// Gets the options that control how the file system is interpreted.
    /// </summary>
    public NtfsOptions NtfsOptions => (NtfsOptions)Options;

    /// <summary>
    /// Gets the volume label.
    /// </summary>
    public override string VolumeLabel
    {
        get
        {
            var volumeFile = GetFile(MasterFileTable.VolumeIndex);
            var volNameStream = volumeFile.GetStream(AttributeType.VolumeName, null);
            return volNameStream?.GetContent<VolumeName>().Name;
        }
    }

    /// <summary>
    /// Indicates if the file system supports write operations.
    /// </summary>
    public override bool CanWrite
        // For now, we don't...
        => !_context.ReadOnly;

    /// <summary>
    /// Gets the size of each cluster (in bytes).
    /// </summary>
    public long ClusterSize => _context.BiosParameterBlock.BytesPerCluster;

    public int SectorSize => _context.BiosParameterBlock.BytesPerSector;

    /// <summary>
    /// Gets the total number of clusters managed by the file system.
    /// </summary>
    public long TotalClusters => MathUtilities.Ceil(_context.BiosParameterBlock.TotalSectors64,
                _context.BiosParameterBlock.SectorsPerCluster);

    public long TotalSectors => _context.BiosParameterBlock.TotalSectors64;

    public AttributeDefinitions AttributeDefinitions => _context.AttributeDefinitions;

    /// <summary>
    /// Copies an existing file to a new file, allowing overwriting of an existing file.
    /// </summary>
    /// <param name="sourceFile">The source file.</param>
    /// <param name="destinationFile">The destination file.</param>
    /// <param name="overwrite">Whether to permit over-writing of an existing file.</param>
    public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
    {
        using (NtfsTransaction.Begin())
        {
            var sourceParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(sourceFile));
            if (sourceParentDirEntry == null || !sourceParentDirEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("No such file", sourceFile);
            }

            var sourceParentDir = GetDirectory(sourceParentDirEntry.Value.Reference);

            var sourceEntry = sourceParentDir.GetEntryByName(Utilities.GetFileFromPath(sourceFile));
            if (sourceEntry == null || sourceEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("No such file", sourceFile);
            }

            var origFile = GetFile(sourceEntry.Value.Reference);

            var destParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(destinationFile));
            if (destParentDirEntry == null || !destParentDirEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("Destination directory not found", destinationFile);
            }

            var destParentDir = GetDirectory(destParentDirEntry.Value.Reference);

            var destDirEntry = destParentDir.GetEntryByName(Utilities.GetFileFromPath(destinationFile));
            if (destDirEntry != null && !destDirEntry.Value.IsDirectory)
            {
                if (overwrite)
                {
                    if (destDirEntry.Value.Reference.MftIndex == sourceEntry.Value.Reference.MftIndex)
                    {
                        throw new IOException("Destination file already exists and is the source file");
                    }

                    var oldFile = GetFile(destDirEntry.Value.Reference);
                    destParentDir.RemoveEntry(destDirEntry.Value);
                    if (oldFile.HardLinkCount == 0)
                    {
                        oldFile.Delete();
                    }
                }
                else
                {
                    throw new IOException("Destination file already exists");
                }
            }

            var newFile = File.CreateNew(_context, destParentDir.StandardInformation.FileAttributes);
            foreach (var origStream in origFile.AllStreams)
            {
                var newStream = newFile.GetStream(origStream.AttributeType, origStream.Name);

                switch (origStream.AttributeType)
                {
                    case AttributeType.Data:
                        newStream ??= newFile.CreateStream(origStream.AttributeType, origStream.Name);

                        using (var s = origStream.Open(FileAccess.Read))
                        using (var d = newStream.Value.Open(FileAccess.Write))
                        {
                            s.CopyTo(d);
                        }

                        break;

                    case AttributeType.StandardInformation:
                        var newSi = origStream.GetContent<StandardInformation>();
                        newStream?.SetContent(newSi);
                        break;
                }
            }

            AddFileToDirectory(newFile, destParentDir, Utilities.GetFileFromPath(destinationFile), options: default);
            destParentDirEntry.Value.UpdateFrom(destParentDir);
        }
    }

    /// <summary>
    /// Creates a directory.
    /// </summary>
    /// <param name="path">The path of the new directory.</param>
    public override void CreateDirectory(string path)
    {
        CreateDirectory(path, options: default);
    }

    /// <summary>
    /// Deletes a directory.
    /// </summary>
    /// <param name="path">The path of the directory to delete.</param>
    public override void DeleteDirectory(string path)
    {
        using (NtfsTransaction.Begin())
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new IOException("Unable to delete root directory");
            }

            var parent = Utilities.GetDirectoryFromPath(path);

            var parentDirEntry = GetDirectoryEntry(parent);
            if (parentDirEntry == null || !parentDirEntry.Value.IsDirectory)
            {
                throw new DirectoryNotFoundException($"No such directory: {path}");
            }

            var parentDir = GetDirectory(parentDirEntry.Value.Reference);

            var dirEntry = parentDir.GetEntryByName(Utilities.GetFileFromPath(path));
            if (dirEntry == null || !dirEntry.Value.IsDirectory)
            {
                throw new DirectoryNotFoundException($"No such directory: {path}");
            }

            var dir = GetDirectory(dirEntry.Value.Reference);

            if (!dir.IsEmpty)
            {
                throw new IOException("Unable to delete non-empty directory");
            }

            if ((dirEntry.Value.Details.FileAttributes & FileAttributes.ReparsePoint) != 0)
            {
                RemoveReparsePoint(dir);
            }

            RemoveFileFromDirectory(parentDir, dir, Utilities.GetFileFromPath(path));

            if (dir.HardLinkCount == 0)
            {
                dir.Delete();
            }
        }
    }

    /// <summary>
    /// Deletes a directory, optionally with all descendants.
    /// </summary>
    /// <param name="path">The path of the directory to delete.</param>
    /// <param name="recursive">Determines if the all descendants should be deleted.</param>
    public override void DeleteDirectory(string path, bool recursive)
    {
        if (recursive)
        {
            foreach (var dir in DoSearch(path, null, subFolders: true, dirs: true, files: false,
                static entry => entry.Key.FileNameNamespace != FileNameNamespace.Dos).ToArray())
            {
                DeleteDirectory(dir, true);
            }

            foreach (var file in DoSearch(path, null, subFolders: true, dirs: false, files: true,
                static entry => entry.Key.FileNameNamespace != FileNameNamespace.Dos).ToArray())
            {
                DeleteFile(file);
            }
        }

        DeleteDirectory(path);
    }

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="path">The path of the file to delete.</param>
    public override void DeleteFile(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntryPath = ParsePath(path, out var attributeName, out var attributeType);

            var parentDirPath = Utilities.GetDirectoryFromPath(dirEntryPath);

            var parentDirEntry = GetDirectoryEntry(parentDirPath);
            if (parentDirEntry == null || !parentDirEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("No such file", path);
            }

            var parentDir = GetDirectory(parentDirEntry.Value.Reference);

            var dirEntry = parentDir.GetEntryByName(Utilities.GetFileFromPath(dirEntryPath));

            if (dirEntry == null || dirEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("No such file", path);
            }

            var file = GetFile(dirEntry.Value.Reference)
                ?? throw new FileNotFoundException("Invalid directory entry, please check file system integrity", path);

            if (string.IsNullOrEmpty(attributeName) && attributeType == AttributeType.Data)
            {
                if ((dirEntry.Value.Details.FileAttributes & FileAttributes.ReparsePoint) != 0)
                {
                    RemoveReparsePoint(file);
                }

                RemoveFileFromDirectory(parentDir, file, Utilities.GetFileFromPath(path));

                if (file.HardLinkCount == 0)
                {
                    file.Delete();
                }
            }
            else
            {
                var attrStream = file.GetStream(attributeType, attributeName)
                    ?? throw new FileNotFoundException($"No such attribute: {attributeName}", path);

                file.RemoveStream(attrStream);
            }
        }
    }

    /// <summary>
    /// Indicates if a directory exists.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>true if the directory exists.</returns>
    public override bool DirectoryExists(string path)
    {
        using (NtfsTransaction.Begin())
        {
            // Special case - root directory
            if (string.IsNullOrEmpty(path))
            {
                return true;
            }

            var dirEntry = GetDirectoryEntry(path);
            return dirEntry != null && (dirEntry.Value.Details.FileAttributes & FileAttributes.Directory) != 0;
        }
    }

    /// <summary>
    /// Indicates if a file exists.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>true if the file exists.</returns>
    public override bool FileExists(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntryPath = ParsePath(path, out var attributeName, out var attributeType);

            var dirEntry = GetDirectoryEntry(dirEntryPath);
            if (dirEntry == null)
            {
                return false;
            }

            // Check that requested named attribute or default data attribute
            // exists
            var file = GetFile(dirEntry.Value.Reference);
            var attr = file.GetAttribute(attributeType, attributeName);
            if (attr == null)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Gets the names of subdirectories in a specified directory matching a specified
    /// search pattern, using a value to determine whether to search subdirectories.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <param name="searchPattern">The search string to match against.</param>
    /// <param name="searchOption">Indicates whether to search subdirectories.</param>
    /// <returns>Array of directories matching the search pattern.</returns>
    public override IEnumerable<string> GetDirectories(string path, string searchPattern, SearchOption searchOption)
    {
        var filter = Utilities.ConvertWildcardsToRegEx(searchPattern, ignoreCase: true);

        using (NtfsTransaction.Begin())
        {
            foreach (var dir in DoSearch(path, filter, searchOption == SearchOption.AllDirectories, dirs: true, files: false, FilterEntry))
            {
                yield return dir;
            }
        }
    }

    /// <summary>
    /// Gets the names of files in a specified directory matching a specified
    /// search pattern, using a value to determine whether to search subdirectories.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <param name="searchPattern">The search string to match against.</param>
    /// <param name="searchOption">Indicates whether to search subdirectories.</param>
    /// <returns>Array of files matching the search pattern.</returns>
    public override IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var filter = Utilities.ConvertWildcardsToRegEx(searchPattern, ignoreCase: true);

        using (NtfsTransaction.Begin())
        {
            foreach (var result in DoSearch(path, filter, searchOption == SearchOption.AllDirectories, dirs: false, files: true, FilterEntry))
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Gets the names of all files and subdirectories in a specified directory.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <returns>Array of files and subdirectories.</returns>
    public override IEnumerable<string> GetFileSystemEntries(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var parentDirEntry = GetDirectoryEntry(path)
                ?? throw new DirectoryNotFoundException($"The directory '{path}' does not exist");

            var parentDir = GetDirectory(parentDirEntry.Reference);

            foreach (var entry in parentDir
                .GetAllEntries(FilterEntry)
                .Select(m => Utilities.CombinePaths(path, m.Details.FileName)))
            {
                yield return entry;
            }
        }
    }

    internal bool FilterEntry(DirectoryIndexEntry entry)
    {
        // Weed out short-name entries for files and any hidden / system / metadata files.
        if (_context.Options.HideHiddenFiles && entry.Key.Flags.HasFlag(NtfsFileAttributes.Hidden)
            || _context.Options.HideSystemFiles && entry.Key.Flags.HasFlag(NtfsFileAttributes.System)
            || _context.Options.HideMetafiles && entry.Value.MftIndex < 16
            || _context.Options.HideDosFileNames && entry.Key.FileNameNamespace == FileNameNamespace.Dos)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the names of files and subdirectories in a specified directory matching a specified
    /// search pattern.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <param name="searchPattern">The search string to match against.</param>
    /// <returns>Array of files and subdirectories matching the search pattern.</returns>
    public override IEnumerable<string> GetFileSystemEntries(string path, string searchPattern)
    {
        var filter = Utilities.ConvertWildcardsToRegEx(searchPattern, ignoreCase: true);

        using (NtfsTransaction.Begin())
        {
            var parentDirEntry = GetDirectoryEntry(path)
                ?? throw new DirectoryNotFoundException($"The directory '{path}' does not exist");

            var parentDir = GetDirectory(parentDirEntry.Reference);

            var results = parentDir.GetAllEntries(FilterEntry)
                .Where(dirEntry => filter is null || filter(dirEntry.Details.SearchName))
                .Select(dirEntry => Utilities.CombinePaths(path, dirEntry.Details.FileName));

            foreach (var result in results)
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Gets the names of files and subdirectories in a specified directory matching a specified
    /// search pattern.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <param name="searchPattern">The search string to match against.</param>
    /// <param name="searchOption">Indicates whether to search subdirectories.</param>
    /// <returns>Array of files and subdirectories matching the search pattern.</returns>
    public override IEnumerable<string> GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
    {
        var filter = Utilities.ConvertWildcardsToRegEx(searchPattern, ignoreCase: true);

        using (NtfsTransaction.Begin())
        {
            foreach (var result in DoSearch(path, filter, searchOption == SearchOption.AllDirectories, dirs: true, files: true, FilterEntry))
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Moves a directory.
    /// </summary>
    /// <param name="sourceDirectoryName">The directory to move.</param>
    /// <param name="destinationDirectoryName">The target directory name.</param>
    public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
    {
        using (NtfsTransaction.Begin())
        {
            using (NtfsTransaction.Begin())
            {
                var sourceParentDirEntry =
                    GetDirectoryEntry(Utilities.GetDirectoryFromPath(sourceDirectoryName));
                if (sourceParentDirEntry == null || !sourceParentDirEntry.Value.IsDirectory)
                {
                    throw new DirectoryNotFoundException($"No such directory: {sourceDirectoryName}");
                }

                var sourceParentDir = GetDirectory(sourceParentDirEntry.Value.Reference);

                var sourceEntry =
                    sourceParentDir.GetEntryByName(Utilities.GetFileFromPath(sourceDirectoryName));
                if (sourceEntry == null || !sourceEntry.Value.IsDirectory)
                {
                    throw new DirectoryNotFoundException($"No such directory: {sourceDirectoryName}");
                }

                var file = GetFile(sourceEntry.Value.Reference);

                var destParentDirEntry =
                    GetDirectoryEntry(Utilities.GetDirectoryFromPath(destinationDirectoryName));
                if (destParentDirEntry == null || !destParentDirEntry.Value.IsDirectory)
                {
                    throw new DirectoryNotFoundException("Destination directory not found: " +
                                                         destinationDirectoryName);
                }

                var destParentDir = GetDirectory(destParentDirEntry.Value.Reference);

                var destDirEntry =
                    destParentDir.GetEntryByName(Utilities.GetFileFromPath(destinationDirectoryName));
                if (destDirEntry != null)
                {
                    throw new IOException("Destination directory already exists");
                }

                RemoveFileFromDirectory(sourceParentDir, file, sourceEntry.Value.Details.FileName);
                AddFileToDirectory(file, destParentDir, Utilities.GetFileFromPath(destinationDirectoryName), options: default);
            }
        }
    }

    /// <summary>
    /// Moves a file, allowing an existing file to be overwritten.
    /// </summary>
    /// <param name="sourceName">The file to move.</param>
    /// <param name="destinationName">The target file name.</param>
    /// <param name="overwrite">Whether to permit a destination file to be overwritten.</param>
    public override void MoveFile(string sourceName, string destinationName, bool overwrite)
    {
        using (NtfsTransaction.Begin())
        {
            var sourceParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(sourceName));
            if (sourceParentDirEntry == null || !sourceParentDirEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("No such file", sourceName);
            }

            var sourceParentDir = GetDirectory(sourceParentDirEntry.Value.Reference);

            var sourceEntry = sourceParentDir.GetEntryByName(Utilities.GetFileFromPath(sourceName));
            if (sourceEntry == null || sourceEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("No such file", sourceName);
            }

            var file = GetFile(sourceEntry.Value.Reference);

            var destParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(destinationName));
            if (destParentDirEntry == null || !destParentDirEntry.Value.IsDirectory)
            {
                throw new FileNotFoundException("Destination directory not found", destinationName);
            }

            var destParentDir = GetDirectory(destParentDirEntry.Value.Reference);

            var destDirEntry = destParentDir.GetEntryByName(Utilities.GetFileFromPath(destinationName));
            if (destDirEntry != null && !destDirEntry.Value.IsDirectory)
            {
                if (overwrite)
                {
                    if (destDirEntry.Value.Reference.MftIndex == sourceEntry.Value.Reference.MftIndex)
                    {
                        throw new IOException("Destination file already exists and is the source file");
                    }

                    var oldFile = GetFile(destDirEntry.Value.Reference);
                    destParentDir.RemoveEntry(destDirEntry.Value);
                    if (oldFile.HardLinkCount == 0)
                    {
                        oldFile.Delete();
                    }
                }
                else
                {
                    throw new IOException("Destination file already exists");
                }
            }

            RemoveFileFromDirectory(sourceParentDir, file, sourceEntry.Value.Details.FileName);
            AddFileToDirectory(file, destParentDir, Utilities.GetFileFromPath(destinationName), options: default);
        }
    }

    /// <summary>
    /// Opens the specified file.
    /// </summary>
    /// <param name="path">The full path of the file to open.</param>
    /// <param name="mode">The file mode for the created stream.</param>
    /// <param name="access">The access permissions for the returned stream.</param>
    /// <returns>The new stream.</returns>
    public override SparseStream OpenFile(string path, FileMode mode, FileAccess access)
    {
        return OpenFile(path, mode, access, options: default);
    }

    /// <summary>
    /// Gets the attributes of a file or directory.
    /// </summary>
    /// <param name="path">The file or directory to inspect.</param>
    /// <returns>The attributes of the file or directory.</returns>
    public override FileAttributes GetAttributes(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            return dirEntry.Details.FileAttributes;
        }
    }

    /// <summary>
    /// Sets the attributes of a file or directory.
    /// </summary>
    /// <param name="path">The file or directory to change.</param>
    /// <param name="newValue">The new attributes of the file or directory.</param>
    public override void SetAttributes(string path, FileAttributes newValue)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var oldValue = dirEntry.Details.FileAttributes;
            var changedAttribs = oldValue ^ newValue;

            if (changedAttribs == 0)
            {
                // Abort - nothing changed
                return;
            }

            if ((changedAttribs & NonSettableFileAttributes) != 0)
            {
                throw new ArgumentException("Attempt to change attributes that are read-only", nameof(newValue));
            }

            var file = GetFile(dirEntry.Reference);

            if ((changedAttribs & FileAttributes.SparseFile) != 0)
            {
                if (dirEntry.IsDirectory)
                {
                    throw new ArgumentException("Attempt to change sparse attribute on a directory",
                        nameof(newValue));
                }

                if ((newValue & FileAttributes.SparseFile) == 0)
                {
                    throw new ArgumentException("Attempt to remove sparse attribute from file", nameof(newValue));
                }

                var ntfsAttr = file.GetAttribute(AttributeType.Data, null);
                if ((ntfsAttr.Flags & AttributeFlags.Compressed) != 0)
                {
                    throw new ArgumentException("Attempt to mark compressed file as sparse", nameof(newValue));
                }

                ntfsAttr.Flags |= AttributeFlags.Sparse;
                if (ntfsAttr.IsNonResident)
                {
                    ntfsAttr.CompressedDataSize = ntfsAttr.PrimaryRecord.AllocatedLength;
                    ntfsAttr.CompressionUnitSize = 16;
                    ((NonResidentAttributeBuffer)ntfsAttr.RawBuffer).AlignVirtualClusterCount();
                }
            }

            if ((changedAttribs & FileAttributes.Compressed) != 0 && !dirEntry.IsDirectory)
            {
                if ((newValue & FileAttributes.Compressed) == 0)
                {
                    throw new ArgumentException("Attempt to remove compressed attribute from file", nameof(newValue));
                }

                var ntfsAttr = file.GetAttribute(AttributeType.Data, null);
                if ((ntfsAttr.Flags & AttributeFlags.Sparse) != 0)
                {
                    throw new ArgumentException("Attempt to mark sparse file as compressed", nameof(newValue));
                }

                ntfsAttr.Flags |= AttributeFlags.Compressed;
                if (ntfsAttr.IsNonResident)
                {
                    ntfsAttr.CompressedDataSize = ntfsAttr.PrimaryRecord.AllocatedLength;
                    ntfsAttr.CompressionUnitSize = 16;
                    ((NonResidentAttributeBuffer)ntfsAttr.RawBuffer).AlignVirtualClusterCount();
                }
            }

            UpdateStandardInformation(dirEntry, file,
                delegate (StandardInformation si) { si.FileAttributes = FileNameRecord.SetAttributes(newValue, si.FileAttributes); });
        }
    }

    /// <summary>
    /// Gets the creation time (in UTC) of a file or directory.
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <returns>The creation time.</returns>
    public override DateTime GetCreationTimeUtc(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            return dirEntry.Details.CreationTime;
        }
    }

    /// <summary>
    /// Sets the creation time (in UTC) of a file or directory.
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <param name="newTime">The new time to set.</param>
    public override void SetCreationTimeUtc(string path, DateTime newTime)
    {
        using (NtfsTransaction.Begin())
        {
            UpdateStandardInformation(path, delegate (StandardInformation si) { si.CreationTime = newTime; });
        }
    }

    /// <summary>
    /// Gets the last access time (in UTC) of a file or directory.
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <returns>The last access time.</returns>
    public override DateTime GetLastAccessTimeUtc(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            return dirEntry.Details.LastAccessTime;
        }
    }

    /// <summary>
    /// Sets the last access time (in UTC) of a file or directory.
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <param name="newTime">The new time to set.</param>
    public override void SetLastAccessTimeUtc(string path, DateTime newTime)
    {
        using (NtfsTransaction.Begin())
        {
            UpdateStandardInformation(path, delegate (StandardInformation si) { si.LastAccessTime = newTime; });
        }
    }

    /// <summary>
    /// Gets the last modification time (in UTC) of a file or directory.
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <returns>The last write time.</returns>
    public override DateTime GetLastWriteTimeUtc(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            return dirEntry.Details.ModificationTime;
        }
    }

    /// <summary>
    /// Sets the last modification time (in local time) of a file or directory.
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <param name="newTime">The new time to set.</param>
    public override void SetLastWriteTimeUtc(string path, DateTime newTime)
    {
        using (NtfsTransaction.Begin())
        {
            UpdateStandardInformation(path, delegate (StandardInformation si) { si.ModificationTime = newTime; });
        }
    }

    /// <summary>
    /// Gets the length of a file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>The length in bytes.</returns>
    public override long GetFileLength(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntryPath = ParsePath(path, out var attributeName, out var attributeType);

            var dirEntry = GetDirectoryEntry(dirEntryPath)
                ?? throw new FileNotFoundException("File not found", path);

            // Ordinary file length request, use info from the directory entry for efficiency - if allowed.
            if (NtfsOptions.FileLengthFromDirectoryEntries && attributeName == null &&
                attributeType == AttributeType.Data)
            {
                return (long)dirEntry.Details.RealSize;
            }

            var file = GetFile(dirEntry.Reference);
            var attr = file.GetAttribute(attributeType, attributeName)
                ?? throw new FileNotFoundException($"No such attribute '{attributeName}({attributeType})'");

            return attr.Length;
        }
    }

    public override DiscFileInfo GetFileInfo(string path)
    {
        using (NtfsTransaction.Begin())
        {
            try
            {
                var dirEntryPath = ParsePath(path, out var attributeName, out var attributeType);

                var dirEntry = GetDirectoryEntry(dirEntryPath);
                if (dirEntry == null)
                {
                    return new(this, path);
                }

                // Ordinary file length request, use info from directory entry for efficiency - if allowed
                if (NtfsOptions.FileLengthFromDirectoryEntries && attributeName == null &&
                    attributeType == AttributeType.Data)
                {
                    if (dirEntry.Value.Details.FileAttributes.HasFlag(FileAttributes.Directory))
                    {
                        return new(this, path);
                    }

                    return new CachedDiscFileInfo(this, path, dirEntry.Value.Details.FileAttributes,
                                               dirEntry.Value.Details.CreationTime, dirEntry.Value.Details.LastAccessTime,
                                               dirEntry.Value.Details.ModificationTime, (long)dirEntry.Value.Details.RealSize);
                }

                // Alternate stream / attribute, pull info from attribute record
                var file = GetFile(dirEntry.Value.Reference);
                var attr = file.GetAttribute(attributeType, attributeName);
                if (attr != null)
                {
                    return new CachedDiscFileInfo(this, path, dirEntry.Value.Details.FileAttributes, dirEntry.Value.Details.CreationTime,
                                               dirEntry.Value.Details.LastAccessTime, dirEntry.Value.Details.ModificationTime, attr.Length);
                }
            }
            catch
            {
            }

            return new(this, path);
        }
    }

    public override DiscFileSystemInfo GetFileSystemInfo(string path)
    {
        using (NtfsTransaction.Begin())
        {
            try
            {
                var dirEntryPath = ParsePath(path, out var attributeName, out var attributeType);

                var dirEntry = GetDirectoryEntry(dirEntryPath);
                if (dirEntry == null)
                {
                    return new(this, path);
                }

                // Ordinary file length request, use info from directory entry for efficiency - if allowed
                if (NtfsOptions.FileLengthFromDirectoryEntries && attributeName == null &&
                    attributeType == AttributeType.Data)
                {
                    if (dirEntry.Value.Details.FileAttributes.HasFlag(FileAttributes.Directory))
                    {
                        return new CachedDiscDirectoryInfo(this, path, dirEntry.Value.Details.FileAttributes,
                                                        dirEntry.Value.Details.CreationTime, dirEntry.Value.Details.LastAccessTime,
                                                        dirEntry.Value.Details.ModificationTime);
                    }

                    return new CachedDiscFileInfo(this, path, dirEntry.Value.Details.FileAttributes,
                                               dirEntry.Value.Details.CreationTime, dirEntry.Value.Details.LastAccessTime,
                                               dirEntry.Value.Details.ModificationTime, (long)dirEntry.Value.Details.RealSize);
                }

                // Alternate stream / attribute, pull info from attribute record
                var file = GetFile(dirEntry.Value.Reference);
                var attr = file.GetAttribute(attributeType, attributeName);
                if (attr != null)
                {
                    return new CachedDiscFileInfo(this, path, dirEntry.Value.Details.FileAttributes, dirEntry.Value.Details.ModificationTime,
                                               dirEntry.Value.Details.ModificationTime, dirEntry.Value.Details.ModificationTime, attr.Length);
                }
            }
            catch
            {
            }

            return new(this, path);
        }
    }

    public override DiscDirectoryInfo GetDirectoryInfo(string path)
    {
        using (NtfsTransaction.Begin())
        {
            try
            {
                var dirEntryPath = ParsePath(path, out var attributeName, out var attributeType);

                var dirEntry = GetDirectoryEntry(dirEntryPath);
                if (dirEntry == null)
                {
                    return new(this, path);
                }

                // Ordinary file length request, use info from directory entry for efficiency - if allowed
                if (NtfsOptions.FileLengthFromDirectoryEntries && attributeName == null &&
                    attributeType == AttributeType.Data &&
                    dirEntry.Value.Details.FileAttributes.HasFlag(FileAttributes.Directory))
                {
                    return new CachedDiscDirectoryInfo(this,
                                                       path,
                                                       dirEntry.Value.Details.FileAttributes,
                                                       dirEntry.Value.Details.ModificationTime,
                                                       dirEntry.Value.Details.ModificationTime,
                                                       dirEntry.Value.Details.ModificationTime);
                }
            }
            catch
            {
            }

            return new(this, path);
        }
    }

    /// <summary>
    /// Converts a cluster (index) into an absolute byte position in the underlying stream.
    /// </summary>
    /// <param name="cluster">The cluster to convert.</param>
    /// <returns>The corresponding absolute byte position.</returns>
    public long ClusterToOffset(long cluster)
    {
        return cluster * ClusterSize;
    }

    /// <summary>
    /// Converts an absolute byte position in the underlying stream to a cluster (index).
    /// </summary>
    /// <param name="offset">The byte position to convert.</param>
    /// <returns>The cluster containing the specified byte.</returns>
    public long OffsetToCluster(long offset)
    {
        return offset / ClusterSize;
    }

    /// <summary>
    /// Converts a file name to the list of clusters occupied by the file's data.
    /// </summary>
    /// <param name="path">The path to inspect.</param>
    /// <returns>The clusters as a list of cluster ranges.</returns>
    /// <remarks>Note that in some file systems, small files may not have dedicated
    /// clusters.  Only dedicated clusters will be returned.</remarks>
    public IEnumerable<Range<long, long>> PathToClusters(string path)
    {
        SplitPath(path, out var plainPath, out var attributeName);

        var dirEntry = GetDirectoryEntry(plainPath);
        if (dirEntry == null || dirEntry.Value.IsDirectory)
        {
            throw new FileNotFoundException("No such file", path);
        }

        var file = GetFile(dirEntry.Value.Reference);

        var stream = file.GetStream(AttributeType.Data, attributeName)
            ?? throw new FileNotFoundException(
                $"File does not contain '{attributeName}' data attribute", path);

        return stream.GetClusters();
    }

    /// <summary>
    /// Converts a file name to the extents containing its data.
    /// </summary>
    /// <param name="path">The path to inspect.</param>
    /// <returns>The file extents, as absolute byte positions in the underlying stream.</returns>
    /// <remarks>Use this method with caution - NTFS supports encrypted, sparse and compressed files
    /// where bytes are not directly stored in extents.  Small files may be entirely stored in the 
    /// Master File Table, where corruption protection algorithms mean that some bytes do not contain
    /// the expected values.  This method merely indicates where file data is stored,
    /// not what's stored.  To access the contents of a file, use OpenFile.</remarks>
    public IEnumerable<StreamExtent> PathToExtents(string path)
    {
        SplitPath(path, out var plainPath, out var attributeName);

        var dirEntry = GetDirectoryEntry(plainPath);
        if (dirEntry == null || dirEntry.Value.IsDirectory)
        {
            throw new FileNotFoundException("No such file", path);
        }

        var file = GetFile(dirEntry.Value.Reference);

        var stream = file.GetStream(AttributeType.Data, attributeName)
            ?? throw new FileNotFoundException(
                $"File does not contain '{attributeName}' data attribute", path);

        return stream.GetAbsoluteExtents();
    }

    /// <summary>
    /// Gets number of allocated clusters for a file.
    /// </summary>
    /// <param name="path">The path to inspect.</param>
    /// <returns>Number of clusters allocated</returns>
    public long GetAllocatedClustersCount(string path)
    {
        SplitPath(path, out var plainPath, out var attributeName);

        var dirEntry = GetDirectoryEntry(plainPath);
        if (dirEntry == null || dirEntry.Value.IsDirectory)
        {
            throw new FileNotFoundException("No such file", path);
        }

        var file = GetFile(dirEntry.Value.Reference);

        var stream = file.GetStream(AttributeType.Data, attributeName)
            ?? throw new FileNotFoundException(
                $"File does not contain '{attributeName}' data attribute", path);

        return stream.GetAllocatedClustersCount();
    }

    /// <summary>
    /// Gets an object that can convert between clusters and files.
    /// </summary>
    /// <returns>The cluster map.</returns>
    public ClusterMap BuildClusterMap()
    {
        return _context.Mft.GetClusterMap();
    }

    /// <summary>
    /// Reads the boot code of the file system into a byte array.
    /// </summary>
    /// <returns>The boot code, or <c>null</c> if not available.</returns>
    public override byte[] ReadBootCode()
    {
        using var s = OpenFile(@"\$Boot", FileMode.Open);
        return s.ReadExactly((int)s.Length);
    }

    /// <summary>
    /// Writes a diagnostic dump of key NTFS structures.
    /// </summary>
    /// <param name="writer">The writer to receive the dump.</param>
    /// <param name="linePrefix">The indent to apply to the start of each line of output.</param>
    public void Dump(TextWriter writer, string linePrefix)
    {
        writer.WriteLine($"{linePrefix}NTFS File System Dump");
        writer.WriteLine($"{linePrefix}=====================");

        ////_context.Mft.Dump(writer, linePrefix);
        writer.WriteLine(linePrefix);
        _context.BiosParameterBlock.Dump(writer, linePrefix);

        if (_context.SecurityDescriptors != null)
        {
            writer.WriteLine(linePrefix);
            _context.SecurityDescriptors.Dump(writer, linePrefix);
        }

        if (_context.ObjectIds != null)
        {
            writer.WriteLine(linePrefix);
            _context.ObjectIds.Dump(writer, linePrefix);
        }

        if (_context.ReparsePoints != null)
        {
            writer.WriteLine(linePrefix);
            _context.ReparsePoints.Dump(writer, linePrefix);
        }

        if (_context.Quotas != null)
        {
            writer.WriteLine(linePrefix);
            _context.Quotas.Dump(writer, linePrefix);
        }

        writer.WriteLine(linePrefix);
        GetDirectory(MasterFileTable.RootDirIndex).Dump(writer, linePrefix);

        writer.WriteLine(linePrefix);
        writer.WriteLine($"{linePrefix}FULL FILE LISTING");
        foreach (var record in _context.Mft.Records)
        {
            // Don't go through cache - these are short-lived, and this is (just!) diagnostics
            var f = new File(_context, record);
            f.Dump(writer, linePrefix);

            foreach (var stream in f.AllStreams)
            {
                if (stream.AttributeType == AttributeType.IndexRoot)
                {
                    try
                    {
                        writer.WriteLine($"{linePrefix}  INDEX ({stream.Name})");
                        f.GetIndex(stream.Name).Dump(writer, $"{linePrefix}    ");
                    }
                    catch (Exception e)
                    {
                        writer.WriteLine($"{linePrefix}!Exception: {e}");
                    }
                }
            }
        }

        writer.WriteLine(linePrefix);
        writer.WriteLine($"{linePrefix}DIRECTORY TREE");
        writer.WriteLine($"{linePrefix}{Path.DirectorySeparatorChar} (5)");
        DumpDirectory(GetDirectory(MasterFileTable.RootDirIndex), writer, linePrefix); // 5 = Root Dir

        writer.WriteLine(linePrefix);
        writer.WriteLine($"{linePrefix}TOTAL SIZE: {Size} ({SizeFormatting.FormatBytes(Size)})");
        var usedSpace = UsedSpace;
        writer.WriteLine($"{linePrefix}USED SPACE: {usedSpace} ({SizeFormatting.FormatBytes(usedSpace)})");
    }

    /// <summary>
    /// Indicates whether the file is known by other names.
    /// </summary>
    /// <param name="path">The file to inspect.</param>
    /// <returns><c>true</c> if the file has other names, else <c>false</c>.</returns>
    public bool HasHardLinks(string path)
    {
        return GetHardLinkCount(path) > 1;
    }

    /// <summary>
    /// Gets the security descriptor associated with the file or directory.
    /// </summary>
    /// <param name="path">The file or directory to inspect.</param>
    /// <returns>The security descriptor.</returns>
    public RawSecurityDescriptor GetSecurity(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var file = GetFile(dirEntry.Reference);
            return DoGetSecurity(file);
        }
    }

    /// <summary>
    /// Sets the security descriptor associated with the file or directory.
    /// </summary>
    /// <param name="path">The file or directory to change.</param>
    /// <param name="securityDescriptor">The new security descriptor.</param>
    public void SetSecurity(string path, RawSecurityDescriptor securityDescriptor)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var file = GetFile(dirEntry.Reference);
            DoSetSecurity(file, securityDescriptor);

            // Update the directory entry used to open the file
            dirEntry.UpdateFrom(file);
        }
    }

    /// <summary>
    /// Removes the security descriptor associated with the file or directory.
    /// </summary>
    /// <param name="path">The file or directory to change.</param>
    public void RemoveSecurity(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else
            {
                var file = GetFile(dirEntry.Value.Reference);
                DoRemoveSecurity(file);

                // Update the directory entry used to open the file
                dirEntry.Value.UpdateFrom(file);
            }
        }
    }

    /// <summary>
    /// Sets the reparse point data on a file or directory.
    /// </summary>
    /// <param name="path">The file to set the reparse point on.</param>
    /// <param name="reparsePoint">The new reparse point.</param>
    public void SetReparsePoint(string path, ReparsePoint reparsePoint)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var file = GetFile(dirEntry.Reference);

            var stream = file.GetStream(AttributeType.ReparsePoint, null);
            if (stream != null)
            {
                // If there's an existing reparse point, unhook it.
                using var contentStream = stream.Value.Open(FileAccess.Read);
                var rp = contentStream.ReadStruct<ReparsePointRecord>((int)contentStream.Length);
                _context.ReparsePoints.Remove(rp.Tag, dirEntry.Reference);
            }
            else
            {
                stream = file.CreateStream(AttributeType.ReparsePoint, null);
            }

            // Set the new content
            var newRp = new ReparsePointRecord
            {
                Tag = (uint)reparsePoint.Tag,
                Content = reparsePoint.Content
            };

            var contentBuffer = ArrayPool<byte>.Shared.Rent(newRp.Size);
            try
            {
                newRp.WriteTo(contentBuffer, 0);
                using var contentStream = stream.Value.Open(FileAccess.ReadWrite);
                contentStream.Write(contentBuffer, 0, newRp.Size);
                contentStream.SetLength(newRp.Size);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(contentBuffer);
            }

            // Update the standard information attribute - so it reflects the actual file state
            var stdInfoStream = file.GetStream(AttributeType.StandardInformation, null);
            var si = stdInfoStream?.GetContent<StandardInformation>();
            si.FileAttributes |= NtfsFileAttributes.ReparsePoint;
            stdInfoStream?.SetContent(si);

            // Update the directory entry used to open the file, so it's accurate
            dirEntry.Details.EASizeOrReparsePointTag = newRp.Tag;
            dirEntry.UpdateFrom(file);

            // Write attribute changes back to the Master File Table
            file.UpdateRecordInMft();

            // Add the reparse point to the index
            _context.ReparsePoints.Add(newRp.Tag, dirEntry.Reference);
        }
    }

    /// <summary>
    /// Gets the reparse point data associated with a file or directory.
    /// </summary>
    /// <param name="path">The file to query.</param>
    /// <returns>The reparse point information.</returns>
    public ReparsePoint GetReparsePoint(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var file = GetFile(dirEntry.Reference);

            var stream = file.GetStream(AttributeType.ReparsePoint, null);
            if (stream != null)
            {

                using var contentStream = stream.Value.Open(FileAccess.Read);
                var rp = contentStream.ReadStruct<ReparsePointRecord>((int)contentStream.Length);
                return new ReparsePoint((int)rp.Tag, rp.Content);
            }
        }

        return null;
    }

    /// <summary>
    /// Removes a reparse point from a file or directory, without deleting the file or directory.
    /// </summary>
    /// <param name="path">The path to the file or directory to remove the reparse point from.</param>
    public void RemoveReparsePoint(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var file = GetFile(dirEntry.Reference);
            RemoveReparsePoint(file);

            // Update the directory entry used to open the file, so it's accurate
            dirEntry.UpdateFrom(file);

            // Write attribute changes back to the Master File Table
            file.UpdateRecordInMft();
        }
    }

    /// <summary>
    /// Gets the short name for a given path.
    /// </summary>
    /// <param name="path">The path to convert.</param>
    /// <returns>The short name.</returns>
    /// <remarks>
    /// This method only gets the short name for the final part of the path, to
    /// convert a complete path, call this method repeatedly, once for each path
    /// segment.  If there is no short name for the given path,<c>null</c> is
    /// returned.
    /// </remarks>
    public string GetShortName(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var parentPath = Utilities.GetDirectoryFromPath(path);
            var parentEntry = GetDirectoryEntry(parentPath);
            if (parentEntry == null || (parentEntry.Value.Details.FileAttributes & FileAttributes.Directory) == 0)
            {
                throw new DirectoryNotFoundException("Parent directory not found");
            }

            var dir = GetDirectory(parentEntry.Value.Reference)
                ?? throw new DirectoryNotFoundException("Parent directory not found");

            var givenEntry = dir.GetEntryByName(Utilities.GetFileFromPath(path))
                ?? throw new FileNotFoundException("Path not found", path);

            if (givenEntry.Details.FileNameNamespace == FileNameNamespace.Dos)
            {
                return givenEntry.Details.FileName;
            }

            if (givenEntry.Details.FileNameNamespace == FileNameNamespace.Win32)
            {
                var file = GetFile(givenEntry.Reference);

                foreach (var stream in file.GetStreams(AttributeType.FileName, null))
                {
                    var fnr = stream.GetContent<FileNameRecord>();
                    if (fnr.ParentDirectory.Equals(givenEntry.Details.ParentDirectory)
                        && fnr.FileNameNamespace == FileNameNamespace.Dos)
                    {
                        return fnr.FileName;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Sets the short name for a given file or directory.
    /// </summary>
    /// <param name="path">The full path to the file or directory to change.</param>
    /// <param name="shortName">The shortName, which should not include a path.</param>
    public void SetShortName(string path, string shortName)
    {
        if (!Utilities.Is8Dot3(shortName, ignoreCase: false))
        {
            throw new ArgumentException("Short name is not a valid 8.3 file name", nameof(shortName));
        }

        using (NtfsTransaction.Begin())
        {
            var parentPath = Utilities.GetDirectoryFromPath(path);
            var parentEntry = GetDirectoryEntry(parentPath);
            if (parentEntry == null || (parentEntry.Value.Details.FileAttributes & FileAttributes.Directory) == 0)
            {
                throw new DirectoryNotFoundException("Parent directory not found");
            }

            var dir = GetDirectory(parentEntry.Value.Reference)
                ?? throw new DirectoryNotFoundException("Parent directory not found");

            var givenEntry = dir.GetEntryByName(Utilities.GetFileFromPath(path))
                ?? throw new FileNotFoundException("Path not found", path);

            var givenNamespace = givenEntry.Details.FileNameNamespace;
            var file = GetFile(givenEntry.Reference);

            if (givenNamespace == FileNameNamespace.Posix && file.HasWin32OrDosName)
            {
                throw new InvalidOperationException("Cannot set a short name on hard links");
            }

            // Convert Posix/Win32AndDos to just Win32
            if (givenEntry.Details.FileNameNamespace != FileNameNamespace.Win32)
            {
                dir.RemoveEntry(givenEntry);
                dir.AddEntry(file, givenEntry.Details.FileName, FileNameNamespace.Win32);
            }

            // Remove any existing Dos names, and set the new one
            var nameStreams = new List<NtfsStream>(file.GetStreams(AttributeType.FileName, null));
            foreach (var stream in nameStreams)
            {
                var fnr = stream.GetContent<FileNameRecord>();
                if (fnr.ParentDirectory.Equals(givenEntry.Details.ParentDirectory)
                    && fnr.FileNameNamespace == FileNameNamespace.Dos)
                {
                    var oldEntry = dir.GetEntryByName(fnr.FileName);
                    dir.RemoveEntry(oldEntry.Value);
                }
            }

            dir.AddEntry(file, shortName, FileNameNamespace.Dos);

            parentEntry.Value.UpdateFrom(dir);
        }
    }

    /// <summary>
    /// Gets the standard file information for a file.
    /// </summary>
    /// <param name="path">The full path to the file or directory to query.</param>
    /// <returns>The standard file information.</returns>
    public WindowsFileInformation GetFileStandardInformation(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var file = GetFile(dirEntry.Reference);
            var si = file.StandardInformation;

            return new WindowsFileInformation
            {
                CreationTime = si.CreationTime,
                LastAccessTime = si.LastAccessTime,
                ChangeTime = si.MftChangedTime,
                LastWriteTime = si.ModificationTime,
                FileAttributes = StandardInformation.ConvertFlags(si.FileAttributes, file.IsDirectory)
            };
        }
    }

    /// <summary>
    /// Sets the standard file information for a file.
    /// </summary>
    /// <param name="path">The full path to the file or directory to query.</param>
    /// <param name="info">The standard file information.</param>
    public void SetFileStandardInformation(string path, WindowsFileInformation info)
    {
        using (NtfsTransaction.Begin())
        {
            UpdateStandardInformation(
                path,
                delegate (StandardInformation si)
                {
                    si.CreationTime = info.CreationTime;
                    si.LastAccessTime = info.LastAccessTime;
                    si.MftChangedTime = info.ChangeTime;
                    si.ModificationTime = info.LastWriteTime;
                    si.FileAttributes = StandardInformation.SetFileAttributes(info.FileAttributes, si.FileAttributes);
                });
        }
    }

    /// <summary>
    /// Gets the file id for a given path.
    /// </summary>
    /// <param name="path">The path to get the id of.</param>
    /// <returns>The file id.</returns>
    /// <remarks>
    /// The returned file id includes the MFT index of the primary file record for the file.
    /// The file id can be used to determine if two paths refer to the same actual file.
    /// The MFT index is held in the lower 48 bits of the id.
    /// </remarks>
    public long GetFileId(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            return (long)dirEntry.Reference.Value;
        }
    }

    /// <summary>
    /// Gets the names of the alternate data streams for a file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>
    /// The list of alternate data streams (or empty, if none).  To access the contents
    /// of the alternate streams, use OpenFile(path + ":" + name, ...).
    /// </returns>
    public IEnumerable<string> GetAlternateDataStreams(string path)
    {
        var dirEntry = GetDirectoryEntry(path)
            ?? throw new FileNotFoundException("File not found", path);

        var file = GetFile(dirEntry.Reference)
            ?? throw new FileNotFoundException("File not found", path);

        foreach (var attr in file.AllStreams)
        {
            if (attr.AttributeType == AttributeType.Data && !string.IsNullOrEmpty(attr.Name))
            {
                yield return attr.Name;
            }
        }
    }

    /// <summary>
    /// Initializes a new NTFS file system.
    /// </summary>
    /// <param name="stream">The stream to write the new file system to.</param>
    /// <param name="label">The label for the new file system.</param>
    /// <param name="diskGeometry">The disk geometry of the disk containing the new file system.</param>
    /// <param name="firstSector">The first sector of the new file system on the disk.</param>
    /// <param name="sectorCount">The number of sectors allocated to the new file system on the disk.</param>
    /// <returns>The newly-initialized file system.</returns>
    public static NtfsFileSystem Format(
        Stream stream,
        string label,
        Geometry diskGeometry,
        long firstSector,
        long sectorCount)
    {
        var formatter = new NtfsFormatter
        {
            Label = label,
            DiskGeometry = diskGeometry,
            FirstSector = firstSector,
            SectorCount = sectorCount
        };
        return formatter.Format(stream);
    }

    /// <summary>
    /// Initializes a new NTFS file system.
    /// </summary>
    /// <param name="stream">The stream to write the new file system to.</param>
    /// <param name="label">The label for the new file system.</param>
    /// <param name="diskGeometry">The disk geometry of the disk containing the new file system.</param>
    /// <param name="firstSector">The first sector of the new file system on the disk.</param>
    /// <param name="sectorCount">The number of sectors allocated to the new file system on the disk.</param>
    /// <param name="bootCode">The Operating System's boot code.</param>
    /// <returns>The newly-initialized file system.</returns>
    public static NtfsFileSystem Format(
        Stream stream,
        string label,
        Geometry diskGeometry,
        long firstSector,
        long sectorCount,
        byte[] bootCode)
    {
        var formatter = new NtfsFormatter
        {
            Label = label,
            DiskGeometry = diskGeometry,
            FirstSector = firstSector,
            SectorCount = sectorCount,
            BootCode = bootCode
        };
        return formatter.Format(stream);
    }

    /// <summary>
    /// Initializes a new NTFS file system.
    /// </summary>
    /// <param name="stream">The stream to write the new file system to.</param>
    /// <param name="label">The label for the new file system.</param>
    /// <param name="diskGeometry">The disk geometry of the disk containing the new file system.</param>
    /// <param name="firstSector">The first sector of the new file system on the disk.</param>
    /// <param name="sectorCount">The number of sectors allocated to the new file system on the disk.</param>
    /// <param name="options">The formatting options.</param>
    /// <returns>The newly-initialized file system.</returns>
    public static NtfsFileSystem Format(
        Stream stream,
        string label,
        Geometry diskGeometry,
        long firstSector,
        long sectorCount,
        NtfsFormatOptions options)
    {
        var formatter = new NtfsFormatter
        {
            Label = label,
            DiskGeometry = diskGeometry,
            FirstSector = firstSector,
            SectorCount = sectorCount,
            BootCode = options.BootCode,
            ComputerAccount = options.ComputerAccount
        };
        return formatter.Format(stream);
    }

    /// <summary>
    /// Initializes a new NTFS file system.
    /// </summary>
    /// <param name="volume">The volume to format.</param>
    /// <param name="label">The label for the new file system.</param>
    /// <returns>The newly-initialized file system.</returns>
    public static NtfsFileSystem Format(
        VolumeInfo volume,
        string label)
    {
        var formatter = new NtfsFormatter
        {
            Label = label,
            DiskGeometry = volume.BiosGeometry ?? Geometry.MakeBiosSafe(volume.PhysicalGeometry, volume.Length),
            FirstSector = volume.PhysicalStartSector,
            SectorCount = volume.Length / Sizes.Sector
        };
        return formatter.Format(volume.Open());
    }

    /// <summary>
    /// Initializes a new NTFS file system.
    /// </summary>
    /// <param name="volume">The volume to format.</param>
    /// <param name="label">The label for the new file system.</param>
    /// <param name="bootCode">The Operating System's boot code.</param>
    /// <returns>The newly-initialized file system.</returns>
    public static NtfsFileSystem Format(
        VolumeInfo volume,
        string label,
        byte[] bootCode)
    {
        var formatter = new NtfsFormatter
        {
            Label = label,
            DiskGeometry = volume.BiosGeometry ?? Geometry.MakeBiosSafe(volume.PhysicalGeometry, volume.Length),
            FirstSector = volume.PhysicalStartSector,
            SectorCount = volume.Length / Sizes.Sector,
            BootCode = bootCode
        };
        return formatter.Format(volume.Open());
    }

    /// <summary>
    /// Initializes a new NTFS file system.
    /// </summary>
    /// <param name="volume">The volume to format.</param>
    /// <param name="label">The label for the new file system.</param>
    /// <param name="options">The formatting options.</param>
    /// <returns>The newly-initialized file system.</returns>
    public static NtfsFileSystem Format(
        VolumeInfo volume,
        string label,
        NtfsFormatOptions options)
    {
        var formatter = new NtfsFormatter
        {
            Label = label,
            DiskGeometry = volume.BiosGeometry ?? Geometry.MakeBiosSafe(volume.PhysicalGeometry, volume.Length),
            FirstSector = volume.PhysicalStartSector,
            SectorCount = volume.Length / Sizes.Sector,
            BootCode = options.BootCode,
            ComputerAccount = options.ComputerAccount
        };
        return formatter.Format(volume.Open());
    }

    /// <summary>
    /// Detects if a stream contains an NTFS file system.
    /// </summary>
    /// <param name="stream">The stream to inspect.</param>
    /// <returns><c>true</c> if NTFS is detected, else <c>false</c>.</returns>
    public static bool Detect(Stream stream)
    {
        if (stream.Length < 512)
        {
            return false;
        }

        stream.Position = 0;
        Span<byte> bytes = stackalloc byte[512];
        stream.ReadExactly(bytes);
        var bpb = BiosParameterBlock.FromBytes(bytes);

        return bpb.IsValid(stream.Length);
    }

    /// <summary>
    /// Gets the Master File Table for this file system.
    /// </summary>
    /// <remarks>
    /// Use the returned object to explore the internals of the file system - most people will
    /// never need to use this.
    /// </remarks>
    /// <returns>The Master File Table.</returns>
    public Internals.MasterFileTable GetMasterFileTable()
    {
        return new Internals.MasterFileTable(_context, _context.Mft);
    }

    /// <summary>
    /// Creates a directory.
    /// </summary>
    /// <param name="path">The path of the new directory.</param>
    /// <param name="options">Options controlling attributes of the new Director, or <c>null</c> for defaults.</param>
    public void CreateDirectory(string path, NewFileOptions options)
    {
        using (NtfsTransaction.Begin())
        {
            var pathElements = path.Split(Utilities.PathSeparators, StringSplitOptions.RemoveEmptyEntries);

            var focusDir = GetDirectory(MasterFileTable.RootDirIndex);
            var focusDirEntry = focusDir.DirectoryEntry;

            for (var i = 0; i < pathElements.Length; ++i)
            {
                var childDirEntry = focusDir.GetEntryByName(pathElements[i]);
                if (childDirEntry == null)
                {
                    var newDirAttrs = focusDir.StandardInformation.FileAttributes;
                    if (options.Compressed.HasValue)
                    {
                        if (options.Compressed.Value)
                        {
                            newDirAttrs |= NtfsFileAttributes.Compressed;
                        }
                        else
                        {
                            newDirAttrs &= ~NtfsFileAttributes.Compressed;
                        }
                    }

                    var childDir = Directory.CreateNew(_context, newDirAttrs);
                    try
                    {
                        childDirEntry = AddFileToDirectory(childDir, focusDir, pathElements[i], options);

                        var parentSd = DoGetSecurity(focusDir);
                        RawSecurityDescriptor newSd;
                        if (options.SecurityDescriptor is not null)
                        {
                            newSd = options.SecurityDescriptor;
                        }
                        else
                        {
                            newSd = SecurityDescriptor.CalcNewObjectDescriptor(parentSd, false);
                        }

                        DoSetSecurity(childDir, newSd);
                        childDirEntry.Value.UpdateFrom(childDir);

                        // Update the directory entry by which we found the directory we've just modified
                        focusDirEntry.Value.UpdateFrom(focusDir);

                        focusDir = childDir;
                    }
                    finally
                    {
                        if (childDir.HardLinkCount == 0)
                        {
                            childDir.Delete();
                        }
                    }
                }
                else
                {
                    focusDir = GetDirectory(childDirEntry.Value.Reference);
                }

                focusDirEntry = childDirEntry.Value;
            }
        }
    }

    /// <summary>
    /// Opens the specified file.
    /// </summary>
    /// <param name="path">The full path of the file to open.</param>
    /// <param name="mode">The file mode for the created stream.</param>
    /// <param name="access">The access permissions for the returned stream.</param>
    /// <param name="options">Options controlling attributes of a new file, or <c>null</c> for defaults (ignored if file exists).</param>
    /// <returns>The new stream.</returns>
    public SparseStream OpenFile(string path, FileMode mode, FileAccess access, NewFileOptions options)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntryPath = ParsePath(path, out var attributeName, out var attributeType);

            var entry = GetDirectoryEntry(dirEntryPath);

            if (entry == null)
            {
                if (mode == FileMode.Open)
                {
                    throw new FileNotFoundException("No such file", path);
                }

                entry = CreateNewFile(dirEntryPath, options);
            }
            else if (mode == FileMode.CreateNew)
            {
                throw new IOException("File already exists");
            }

            if (string.IsNullOrWhiteSpace(attributeName) &&
                (entry.Value.Details.FileAttributes & FileAttributes.Directory) != 0 &&
                attributeType == AttributeType.Data)
            {
                throw new IOException("Attempt to open directory as a file");
            }

            var file = GetFile(entry.Value.Reference)
                ?? throw new FileNotFoundException("File not found", path);

            var ntfsStream = file.GetStream(attributeType, attributeName);

            if (ntfsStream == null)
            {
                if (mode is FileMode.Create or FileMode.OpenOrCreate)
                {
                    ntfsStream = file.CreateStream(attributeType, attributeName);
                }
                else
                {
                    throw new FileNotFoundException("No such attribute on file", path);
                }
            }

            var stream = NtfsFileStream.Open(file, entry.Value, attributeType, attributeName, access)
                ?? throw new InvalidFileSystemException("File system corrupt");

            if (mode is FileMode.Create or FileMode.Truncate)
            {
                stream.SetLength(0);
            }

            return stream;
        }
    }

    /// <summary>
    /// Opens an existing file stream.
    /// </summary>
    /// <param name="file">The file containing the stream.</param>
    /// <param name="type">The type of the stream.</param>
    /// <param name="name">The name of the stream.</param>
    /// <param name="access">The desired access to the stream.</param>
    /// <returns>A stream that can be used to access the file stream.</returns>
    [Obsolete(@"Use OpenFile with filename:attributename:$attributetype syntax (e.g. \FILE.TXT:STREAM:$DATA)", false
     )]
    public SparseStream OpenRawStream(string file, AttributeType type, string name, FileAccess access)
    {
        using (NtfsTransaction.Begin())
        {
            var entry = GetDirectoryEntry(file)
                ?? throw new FileNotFoundException("No such file", file);

            var fileObj = GetFile(entry.Reference);
            return fileObj.OpenStream(type, name, access);
        }
    }

    /// <summary>
    /// Creates an NTFS hard link to an existing file.
    /// </summary>
    /// <param name="sourceName">An existing name of the file.</param>
    /// <param name="destinationName">The name of the new hard link to the file.</param>
    public void CreateHardLink(string sourceName, string destinationName)
    {
        using (NtfsTransaction.Begin())
        {
            var sourceDirEntry = GetDirectoryEntry(sourceName)
                ?? throw new FileNotFoundException("Source file not found", sourceName);

            var destinationDirName = Utilities.GetDirectoryFromPath(destinationName);
            var destinationDirSelfEntry = GetDirectoryEntry(destinationDirName);
            if (destinationDirSelfEntry == null ||
                (destinationDirSelfEntry.Value.Details.FileAttributes & FileAttributes.Directory) == 0)
            {
                throw new FileNotFoundException("Destination directory not found", destinationDirName);
            }

            var destinationDir = GetDirectory(destinationDirSelfEntry.Value.Reference)
                ?? throw new FileNotFoundException("Destination directory not found", destinationDirName);

            var destinationDirEntry = GetDirectoryEntry(destinationDir,
                Utilities.GetFileFromPath(destinationName));
            if (destinationDirEntry != null)
            {
                throw new IOException($"A file with this name already exists: {destinationName}");
            }

            var file = GetFile(sourceDirEntry.Reference);
            destinationDir.AddEntry(file, Utilities.GetFileFromPath(destinationName), FileNameNamespace.Posix);
            destinationDirSelfEntry.Value.UpdateFrom(destinationDir);
        }
    }

    /// <summary>
    /// Gets the number of hard links to a given file or directory.
    /// </summary>
    /// <param name="path">The path of the file or directory.</param>
    /// <returns>The number of hard links.</returns>
    /// <remarks>All files have at least one hard link.</remarks>
    public int GetHardLinkCount(string path)
    {
        using (NtfsTransaction.Begin())
        {
            var dirEntry = GetDirectoryEntry(path)
                ?? throw new FileNotFoundException("File not found", path);

            var file = GetFile(dirEntry.Reference);

            if (file == null)
            {
                return 0;
            }

            if (!_context.Options.HideDosFileNames)
            {
                return file.HardLinkCount;
            }

            var numHardLinks = 0;

            foreach (var fnStream in file.GetStreams(AttributeType.FileName, null))
            {
                var fnr = fnStream.GetContent<FileNameRecord>();
                if (fnr.FileNameNamespace != FileNameNamespace.Dos)
                {
                    ++numHardLinks;
                }
            }

            return numHardLinks;
        }
    }

    /// <summary>
    /// Updates the BIOS Parameter Block (BPB) of the file system to reflect a new disk geometry.
    /// </summary>
    /// <param name="geometry">The disk's new BIOS geometry.</param>
    /// <remarks>Having an accurate geometry in the BPB is essential for booting some Operating Systems (e.g. Windows XP).</remarks>
    public void UpdateBiosGeometry(Geometry geometry)
    {
        _context.BiosParameterBlock.SectorsPerTrack = (ushort)geometry.SectorsPerTrack;
        _context.BiosParameterBlock.NumHeads = (ushort)geometry.HeadsPerCylinder;

        _context.RawStream.Position = 0;
        Span<byte> bpbSector = stackalloc byte[512];
        _context.RawStream.ReadExactly(bpbSector);

        _context.BiosParameterBlock.ToBytes(bpbSector);

        _context.RawStream.Position = 0;
        _context.RawStream.Write(bpbSector);
    }

    internal DirectoryEntry? GetDirectoryEntry(string path)
    {
        return GetDirectoryEntry(GetDirectory(MasterFileTable.RootDirIndex), path);
    }

    /// <summary>
    /// Disposes of this instance.
    /// </summary>
    /// <param name="disposing">Whether called from Dispose or from a finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        if (_context != null && _context.Mft != null)
        {
            if (disposing)
            {
                _context.Mft.Dispose();
            }

            _context.Mft = null;
        }

        // _context is null when the constructor threw (e.g. the journal needed a replay that was refused).
        if (_context?.Options.Compressor is IDisposable disposableCompressor)
        {
            if (disposing)
            {
                disposableCompressor.Dispose();
            }

            _context.Options.Compressor = null;
        }

        base.Dispose(disposing);
    }

    private static void RemoveFileFromDirectory(Directory dir, File file, string name)
    {
        var aliases = GetAliases(dir, file, name).ToArray();

        foreach (var alias in aliases)
        {
            var de = dir.GetEntryByName(alias).Value;
            dir.RemoveEntry(de);
        }
    }

    private static IEnumerable<string> GetAliases(Directory dir, File file, string name)
    {
        var dirEntry = dir.GetEntryByName(name);
        if (dirEntry.Value.Details.FileNameNamespace is FileNameNamespace.Dos
            or FileNameNamespace.Win32)
        {
            foreach (var fnStream in file.GetStreams(AttributeType.FileName, null))
            {
                var fnr = fnStream.GetContent<FileNameRecord>();
                if ((fnr.FileNameNamespace == FileNameNamespace.Win32 ||
                     fnr.FileNameNamespace == FileNameNamespace.Dos)
                    && fnr.ParentDirectory.Value == dir.MftReference.Value)
                {
                    yield return fnr.FileName;
                }
            }
        }
        else
        {
            yield return name;
        }
    }

    private static void SplitPath(string path, out string plainPath, out string attributeName)
    {
        plainPath = path;
        var fileName = Utilities.GetFileFromPath(path);
        attributeName = null;

        var streamSepPos = fileName.IndexOf(':');
        if (streamSepPos >= 0)
        {
            attributeName = fileName[(streamSepPos + 1)..];
            plainPath = plainPath[..(path.Length - (fileName.Length - streamSepPos))];
        }
    }

    private static void UpdateStandardInformation(DirectoryEntry dirEntry, File file,
                                                  StandardInformationModifier modifier)
    {
        // Update the standard information attribute - so it reflects the actual file state
        var stream = file.GetStream(AttributeType.StandardInformation, null).Value;
        var si = stream.GetContent<StandardInformation>();
        modifier(si);
        stream.SetContent(si);

        // Update the directory entry used to open the file, so it's accurate
        dirEntry.UpdateFrom(file);

        // Write attribute changes back to the Master File Table
        file.UpdateRecordInMft();
    }

    private DirectoryEntry CreateNewFile(string path, NewFileOptions options)
    {
        DirectoryEntry result;
        var parentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(path));
        var parentDir = GetDirectory(parentDirEntry.Value.Reference);

        var newFileAttrs = parentDir.StandardInformation.FileAttributes;
        if (options.Compressed.HasValue)
        {
            if (options.Compressed.Value)
            {
                newFileAttrs |= NtfsFileAttributes.Compressed;
            }
            else
            {
                newFileAttrs &= ~NtfsFileAttributes.Compressed;
            }
        }

        var file = File.CreateNew(_context, newFileAttrs);
        try
        {
            result = AddFileToDirectory(file, parentDir, Utilities.GetFileFromPath(path), options);

            var parentSd = DoGetSecurity(parentDir);
            RawSecurityDescriptor newSd;
            if (options.SecurityDescriptor != null)
            {
                newSd = options.SecurityDescriptor;
            }
            else
            {
                newSd = SecurityDescriptor.CalcNewObjectDescriptor(parentSd, false);
            }

            DoSetSecurity(file, newSd);
            result.UpdateFrom(file);

            parentDirEntry.Value.UpdateFrom(parentDir);
        }
        finally
        {
            if (file.HardLinkCount == 0)
            {
                file.Delete();
            }
        }

        return result;
    }

    private DirectoryEntry? GetDirectoryEntry(Directory dir, string path)
    {
        var pathElements = path.Split(Utilities.PathSeparators, StringSplitOptions.RemoveEmptyEntries);
        return GetDirectoryEntry(dir, pathElements, 0);
    }

    private IEnumerable<string> DoSearch(string path, Func<string, bool> filter, bool subFolders, bool dirs, bool files, Func<DirectoryIndexEntry, bool> filterEntry)
    {
        var parentDirEntry = GetDirectoryEntry(path)
            ?? throw new DirectoryNotFoundException($"The directory '{path}' was not found");

        var parentDir = GetDirectory(parentDirEntry.Reference)
            ?? throw new DirectoryNotFoundException($"The directory '{path}' was not found");

        foreach (var de in parentDir.GetAllEntries(filterEntry))
        {
            var isDir = (de.Details.FileAttributes & FileAttributes.Directory) != 0;

            if ((isDir && dirs) || (!isDir && files))
            {
                if (filter is null || filter(de.SearchName))
                {
                    yield return Utilities.CombinePaths(path, de.Details.FileName);
                }
            }

            if (subFolders && isDir && de.Reference.MftIndex != MasterFileTable.RootDirIndex)
            {
                foreach (var subdirentry in DoSearch(Utilities.CombinePaths(path, de.Details.FileName), filter, subFolders, dirs, files, filterEntry))
                {
                    yield return subdirentry;
                }
            }
        }
    }

    private DirectoryEntry? GetDirectoryEntry(Directory dir, string[] pathEntries, int pathOffset)
    {
        if (pathEntries.Length == 0)
        {
            return dir.DirectoryEntry;
        }

        var entry = dir.GetEntryByName(pathEntries[pathOffset]);
        if (entry != null)
        {
            if (pathOffset == pathEntries.Length - 1)
            {
                return entry;
            }

            if ((entry.Value.Details.FileAttributes & FileAttributes.Directory) != 0)
            {
                var subdir = GetDirectory(entry.Value.Reference);

                if (subdir is null)
                {
                    return null;
                }

                return GetDirectoryEntry(subdir, pathEntries, pathOffset + 1);
            }

            throw new IOException($"{pathEntries[pathOffset]} is a file, not a directory");
        }

        return null;
    }

    private DirectoryEntry AddFileToDirectory(File file, Directory dir, string name, NewFileOptions options)
    {
        DirectoryEntry entry;

        bool createShortNames;
        if (options.CreateShortNames.HasValue)
        {
            createShortNames = options.CreateShortNames.Value;
        }
        else
        {
            createShortNames = CreateShortNames;
        }

        if (createShortNames)
        {
            if (Utilities.Is8Dot3(name, ignoreCase: true))
            {
                entry = dir.AddEntry(file, name, FileNameNamespace.Win32AndDos);
            }
            else
            {
                entry = dir.AddEntry(file, name, FileNameNamespace.Win32);
                dir.AddEntry(file, dir.CreateShortName(name), FileNameNamespace.Dos);
            }
        }
        else
        {
            entry = dir.AddEntry(file, name, FileNameNamespace.Posix);
        }

        return entry;
    }

    private void RemoveReparsePoint(File file)
    {
        var stream = file.GetStream(AttributeType.ReparsePoint, null);
        if (stream != null)
        {
            var rp = new ReparsePointRecord();

            using (var contentStream = stream.Value.Open(FileAccess.Read))
            {
                rp.ReadFrom(contentStream, (int)contentStream.Length);
            }

            file.RemoveStream(stream.Value);

            // Update the standard information attribute - so it reflects the actual file state
            var stdInfoStream = file.GetStream(AttributeType.StandardInformation, null).Value;
            var si = stdInfoStream.GetContent<StandardInformation>();
            si.FileAttributes &= ~NtfsFileAttributes.ReparsePoint;
            stdInfoStream.SetContent(si);

            // Remove the reparse point from the index
            _context.ReparsePoints.Remove(rp.Tag, file.MftReference);
        }
    }

    private RawSecurityDescriptor DoGetSecurity(File file)
    {
        var legacyStream = file.GetStream(AttributeType.SecurityDescriptor, null);
        if (legacyStream != null)
        {
            return legacyStream.Value.GetContent<SecurityDescriptor>().Descriptor;
        }

        var si = file.StandardInformation;
        return _context.SecurityDescriptors.GetDescriptorById(si.SecurityId);
    }

    private void DoSetSecurity(File file, RawSecurityDescriptor securityDescriptor)
    {
        var legacyStream = file.GetStream(AttributeType.SecurityDescriptor, null);
        if (legacyStream != null)
        {
            var sd = new SecurityDescriptor
            {
                Descriptor = securityDescriptor
            };
            legacyStream.Value.SetContent(sd);
        }
        else
        {
            var id = _context.SecurityDescriptors.AddDescriptor(securityDescriptor);

            // Update the standard information attribute - so it reflects the actual file state
            var stream = file.GetStream(AttributeType.StandardInformation, null).Value;
            var si = stream.GetContent<StandardInformation>();
            si.SecurityId = id;
            stream.SetContent(si);

            // Write attribute changes back to the Master File Table
            file.UpdateRecordInMft();
        }
    }

    private static void DoRemoveSecurity(File file)
    {
        var legacyStream = file.GetStream(AttributeType.SecurityDescriptor, null);
        if (legacyStream != null)
        {
            file.RemoveStream(legacyStream.Value);
        }
        else
        {
            // Update the standard information attribute - so it reflects the actual file state
            var stream = file.GetStream(AttributeType.StandardInformation, null).Value;
            var si = stream.GetContent<StandardInformation>();
            si.SecurityId = uint.MaxValue;
            stream.SetContent(si);

            // Write attribute changes back to the Master File Table
            file.UpdateRecordInMft();
        }
    }

    private void DumpDirectory(Directory dir, TextWriter writer, string indent)
    {
        foreach (var dirEntry in dir.GetAllEntries(FilterEntry))
        {
            var file = GetFile(dirEntry.Reference);
            writer.WriteLine($"{indent}+-{file} ({file.IndexInMft})");

            // Recurse - but avoid infinite recursion via the root dir...
            if (file is Directory asDir && file.IndexInMft != 5)
            {
                DumpDirectory(asDir, writer, $"{indent}| ");
            }
        }
    }

    private void UpdateStandardInformation(string path, StandardInformationModifier modifier)
    {
        var dirEntry = GetDirectoryEntry(path)
            ?? throw new FileNotFoundException("File not found", path);

        var file = GetFile(dirEntry.Reference);

        UpdateStandardInformation(dirEntry, file, modifier);
    }

    private string ParsePath(string path, out string attributeName, out AttributeType attributeType)
    {
        var fileName = Utilities.GetFileFromPath(path);
        attributeName = null;
        attributeType = AttributeType.Data;

        var fileNameElements = fileName.Split(':', 3);
        fileName = fileNameElements[0];

        if (fileNameElements.Length > 1)
        {
            attributeName = fileNameElements[1];
            if (string.IsNullOrEmpty(attributeName))
            {
                attributeName = null;
            }
        }

        if (fileNameElements.Length > 2)
        {
            var typeName = fileNameElements[2];
            var typeDefn = _context.AttributeDefinitions.Lookup(typeName)
                ?? throw new FileNotFoundException(
                    $"No such attribute type '{typeName}'", path);

            attributeType = typeDefn.Type;
        }

        try
        {
            var dirName = Utilities.GetDirectoryFromPath(path);
            return Utilities.CombinePaths(dirName, fileName);
        }
        catch (ArgumentException)
        {
            throw new IOException($"Invalid path: {path}");
        }
    }

    private delegate void StandardInformationModifier(StandardInformation si);

    #region Internal File access methods (exposed via NtfsContext)

    internal Directory GetDirectory(long index)
    {
        return GetFile(index) as Directory;
    }

    internal Directory GetDirectory(FileRecordReference fileReference)
    {
        return GetFile(fileReference) as Directory;
    }

    internal File GetFile(FileRecordReference fileReference)
    {
        var record = _context.Mft.GetRecord(fileReference);
        if (record == null)
        {
            return null;
        }

        // Don't create file objects for file record segments that are part of another
        // logical file.
        if (record.BaseFile.Value != 0)
        {
            return null;
        }

        var file = _fileCache[fileReference.MftIndex];

        if (file != null && file.MftReference.SequenceNumber != fileReference.SequenceNumber)
        {
            file = null;
        }

        if (file == null)
        {
            if ((record.Flags & FileRecordFlags.IsDirectory) != 0)
            {
                file = new Directory(_context, record);
            }
            else
            {
                file = new File(_context, record);
            }

            _fileCache[fileReference.MftIndex] = file;
        }

        return file;
    }

    internal File GetFile(long index)
    {
        var record = _context.Mft.GetRecord(index, ignoreMagic: false);

        if (record == null)
        {
            return null;
        }

        // Don't create file objects for file record segments that are part of another
        // logical file.
        if (record.BaseFile.Value != 0)
        {
            return null;
        }

        var file = _fileCache[index];

        if (file != null && file.MftReference.SequenceNumber != record.SequenceNumber)
        {
            file = null;
        }

        if (file == null)
        {
            if ((record.Flags & FileRecordFlags.IsDirectory) != 0)
            {
                file = new Directory(_context, record);
            }
            else
            {
                file = new File(_context, record);
            }

            _fileCache[index] = file;
        }

        return file;
    }

    internal File AllocateFile(FileRecordFlags flags)
    {
        File result;
        if ((flags & FileRecordFlags.IsDirectory) != 0)
        {
            result = new Directory(_context, _context.Mft.AllocateRecord(flags, false));
        }
        else
        {
            result = new File(_context, _context.Mft.AllocateRecord(flags, false));
        }

        _fileCache[result.MftReference.MftIndex] = result;
        return result;
    }

    internal void ForgetFile(File file)
    {
        _fileCache.Remove(file.IndexInMft);
    }

    #endregion

    /// <summary>
    /// Size of the Filesystem in bytes
    /// </summary>
    public override long Size => TotalClusters * ClusterSize;

    /// <summary>
    /// Used space of the Filesystem in bytes
    /// </summary>
    public override long UsedSpace => _context.ClusterBitmap.GetUsedClustersCount() * ClusterSize;

    /// <summary>
    /// Available space of the Filesystem in bytes
    /// </summary>
    public override long AvailableSpace => Size - UsedSpace;

    public bool IsClusterInUse(long index) => _context.ClusterBitmap.Bitmap.IsPresent(index);

    public override uint VolumeId => (uint)_context.BiosParameterBlock.VolumeSerialNumber;

    /// <summary>
    /// A plugin system for handling reparse points. Handlers for specific tags can register here
    /// with a delegate that handles such reparse points when they are opened.
    /// </summary>
    internal static ConcurrentDictionary<uint, NtfsReparsePluginHandler> ReparsePlugins => field ??= new()
    {
        [Wof.ReparsePointTagWofCompressed] = Wof.OpenWofReparsePoint
    };

    /// <summary>
    /// A plugin system for handling reparse points. Handlers for specific tags can register in
    /// the <see cref="ReparsePlugins"/> dictionary. The registered delegate will be called when
    /// such reparse points are opened.
    /// </summary>
    internal delegate SparseStream NtfsReparsePluginHandler(File file,
                                                            StandardInformation info,
                                                            NtfsAttribute attr,
                                                            FileAccess access,
                                                            NtfsStream ntfsStream,
                                                            ReparsePointRecord reparsePoint);
}

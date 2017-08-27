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
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class for accessing NTFS file systems.
    /// </summary>
    public sealed class NtfsFileSystem : DiscFileSystem, IClusterBasedFileSystem, IWindowsFileSystem,
                                         IDiagnosticTraceable
    {
        private const FileAttributes NonSettableFileAttributes =
            FileAttributes.Directory | FileAttributes.Offline | FileAttributes.ReparsePoint;

        private readonly NtfsContext _context;

        // Top-level file system structures

        // Working state
        private readonly ObjectCache<long, File> _fileCache;
        private readonly VolumeInformation _volumeInfo;

        /// <summary>
        /// Initializes a new instance of the NtfsFileSystem class.
        /// </summary>
        /// <param name="stream">The stream containing the NTFS file system.</param>
        public NtfsFileSystem(Stream stream)
            : base(new NtfsOptions())
        {
            _context = new NtfsContext();
            _context.RawStream = stream;
            _context.Options = NtfsOptions;

            _context.GetFileByIndex = GetFile;
            _context.GetFileByRef = GetFile;
            _context.GetDirectoryByRef = GetDirectory;
            _context.GetDirectoryByIndex = GetDirectory;
            _context.AllocateFile = AllocateFile;
            _context.ForgetFile = ForgetFile;
            _context.ReadOnly = !stream.CanWrite;

            _fileCache = new ObjectCache<long, File>();

            stream.Position = 0;
            byte[] bytes = StreamUtilities.ReadExact(stream, 512);

            _context.BiosParameterBlock = BiosParameterBlock.FromBytes(bytes, 0);
            if (!IsValidBPB(_context.BiosParameterBlock, stream.Length))
            {
                throw new InvalidFileSystemException("BIOS Parameter Block is invalid for an NTFS file system");
            }

            if (NtfsOptions.ReadCacheEnabled)
            {
                BlockCacheSettings cacheSettings = new BlockCacheSettings();
                cacheSettings.BlockSize = _context.BiosParameterBlock.BytesPerCluster;
                _context.RawStream = new BlockCacheStream(SparseStream.FromStream(stream, Ownership.None),
                    Ownership.None, cacheSettings);
            }

            // Bootstrap the Master File Table
            _context.Mft = new MasterFileTable(_context);
            File mftFile = new File(_context, _context.Mft.GetBootstrapRecord());
            _fileCache[MasterFileTable.MftIndex] = mftFile;
            _context.Mft.Initialize(mftFile);

            // Get volume information (includes version number)
            File volumeInfoFile = GetFile(MasterFileTable.VolumeIndex);
            _volumeInfo =
                volumeInfoFile.GetStream(AttributeType.VolumeInformation, null).GetContent<VolumeInformation>();

            // Initialize access to the other well-known metadata files
            _context.ClusterBitmap = new ClusterBitmap(GetFile(MasterFileTable.BitmapIndex));
            _context.AttributeDefinitions = new AttributeDefinitions(GetFile(MasterFileTable.AttrDefIndex));
            _context.UpperCase = new UpperCase(GetFile(MasterFileTable.UpCaseIndex));

            if (_volumeInfo.Version >= VolumeInformation.VersionW2k)
            {
                _context.SecurityDescriptors = new SecurityDescriptors(GetFile(MasterFileTable.SecureIndex));
                _context.ObjectIds = new ObjectIds(GetFile(GetDirectoryEntry(@"$Extend\$ObjId").Reference));
                _context.ReparsePoints = new ReparsePoints(GetFile(GetDirectoryEntry(@"$Extend\$Reparse").Reference));
                _context.Quotas = new Quotas(GetFile(GetDirectoryEntry(@"$Extend\$Quota").Reference));
            }
#if false
            byte[] buffer = new byte[1024];
            for (int i = 0; i < buffer.Length; ++i)
            {
                buffer[i] = 0xFF;
            }

            using (Stream s = OpenFile("$LogFile", FileMode.Open, FileAccess.ReadWrite))
            {
                while (s.Position != s.Length)
                {
                    s.Write(buffer, 0, (int)Math.Min(buffer.Length, s.Length - s.Position));
                }
            }
#endif
        }

        private bool CreateShortNames
        {
            get
            {
                return _context.Options.ShortNameCreation == ShortFileNameOption.Enabled
                       || (_context.Options.ShortNameCreation == ShortFileNameOption.UseVolumeFlag
                           && (_volumeInfo.Flags & VolumeInformationFlags.DisableShortNameCreation) == 0);
            }
        }

        /// <summary>
        /// Gets the friendly name for the file system.
        /// </summary>
        public override string FriendlyName
        {
            get { return "Microsoft NTFS"; }
        }

        /// <summary>
        /// Gets the options that control how the file system is interpreted.
        /// </summary>
        public NtfsOptions NtfsOptions
        {
            get { return (NtfsOptions)Options; }
        }

        /// <summary>
        /// Gets the volume label.
        /// </summary>
        public override string VolumeLabel
        {
            get
            {
                File volumeFile = GetFile(MasterFileTable.VolumeIndex);
                NtfsStream volNameStream = volumeFile.GetStream(AttributeType.VolumeName, null);
                return volNameStream.GetContent<VolumeName>().Name;
            }
        }

        /// <summary>
        /// Indicates if the file system supports write operations.
        /// </summary>
        public override bool CanWrite
        {
            // For now, we don't...
            get { return !_context.ReadOnly; }
        }

        /// <summary>
        /// Gets the size of each cluster (in bytes).
        /// </summary>
        public long ClusterSize
        {
            get { return _context.BiosParameterBlock.BytesPerCluster; }
        }

        /// <summary>
        /// Gets the total number of clusters managed by the file system.
        /// </summary>
        public long TotalClusters
        {
            get
            {
                return MathUtilities.Ceil(_context.BiosParameterBlock.TotalSectors64,
                    _context.BiosParameterBlock.SectorsPerCluster);
            }
        }

        /// <summary>
        /// Copies an existing file to a new file, allowing overwriting of an existing file.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="destinationFile">The destination file.</param>
        /// <param name="overwrite">Whether to permit over-writing of an existing file.</param>
        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry sourceParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(sourceFile));
                if (sourceParentDirEntry == null || !sourceParentDirEntry.IsDirectory)
                {
                    throw new FileNotFoundException("No such file", sourceFile);
                }

                Directory sourceParentDir = GetDirectory(sourceParentDirEntry.Reference);

                DirectoryEntry sourceEntry = sourceParentDir.GetEntryByName(Utilities.GetFileFromPath(sourceFile));
                if (sourceEntry == null || sourceEntry.IsDirectory)
                {
                    throw new FileNotFoundException("No such file", sourceFile);
                }

                File origFile = GetFile(sourceEntry.Reference);

                DirectoryEntry destParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(destinationFile));
                if (destParentDirEntry == null || !destParentDirEntry.IsDirectory)
                {
                    throw new FileNotFoundException("Destination directory not found", destinationFile);
                }

                Directory destParentDir = GetDirectory(destParentDirEntry.Reference);

                DirectoryEntry destDirEntry = destParentDir.GetEntryByName(Utilities.GetFileFromPath(destinationFile));
                if (destDirEntry != null && !destDirEntry.IsDirectory)
                {
                    if (overwrite)
                    {
                        if (destDirEntry.Reference.MftIndex == sourceEntry.Reference.MftIndex)
                        {
                            throw new IOException("Destination file already exists and is the source file");
                        }

                        File oldFile = GetFile(destDirEntry.Reference);
                        destParentDir.RemoveEntry(destDirEntry);
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

                File newFile = File.CreateNew(_context, destParentDir.StandardInformation.FileAttributes);
                foreach (NtfsStream origStream in origFile.AllStreams)
                {
                    NtfsStream newStream = newFile.GetStream(origStream.AttributeType, origStream.Name);

                    switch (origStream.AttributeType)
                    {
                        case AttributeType.Data:
                            if (newStream == null)
                            {
                                newStream = newFile.CreateStream(origStream.AttributeType, origStream.Name);
                            }

                            using (SparseStream s = origStream.Open(FileAccess.Read))
                            using (SparseStream d = newStream.Open(FileAccess.Write))
                            {
                                byte[] buffer = new byte[64 * Sizes.OneKiB];
                                int numRead;

                                do
                                {
                                    numRead = s.Read(buffer, 0, buffer.Length);
                                    d.Write(buffer, 0, numRead);
                                } while (numRead != 0);
                            }

                            break;

                        case AttributeType.StandardInformation:
                            StandardInformation newSi = origStream.GetContent<StandardInformation>();
                            newStream.SetContent(newSi);
                            break;
                    }
                }

                AddFileToDirectory(newFile, destParentDir, Utilities.GetFileFromPath(destinationFile), null);
                destParentDirEntry.UpdateFrom(destParentDir);
            }
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        /// <param name="path">The path of the new directory.</param>
        public override void CreateDirectory(string path)
        {
            CreateDirectory(path, null);
        }

        /// <summary>
        /// Deletes a directory.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public override void DeleteDirectory(string path)
        {
            using (new NtfsTransaction())
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new IOException("Unable to delete root directory");
                }

                string parent = Utilities.GetDirectoryFromPath(path);

                DirectoryEntry parentDirEntry = GetDirectoryEntry(parent);
                if (parentDirEntry == null || !parentDirEntry.IsDirectory)
                {
                    throw new DirectoryNotFoundException("No such directory: " + path);
                }

                Directory parentDir = GetDirectory(parentDirEntry.Reference);

                DirectoryEntry dirEntry = parentDir.GetEntryByName(Utilities.GetFileFromPath(path));
                if (dirEntry == null || !dirEntry.IsDirectory)
                {
                    throw new DirectoryNotFoundException("No such directory: " + path);
                }

                Directory dir = GetDirectory(dirEntry.Reference);

                if (!dir.IsEmpty)
                {
                    throw new IOException("Unable to delete non-empty directory");
                }

                if ((dirEntry.Details.FileAttributes & FileAttributes.ReparsePoint) != 0)
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
        /// Deletes a file.
        /// </summary>
        /// <param name="path">The path of the file to delete.</param>
        public override void DeleteFile(string path)
        {
            using (new NtfsTransaction())
            {
                string attributeName;
                AttributeType attributeType;
                string dirEntryPath = ParsePath(path, out attributeName, out attributeType);

                string parentDirPath = Utilities.GetDirectoryFromPath(dirEntryPath);

                DirectoryEntry parentDirEntry = GetDirectoryEntry(parentDirPath);
                if (parentDirEntry == null || !parentDirEntry.IsDirectory)
                {
                    throw new FileNotFoundException("No such file", path);
                }

                Directory parentDir = GetDirectory(parentDirEntry.Reference);

                DirectoryEntry dirEntry = parentDir.GetEntryByName(Utilities.GetFileFromPath(dirEntryPath));
                if (dirEntry == null || dirEntry.IsDirectory)
                {
                    throw new FileNotFoundException("No such file", path);
                }

                File file = GetFile(dirEntry.Reference);

                if (string.IsNullOrEmpty(attributeName) && attributeType == AttributeType.Data)
                {
                    if ((dirEntry.Details.FileAttributes & FileAttributes.ReparsePoint) != 0)
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
                    NtfsStream attrStream = file.GetStream(attributeType, attributeName);
                    if (attrStream == null)
                    {
                        throw new FileNotFoundException("No such attribute: " + attributeName, path);
                    }
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
            using (new NtfsTransaction())
            {
                // Special case - root directory
                if (string.IsNullOrEmpty(path))
                {
                    return true;
                }
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                return dirEntry != null && (dirEntry.Details.FileAttributes & FileAttributes.Directory) != 0;
            }
        }

        /// <summary>
        /// Indicates if a file exists.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns>true if the file exists.</returns>
        public override bool FileExists(string path)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                return dirEntry != null && (dirEntry.Details.FileAttributes & FileAttributes.Directory) == 0;
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
        public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            using (new NtfsTransaction())
            {
                Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

                List<string> dirs = new List<string>();
                DoSearch(dirs, path, re, searchOption == SearchOption.AllDirectories, true, false);
                return dirs.ToArray();
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
        public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            using (new NtfsTransaction())
            {
                Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

                List<string> results = new List<string>();
                DoSearch(results, path, re, searchOption == SearchOption.AllDirectories, false, true);
                return results.ToArray();
            }
        }

        /// <summary>
        /// Gets the names of all files and subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files and subdirectories.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry parentDirEntry = GetDirectoryEntry(path);
                if (parentDirEntry == null)
                {
                    throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture,
                        "The directory '{0}' does not exist", path));
                }

                Directory parentDir = GetDirectory(parentDirEntry.Reference);

                return Utilities.Map(parentDir.GetAllEntries(true),
                    m => Utilities.CombinePaths(path, m.Details.FileName));
            }
        }

        /// <summary>
        /// Gets the names of files and subdirectories in a specified directory matching a specified
        /// search pattern.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path, string searchPattern)
        {
            using (new NtfsTransaction())
            {
                // TODO: Be smarter, use the B*Tree for better performance when the start of the pattern is known
                // characters
                Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

                DirectoryEntry parentDirEntry = GetDirectoryEntry(path);
                if (parentDirEntry == null)
                {
                    throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture,
                        "The directory '{0}' does not exist", path));
                }

                Directory parentDir = GetDirectory(parentDirEntry.Reference);

                List<string> result = new List<string>();
                foreach (DirectoryEntry dirEntry in parentDir.GetAllEntries(true))
                {
                    if (re.IsMatch(dirEntry.Details.FileName))
                    {
                        result.Add(Utilities.CombinePaths(path, dirEntry.Details.FileName));
                    }
                }

                return result.ToArray();
            }
        }

        /// <summary>
        /// Moves a directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The directory to move.</param>
        /// <param name="destinationDirectoryName">The target directory name.</param>
        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            using (new NtfsTransaction())
            {
                using (new NtfsTransaction())
                {
                    DirectoryEntry sourceParentDirEntry =
                        GetDirectoryEntry(Utilities.GetDirectoryFromPath(sourceDirectoryName));
                    if (sourceParentDirEntry == null || !sourceParentDirEntry.IsDirectory)
                    {
                        throw new DirectoryNotFoundException("No such directory: " + sourceDirectoryName);
                    }

                    Directory sourceParentDir = GetDirectory(sourceParentDirEntry.Reference);

                    DirectoryEntry sourceEntry =
                        sourceParentDir.GetEntryByName(Utilities.GetFileFromPath(sourceDirectoryName));
                    if (sourceEntry == null || !sourceEntry.IsDirectory)
                    {
                        throw new DirectoryNotFoundException("No such directory: " + sourceDirectoryName);
                    }

                    File file = GetFile(sourceEntry.Reference);

                    DirectoryEntry destParentDirEntry =
                        GetDirectoryEntry(Utilities.GetDirectoryFromPath(destinationDirectoryName));
                    if (destParentDirEntry == null || !destParentDirEntry.IsDirectory)
                    {
                        throw new DirectoryNotFoundException("Destination directory not found: " +
                                                             destinationDirectoryName);
                    }

                    Directory destParentDir = GetDirectory(destParentDirEntry.Reference);

                    DirectoryEntry destDirEntry =
                        destParentDir.GetEntryByName(Utilities.GetFileFromPath(destinationDirectoryName));
                    if (destDirEntry != null)
                    {
                        throw new IOException("Destination directory already exists");
                    }

                    RemoveFileFromDirectory(sourceParentDir, file, sourceEntry.Details.FileName);
                    AddFileToDirectory(file, destParentDir, Utilities.GetFileFromPath(destinationDirectoryName), null);
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
            using (new NtfsTransaction())
            {
                DirectoryEntry sourceParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(sourceName));
                if (sourceParentDirEntry == null || !sourceParentDirEntry.IsDirectory)
                {
                    throw new FileNotFoundException("No such file", sourceName);
                }

                Directory sourceParentDir = GetDirectory(sourceParentDirEntry.Reference);

                DirectoryEntry sourceEntry = sourceParentDir.GetEntryByName(Utilities.GetFileFromPath(sourceName));
                if (sourceEntry == null || sourceEntry.IsDirectory)
                {
                    throw new FileNotFoundException("No such file", sourceName);
                }

                File file = GetFile(sourceEntry.Reference);

                DirectoryEntry destParentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(destinationName));
                if (destParentDirEntry == null || !destParentDirEntry.IsDirectory)
                {
                    throw new FileNotFoundException("Destination directory not found", destinationName);
                }

                Directory destParentDir = GetDirectory(destParentDirEntry.Reference);

                DirectoryEntry destDirEntry = destParentDir.GetEntryByName(Utilities.GetFileFromPath(destinationName));
                if (destDirEntry != null && !destDirEntry.IsDirectory)
                {
                    if (overwrite)
                    {
                        if (destDirEntry.Reference.MftIndex == sourceEntry.Reference.MftIndex)
                        {
                            throw new IOException("Destination file already exists and is the source file");
                        }

                        File oldFile = GetFile(destDirEntry.Reference);
                        destParentDir.RemoveEntry(destDirEntry);
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

                RemoveFileFromDirectory(sourceParentDir, file, sourceEntry.Details.FileName);
                AddFileToDirectory(file, destParentDir, Utilities.GetFileFromPath(destinationName), null);
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
            return OpenFile(path, mode, access, null);
        }

        /// <summary>
        /// Gets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect.</param>
        /// <returns>The attributes of the file or directory.</returns>
        public override FileAttributes GetAttributes(string path)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
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
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }

                FileAttributes oldValue = dirEntry.Details.FileAttributes;
                FileAttributes changedAttribs = oldValue ^ newValue;

                if (changedAttribs == 0)
                {
                    // Abort - nothing changed
                    return;
                }

                if ((changedAttribs & NonSettableFileAttributes) != 0)
                {
                    throw new ArgumentException("Attempt to change attributes that are read-only", nameof(newValue));
                }

                File file = GetFile(dirEntry.Reference);

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
                    NtfsAttribute ntfsAttr = file.GetAttribute(AttributeType.Data, null);
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
                    NtfsAttribute ntfsAttr = file.GetAttribute(AttributeType.Data, null);
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
                    delegate(StandardInformation si) { si.FileAttributes = FileNameRecord.SetAttributes(newValue, si.FileAttributes); });
            }
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
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
            using (new NtfsTransaction())
            {
                UpdateStandardInformation(path, delegate(StandardInformation si) { si.CreationTime = newTime; });
            }
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last access time.</returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
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
            using (new NtfsTransaction())
            {
                UpdateStandardInformation(path, delegate(StandardInformation si) { si.LastAccessTime = newTime; });
            }
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last write time.</returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
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
            using (new NtfsTransaction())
            {
                UpdateStandardInformation(path, delegate(StandardInformation si) { si.ModificationTime = newTime; });
            }
        }

        /// <summary>
        /// Gets the length of a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The length in bytes.</returns>
        public override long GetFileLength(string path)
        {
            using (new NtfsTransaction())
            {
                string attributeName;
                AttributeType attributeType;
                string dirEntryPath = ParsePath(path, out attributeName, out attributeType);

                DirectoryEntry dirEntry = GetDirectoryEntry(dirEntryPath);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }

                // Ordinary file length request, use info from directory entry for efficiency - if allowed
                if (NtfsOptions.FileLengthFromDirectoryEntries && attributeName == null &&
                    attributeType == AttributeType.Data)
                {
                    return (long)dirEntry.Details.RealSize;
                }

                // Alternate stream / attribute, pull info from attribute record
                File file = GetFile(dirEntry.Reference);
                NtfsAttribute attr = file.GetAttribute(attributeType, attributeName);
                if (attr == null)
                {
                    throw new FileNotFoundException(string.Format(CultureInfo.InvariantCulture,
                        "No such attribute '{0}({1})'", attributeName, attributeType));
                }

                return attr.Length;
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
        public Range<long, long>[] PathToClusters(string path)
        {
            string plainPath;
            string attributeName;
            SplitPath(path, out plainPath, out attributeName);

            DirectoryEntry dirEntry = GetDirectoryEntry(plainPath);
            if (dirEntry == null || dirEntry.IsDirectory)
            {
                throw new FileNotFoundException("No such file", path);
            }

            File file = GetFile(dirEntry.Reference);

            NtfsStream stream = file.GetStream(AttributeType.Data, attributeName);
            if (stream == null)
            {
                throw new FileNotFoundException(
                    string.Format(CultureInfo.InvariantCulture, "File does not contain '{0}' data attribute",
                        attributeName), path);
            }

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
        public StreamExtent[] PathToExtents(string path)
        {
            string plainPath;
            string attributeName;
            SplitPath(path, out plainPath, out attributeName);

            DirectoryEntry dirEntry = GetDirectoryEntry(plainPath);
            if (dirEntry == null || dirEntry.IsDirectory)
            {
                throw new FileNotFoundException("No such file", path);
            }

            File file = GetFile(dirEntry.Reference);

            NtfsStream stream = file.GetStream(AttributeType.Data, attributeName);
            if (stream == null)
            {
                throw new FileNotFoundException(
                    string.Format(CultureInfo.InvariantCulture, "File does not contain '{0}' data attribute",
                        attributeName), path);
            }

            return stream.GetAbsoluteExtents();
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
            using (Stream s = OpenFile(@"\$Boot", FileMode.Open))
            {
                return StreamUtilities.ReadExact(s, (int)s.Length);
            }
        }

        /// <summary>
        /// Writes a diagnostic dump of key NTFS structures.
        /// </summary>
        /// <param name="writer">The writer to receive the dump.</param>
        /// <param name="linePrefix">The indent to apply to the start of each line of output.</param>
        public void Dump(TextWriter writer, string linePrefix)
        {
            writer.WriteLine(linePrefix + "NTFS File System Dump");
            writer.WriteLine(linePrefix + "=====================");

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
            writer.WriteLine(linePrefix + "FULL FILE LISTING");
            foreach (FileRecord record in _context.Mft.Records)
            {
                // Don't go through cache - these are short-lived, and this is (just!) diagnostics
                File f = new File(_context, record);
                f.Dump(writer, linePrefix);

                foreach (NtfsStream stream in f.AllStreams)
                {
                    if (stream.AttributeType == AttributeType.IndexRoot)
                    {
                        try
                        {
                            writer.WriteLine(linePrefix + "  INDEX (" + stream.Name + ")");
                            f.GetIndex(stream.Name).Dump(writer, linePrefix + "    ");
                        }
                        catch (Exception e)
                        {
                            writer.WriteLine(linePrefix + "!Exception: " + e);
                        }
                    }
                }
            }

            writer.WriteLine(linePrefix);
            writer.WriteLine(linePrefix + "DIRECTORY TREE");
            writer.WriteLine(linePrefix + @"\ (5)");
            DumpDirectory(GetDirectory(MasterFileTable.RootDirIndex), writer, linePrefix); // 5 = Root Dir
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
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
                File file = GetFile(dirEntry.Reference);
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
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
                File file = GetFile(dirEntry.Reference);
                DoSetSecurity(file, securityDescriptor);

                // Update the directory entry used to open the file
                dirEntry.UpdateFrom(file);
            }
        }

        /// <summary>
        /// Sets the reparse point data on a file or directory.
        /// </summary>
        /// <param name="path">The file to set the reparse point on.</param>
        /// <param name="reparsePoint">The new reparse point.</param>
        public void SetReparsePoint(string path, ReparsePoint reparsePoint)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
                File file = GetFile(dirEntry.Reference);

                NtfsStream stream = file.GetStream(AttributeType.ReparsePoint, null);
                if (stream != null)
                {
                    // If there's an existing reparse point, unhook it.
                    using (Stream contentStream = stream.Open(FileAccess.Read))
                    {
                        byte[] oldRpBuffer = StreamUtilities.ReadExact(contentStream, (int)contentStream.Length);
                        ReparsePointRecord rp = new ReparsePointRecord();
                        rp.ReadFrom(oldRpBuffer, 0);
                        _context.ReparsePoints.Remove(rp.Tag, dirEntry.Reference);
                    }
                }
                else
                {
                    stream = file.CreateStream(AttributeType.ReparsePoint, null);
                }

                // Set the new content
                ReparsePointRecord newRp = new ReparsePointRecord();
                newRp.Tag = (uint)reparsePoint.Tag;
                newRp.Content = reparsePoint.Content;

                byte[] contentBuffer = new byte[newRp.Size];
                newRp.WriteTo(contentBuffer, 0);
                using (Stream contentStream = stream.Open(FileAccess.ReadWrite))
                {
                    contentStream.Write(contentBuffer, 0, contentBuffer.Length);
                    contentStream.SetLength(contentBuffer.Length);
                }

                // Update the standard information attribute - so it reflects the actual file state
                NtfsStream stdInfoStream = file.GetStream(AttributeType.StandardInformation, null);
                StandardInformation si = stdInfoStream.GetContent<StandardInformation>();
                si.FileAttributes = si.FileAttributes | FileAttributeFlags.ReparsePoint;
                stdInfoStream.SetContent(si);

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
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
                File file = GetFile(dirEntry.Reference);

                NtfsStream stream = file.GetStream(AttributeType.ReparsePoint, null);
                if (stream != null)
                {
                    ReparsePointRecord rp = new ReparsePointRecord();

                    using (Stream contentStream = stream.Open(FileAccess.Read))
                    {
                        byte[] buffer = StreamUtilities.ReadExact(contentStream, (int)contentStream.Length);
                        rp.ReadFrom(buffer, 0);
                        return new ReparsePoint((int)rp.Tag, rp.Content);
                    }
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
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
                File file = GetFile(dirEntry.Reference);
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
            using (new NtfsTransaction())
            {
                string parentPath = Utilities.GetDirectoryFromPath(path);
                DirectoryEntry parentEntry = GetDirectoryEntry(parentPath);
                if (parentEntry == null || (parentEntry.Details.FileAttributes & FileAttributes.Directory) == 0)
                {
                    throw new DirectoryNotFoundException("Parent directory not found");
                }

                Directory dir = GetDirectory(parentEntry.Reference);
                if (dir == null)
                {
                    throw new DirectoryNotFoundException("Parent directory not found");
                }

                DirectoryEntry givenEntry = dir.GetEntryByName(Utilities.GetFileFromPath(path));
                if (givenEntry == null)
                {
                    throw new FileNotFoundException("Path not found", path);
                }

                if (givenEntry.Details.FileNameNamespace == FileNameNamespace.Dos)
                {
                    return givenEntry.Details.FileName;
                }
                if (givenEntry.Details.FileNameNamespace == FileNameNamespace.Win32)
                {
                    File file = GetFile(givenEntry.Reference);

                    foreach (NtfsStream stream in file.GetStreams(AttributeType.FileName, null))
                    {
                        FileNameRecord fnr = stream.GetContent<FileNameRecord>();
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
            if (!Utilities.Is8Dot3(shortName))
            {
                throw new ArgumentException("Short name is not a valid 8.3 file name", nameof(shortName));
            }

            using (new NtfsTransaction())
            {
                string parentPath = Utilities.GetDirectoryFromPath(path);
                DirectoryEntry parentEntry = GetDirectoryEntry(parentPath);
                if (parentEntry == null || (parentEntry.Details.FileAttributes & FileAttributes.Directory) == 0)
                {
                    throw new DirectoryNotFoundException("Parent directory not found");
                }

                Directory dir = GetDirectory(parentEntry.Reference);
                if (dir == null)
                {
                    throw new DirectoryNotFoundException("Parent directory not found");
                }

                DirectoryEntry givenEntry = dir.GetEntryByName(Utilities.GetFileFromPath(path));
                if (givenEntry == null)
                {
                    throw new FileNotFoundException("Path not found", path);
                }

                FileNameNamespace givenNamespace = givenEntry.Details.FileNameNamespace;
                File file = GetFile(givenEntry.Reference);

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
                List<NtfsStream> nameStreams = new List<NtfsStream>(file.GetStreams(AttributeType.FileName, null));
                foreach (NtfsStream stream in nameStreams)
                {
                    FileNameRecord fnr = stream.GetContent<FileNameRecord>();
                    if (fnr.ParentDirectory.Equals(givenEntry.Details.ParentDirectory)
                        && fnr.FileNameNamespace == FileNameNamespace.Dos)
                    {
                        DirectoryEntry oldEntry = dir.GetEntryByName(fnr.FileName);
                        dir.RemoveEntry(oldEntry);
                    }
                }

                dir.AddEntry(file, shortName, FileNameNamespace.Dos);

                parentEntry.UpdateFrom(dir);
            }
        }

        /// <summary>
        /// Gets the standard file information for a file.
        /// </summary>
        /// <param name="path">The full path to the file or directory to query.</param>
        /// <returns>The standard file information.</returns>
        public WindowsFileInformation GetFileStandardInformation(string path)
        {
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }

                File file = GetFile(dirEntry.Reference);
                StandardInformation si = file.StandardInformation;

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
            using (new NtfsTransaction())
            {
                UpdateStandardInformation(
                    path,
                    delegate(StandardInformation si)
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
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }
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
        public string[] GetAlternateDataStreams(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }

            File file = GetFile(dirEntry.Reference);

            List<string> names = new List<string>();
            foreach (NtfsStream attr in file.AllStreams)
            {
                if (attr.AttributeType == AttributeType.Data && !string.IsNullOrEmpty(attr.Name))
                {
                    names.Add(attr.Name);
                }
            }

            return names.ToArray();
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
            NtfsFormatter formatter = new NtfsFormatter();
            formatter.Label = label;
            formatter.DiskGeometry = diskGeometry;
            formatter.FirstSector = firstSector;
            formatter.SectorCount = sectorCount;
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
            NtfsFormatter formatter = new NtfsFormatter();
            formatter.Label = label;
            formatter.DiskGeometry = diskGeometry;
            formatter.FirstSector = firstSector;
            formatter.SectorCount = sectorCount;
            formatter.BootCode = bootCode;
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
            NtfsFormatter formatter = new NtfsFormatter();
            formatter.Label = label;
            formatter.DiskGeometry = diskGeometry;
            formatter.FirstSector = firstSector;
            formatter.SectorCount = sectorCount;
            formatter.BootCode = options.BootCode;
            formatter.ComputerAccount = options.ComputerAccount;
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
            NtfsFormatter formatter = new NtfsFormatter();
            formatter.Label = label;
            formatter.DiskGeometry = volume.BiosGeometry ?? Geometry.Null;
            formatter.FirstSector = volume.PhysicalStartSector;
            formatter.SectorCount = volume.Length / Sizes.Sector;
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
            NtfsFormatter formatter = new NtfsFormatter();
            formatter.Label = label;
            formatter.DiskGeometry = volume.BiosGeometry ?? Geometry.Null;
            formatter.FirstSector = volume.PhysicalStartSector;
            formatter.SectorCount = volume.Length / Sizes.Sector;
            formatter.BootCode = bootCode;
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
            NtfsFormatter formatter = new NtfsFormatter();
            formatter.Label = label;
            formatter.DiskGeometry = volume.BiosGeometry ?? Geometry.Null;
            formatter.FirstSector = volume.PhysicalStartSector;
            formatter.SectorCount = volume.Length / Sizes.Sector;
            formatter.BootCode = options.BootCode;
            formatter.ComputerAccount = options.ComputerAccount;
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
            byte[] bytes = StreamUtilities.ReadExact(stream, 512);
            BiosParameterBlock bpb = BiosParameterBlock.FromBytes(bytes, 0);

            return IsValidBPB(bpb, stream.Length);
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
            using (new NtfsTransaction())
            {
                string[] pathElements = path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

                Directory focusDir = GetDirectory(MasterFileTable.RootDirIndex);
                DirectoryEntry focusDirEntry = focusDir.DirectoryEntry;

                for (int i = 0; i < pathElements.Length; ++i)
                {
                    DirectoryEntry childDirEntry = focusDir.GetEntryByName(pathElements[i]);
                    if (childDirEntry == null)
                    {
                        FileAttributeFlags newDirAttrs = focusDir.StandardInformation.FileAttributes;
                        if (options != null && options.Compressed.HasValue)
                        {
                            if (options.Compressed.Value)
                            {
                                newDirAttrs |= FileAttributeFlags.Compressed;
                            }
                            else
                            {
                                newDirAttrs &= ~FileAttributeFlags.Compressed;
                            }
                        }

                        Directory childDir = Directory.CreateNew(_context, newDirAttrs);
                        try
                        {
                            childDirEntry = AddFileToDirectory(childDir, focusDir, pathElements[i], options);

                            RawSecurityDescriptor parentSd = DoGetSecurity(focusDir);
                            RawSecurityDescriptor newSd;
                            if (options != null && options.SecurityDescriptor != null)
                            {
                                newSd = options.SecurityDescriptor;
                            }
                            else
                            {
                                newSd = SecurityDescriptor.CalcNewObjectDescriptor(parentSd, false);
                            }

                            DoSetSecurity(childDir, newSd);
                            childDirEntry.UpdateFrom(childDir);

                            // Update the directory entry by which we found the directory we've just modified
                            focusDirEntry.UpdateFrom(focusDir);

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
                        focusDir = GetDirectory(childDirEntry.Reference);
                    }

                    focusDirEntry = childDirEntry;
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
            using (new NtfsTransaction())
            {
                string attributeName;
                AttributeType attributeType;
                string dirEntryPath = ParsePath(path, out attributeName, out attributeType);

                DirectoryEntry entry = GetDirectoryEntry(dirEntryPath);
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

                if ((entry.Details.FileAttributes & FileAttributes.Directory) != 0 &&
                    attributeType == AttributeType.Data)
                {
                    throw new IOException("Attempt to open directory as a file");
                }
                File file = GetFile(entry.Reference);
                NtfsStream ntfsStream = file.GetStream(attributeType, attributeName);

                if (ntfsStream == null)
                {
                    if (mode == FileMode.Create || mode == FileMode.OpenOrCreate)
                    {
                        ntfsStream = file.CreateStream(attributeType, attributeName);
                    }
                    else
                    {
                        throw new FileNotFoundException("No such attribute on file", path);
                    }
                }

                SparseStream stream = new NtfsFileStream(this, entry, attributeType, attributeName, access);

                if (mode == FileMode.Create || mode == FileMode.Truncate)
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
            using (new NtfsTransaction())
            {
                DirectoryEntry entry = GetDirectoryEntry(file);
                if (entry == null)
                {
                    throw new FileNotFoundException("No such file", file);
                }

                File fileObj = GetFile(entry.Reference);
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
            using (new NtfsTransaction())
            {
                DirectoryEntry sourceDirEntry = GetDirectoryEntry(sourceName);
                if (sourceDirEntry == null)
                {
                    throw new FileNotFoundException("Source file not found", sourceName);
                }

                string destinationDirName = Utilities.GetDirectoryFromPath(destinationName);
                DirectoryEntry destinationDirSelfEntry = GetDirectoryEntry(destinationDirName);
                if (destinationDirSelfEntry == null ||
                    (destinationDirSelfEntry.Details.FileAttributes & FileAttributes.Directory) == 0)
                {
                    throw new FileNotFoundException("Destination directory not found", destinationDirName);
                }

                Directory destinationDir = GetDirectory(destinationDirSelfEntry.Reference);
                if (destinationDir == null)
                {
                    throw new FileNotFoundException("Destination directory not found", destinationDirName);
                }

                DirectoryEntry destinationDirEntry = GetDirectoryEntry(destinationDir,
                    Utilities.GetFileFromPath(destinationName));
                if (destinationDirEntry != null)
                {
                    throw new IOException("A file with this name already exists: " + destinationName);
                }

                File file = GetFile(sourceDirEntry.Reference);
                destinationDir.AddEntry(file, Utilities.GetFileFromPath(destinationName), FileNameNamespace.Posix);
                destinationDirSelfEntry.UpdateFrom(destinationDir);
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
            using (new NtfsTransaction())
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                if (dirEntry == null)
                {
                    throw new FileNotFoundException("File not found", path);
                }

                File file = GetFile(dirEntry.Reference);

                if (!_context.Options.HideDosFileNames)
                {
                    return file.HardLinkCount;
                }
                int numHardLinks = 0;

                foreach (NtfsStream fnStream in file.GetStreams(AttributeType.FileName, null))
                {
                    FileNameRecord fnr = fnStream.GetContent<FileNameRecord>();
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
            byte[] bpbSector = StreamUtilities.ReadExact(_context.RawStream, 512);

            _context.BiosParameterBlock.ToBytes(bpbSector, 0);

            _context.RawStream.Position = 0;
            _context.RawStream.Write(bpbSector, 0, bpbSector.Length);
        }

        internal DirectoryEntry GetDirectoryEntry(string path)
        {
            return GetDirectoryEntry(GetDirectory(MasterFileTable.RootDirIndex), path);
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing">Whether called from Dispose or from a finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_context != null && _context.Mft != null)
                {
                    _context.Mft.Dispose();
                    _context.Mft = null;
                }

                IDisposable disposableCompressor = _context.Options.Compressor as IDisposable;
                if (disposableCompressor != null)
                {
                    disposableCompressor.Dispose();
                    _context.Options.Compressor = null;
                }
            }

            base.Dispose(disposing);
        }

        private static bool IsValidBPB(BiosParameterBlock bpb, long volumeSize)
        {
            if (bpb.SignatureByte != 0x80 || bpb.TotalSectors16 != 0 || bpb.TotalSectors32 != 0
                || bpb.TotalSectors64 == 0 || bpb.MftRecordSize == 0 || bpb.MftCluster == 0 || bpb.BytesPerSector == 0)
            {
                return false;
            }

            long mftPos = bpb.MftCluster * bpb.SectorsPerCluster * bpb.BytesPerSector;
            return mftPos < bpb.TotalSectors64 * bpb.BytesPerSector && mftPos < volumeSize;
        }

        private static void RemoveFileFromDirectory(Directory dir, File file, string name)
        {
            List<string> aliases = new List<string>();

            DirectoryEntry dirEntry = dir.GetEntryByName(name);
            if (dirEntry.Details.FileNameNamespace == FileNameNamespace.Dos
                || dirEntry.Details.FileNameNamespace == FileNameNamespace.Win32)
            {
                foreach (NtfsStream fnStream in file.GetStreams(AttributeType.FileName, null))
                {
                    FileNameRecord fnr = fnStream.GetContent<FileNameRecord>();
                    if ((fnr.FileNameNamespace == FileNameNamespace.Win32 ||
                         fnr.FileNameNamespace == FileNameNamespace.Dos)
                        && fnr.ParentDirectory.Value == dir.MftReference.Value)
                    {
                        aliases.Add(fnr.FileName);
                    }
                }
            }
            else
            {
                aliases.Add(name);
            }

            foreach (string alias in aliases)
            {
                DirectoryEntry de = dir.GetEntryByName(alias);
                dir.RemoveEntry(de);
            }
        }

        private static void SplitPath(string path, out string plainPath, out string attributeName)
        {
            plainPath = path;
            string fileName = Utilities.GetFileFromPath(path);
            attributeName = null;

            int streamSepPos = fileName.IndexOf(':');
            if (streamSepPos >= 0)
            {
                attributeName = fileName.Substring(streamSepPos + 1);
                plainPath = plainPath.Substring(0, path.Length - (fileName.Length - streamSepPos));
            }
        }

        private static void UpdateStandardInformation(DirectoryEntry dirEntry, File file,
                                                      StandardInformationModifier modifier)
        {
            // Update the standard information attribute - so it reflects the actual file state
            NtfsStream stream = file.GetStream(AttributeType.StandardInformation, null);
            StandardInformation si = stream.GetContent<StandardInformation>();
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
            DirectoryEntry parentDirEntry = GetDirectoryEntry(Utilities.GetDirectoryFromPath(path));
            Directory parentDir = GetDirectory(parentDirEntry.Reference);

            FileAttributeFlags newFileAttrs = parentDir.StandardInformation.FileAttributes;
            if (options != null && options.Compressed.HasValue)
            {
                if (options.Compressed.Value)
                {
                    newFileAttrs |= FileAttributeFlags.Compressed;
                }
                else
                {
                    newFileAttrs &= ~FileAttributeFlags.Compressed;
                }
            }

            File file = File.CreateNew(_context, newFileAttrs);
            try
            {
                result = AddFileToDirectory(file, parentDir, Utilities.GetFileFromPath(path), options);

                RawSecurityDescriptor parentSd = DoGetSecurity(parentDir);
                RawSecurityDescriptor newSd;
                if (options != null && options.SecurityDescriptor != null)
                {
                    newSd = options.SecurityDescriptor;
                }
                else
                {
                    newSd = SecurityDescriptor.CalcNewObjectDescriptor(parentSd, false);
                }

                DoSetSecurity(file, newSd);
                result.UpdateFrom(file);

                parentDirEntry.UpdateFrom(parentDir);
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

        private DirectoryEntry GetDirectoryEntry(Directory dir, string path)
        {
            string[] pathElements = path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return GetDirectoryEntry(dir, pathElements, 0);
        }

        private void DoSearch(List<string> results, string path, Regex regex, bool subFolders, bool dirs, bool files)
        {
            DirectoryEntry parentDirEntry = GetDirectoryEntry(path);
            if (parentDirEntry == null)
            {
                throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture,
                    "The directory '{0}' was not found", path));
            }

            Directory parentDir = GetDirectory(parentDirEntry.Reference);
            if (parentDir == null)
            {
                throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture,
                    "The directory '{0}' was not found", path));
            }

            foreach (DirectoryEntry de in parentDir.GetAllEntries(true))
            {
                bool isDir = (de.Details.FileAttributes & FileAttributes.Directory) != 0;

                if ((isDir && dirs) || (!isDir && files))
                {
                    if (regex.IsMatch(de.SearchName))
                    {
                        results.Add(Utilities.CombinePaths(path, de.Details.FileName));
                    }
                }

                if (subFolders && isDir)
                {
                    DoSearch(results, Utilities.CombinePaths(path, de.Details.FileName), regex, subFolders, dirs, files);
                }
            }
        }

        private DirectoryEntry GetDirectoryEntry(Directory dir, string[] pathEntries, int pathOffset)
        {
            DirectoryEntry entry;

            if (pathEntries.Length == 0)
            {
                return dir.DirectoryEntry;
            }
            entry = dir.GetEntryByName(pathEntries[pathOffset]);
            if (entry != null)
            {
                if (pathOffset == pathEntries.Length - 1)
                {
                    return entry;
                }
                if ((entry.Details.FileAttributes & FileAttributes.Directory) != 0)
                {
                    return GetDirectoryEntry(GetDirectory(entry.Reference), pathEntries, pathOffset + 1);
                }
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "{0} is a file, not a directory", pathEntries[pathOffset]));
            }
            return null;
        }

        private DirectoryEntry AddFileToDirectory(File file, Directory dir, string name, NewFileOptions options)
        {
            DirectoryEntry entry;

            bool createShortNames;
            if (options != null && options.CreateShortNames.HasValue)
            {
                createShortNames = options.CreateShortNames.Value;
            }
            else
            {
                createShortNames = CreateShortNames;
            }

            if (createShortNames)
            {
                if (Utilities.Is8Dot3(name.ToUpperInvariant()))
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
            NtfsStream stream = file.GetStream(AttributeType.ReparsePoint, null);
            if (stream != null)
            {
                ReparsePointRecord rp = new ReparsePointRecord();

                using (Stream contentStream = stream.Open(FileAccess.Read))
                {
                    byte[] buffer = StreamUtilities.ReadExact(contentStream, (int)contentStream.Length);
                    rp.ReadFrom(buffer, 0);
                }

                file.RemoveStream(stream);

                // Update the standard information attribute - so it reflects the actual file state
                NtfsStream stdInfoStream = file.GetStream(AttributeType.StandardInformation, null);
                StandardInformation si = stdInfoStream.GetContent<StandardInformation>();
                si.FileAttributes = si.FileAttributes & ~FileAttributeFlags.ReparsePoint;
                stdInfoStream.SetContent(si);

                // Remove the reparse point from the index
                _context.ReparsePoints.Remove(rp.Tag, file.MftReference);
            }
        }

        private RawSecurityDescriptor DoGetSecurity(File file)
        {
            NtfsStream legacyStream = file.GetStream(AttributeType.SecurityDescriptor, null);
            if (legacyStream != null)
            {
                return legacyStream.GetContent<SecurityDescriptor>().Descriptor;
            }

            StandardInformation si = file.StandardInformation;
            return _context.SecurityDescriptors.GetDescriptorById(si.SecurityId);
        }

        private void DoSetSecurity(File file, RawSecurityDescriptor securityDescriptor)
        {
            NtfsStream legacyStream = file.GetStream(AttributeType.SecurityDescriptor, null);
            if (legacyStream != null)
            {
                SecurityDescriptor sd = new SecurityDescriptor();
                sd.Descriptor = securityDescriptor;
                legacyStream.SetContent(sd);
            }
            else
            {
                uint id = _context.SecurityDescriptors.AddDescriptor(securityDescriptor);

                // Update the standard information attribute - so it reflects the actual file state
                NtfsStream stream = file.GetStream(AttributeType.StandardInformation, null);
                StandardInformation si = stream.GetContent<StandardInformation>();
                si.SecurityId = id;
                stream.SetContent(si);

                // Write attribute changes back to the Master File Table
                file.UpdateRecordInMft();
            }
        }

        private void DumpDirectory(Directory dir, TextWriter writer, string indent)
        {
            foreach (DirectoryEntry dirEntry in dir.GetAllEntries(true))
            {
                File file = GetFile(dirEntry.Reference);
                Directory asDir = file as Directory;
                writer.WriteLine(indent + "+-" + file + " (" + file.IndexInMft + ")");

                // Recurse - but avoid infinite recursion via the root dir...
                if (asDir != null && file.IndexInMft != 5)
                {
                    DumpDirectory(asDir, writer, indent + "| ");
                }
            }
        }

        private void UpdateStandardInformation(string path, StandardInformationModifier modifier)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            File file = GetFile(dirEntry.Reference);

            UpdateStandardInformation(dirEntry, file, modifier);
        }

        private string ParsePath(string path, out string attributeName, out AttributeType attributeType)
        {
            string fileName = Utilities.GetFileFromPath(path);
            attributeName = null;
            attributeType = AttributeType.Data;

            string[] fileNameElements = fileName.Split(new[] { ':' }, 3);
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
                string typeName = fileNameElements[2];
                AttributeDefinitionRecord typeDefn = _context.AttributeDefinitions.Lookup(typeName);
                if (typeDefn == null)
                {
                    throw new FileNotFoundException(
                        string.Format(CultureInfo.InvariantCulture, "No such attribute type '{0}'", typeName), path);
                }

                attributeType = typeDefn.Type;
            }

            try
            {
                string dirName = Utilities.GetDirectoryFromPath(path);
                return Utilities.CombinePaths(dirName, fileName);
            }
            catch (ArgumentException)
            {
                throw new IOException("Invalid path: " + path);
            }
        }

        private delegate void StandardInformationModifier(StandardInformation si);

        #region Internal File access methods (exposed via NtfsContext)

        internal Directory GetDirectory(long index)
        {
            return (Directory)GetFile(index);
        }

        internal Directory GetDirectory(FileRecordReference fileReference)
        {
            return (Directory)GetFile(fileReference);
        }

        internal File GetFile(FileRecordReference fileReference)
        {
            FileRecord record = _context.Mft.GetRecord(fileReference);
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

            File file = _fileCache[fileReference.MftIndex];

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
            FileRecord record = _context.Mft.GetRecord(index, false);
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

            File file = _fileCache[index];

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
            File result = null;
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
        public override long Size
        {
            get { return TotalClusters* ClusterSize;  }
        }
 
        /// <summary>
        /// Used space of the Filesystem in bytes
        /// </summary>
        public override long UsedSpace
        {
            get
            {
                long usedCluster = 0;
                var bitmap = _context.ClusterBitmap.Bitmap;
                var processed = 0L;
                while (processed < bitmap.Size)
                {
                    byte[] buffer = new byte[4*Sizes.OneKiB];
                    var count = bitmap.GetBytes(processed, buffer, 0, buffer.Length);
                    usedCluster += BitCounter.Count(buffer, 0, count);
                    processed += count;
                }
                return (usedCluster* ClusterSize);
            }
        }
 
        /// <summary>
        /// Available space of the Filesystem in bytes
        /// </summary>
        public override long AvailableSpace { get { return Size - UsedSpace; } }
    }
}

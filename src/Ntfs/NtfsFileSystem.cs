//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class for accessing NTFS file systems.
    /// </summary>
    public sealed class NtfsFileSystem : ClusterBasedFileSystem, IDiagnosticTraceable
    {
        private const FileAttributes NonSettableFileAttributes = FileAttributes.Directory | FileAttributes.NotContentIndexed | FileAttributes.Offline | FileAttributes.ReparsePoint | FileAttributes.Temporary;

        private NtfsContext _context;

        // Top-level file system structures


        // Working state
        private ObjectCache<long, File> _fileCache;

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the NTFS file system</param>
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
            _context.DeleteFile = DeleteFile;
            _context.ReadOnly = !stream.CanWrite;

            _fileCache = new ObjectCache<long, File>();

            stream.Position = 0;
            byte[] bytes = Utilities.ReadFully(stream, 512);


            _context.BiosParameterBlock = BiosParameterBlock.FromBytes(bytes, 0);

            // Bootstrap the Master File Table
            _context.Mft = new MasterFileTable();
            File mftFile = new File(_context, MasterFileTable.GetBootstrapRecord(stream, _context.BiosParameterBlock));
            _fileCache[MasterFileTable.MftIndex] = mftFile;
            _context.Mft.Initialize(mftFile);

            // Initialize access to the other well-known metadata files
            _context.ClusterBitmap = new ClusterBitmap(GetFile(MasterFileTable.BitmapIndex));
            _context.AttributeDefinitions = new AttributeDefinitions(GetFile(MasterFileTable.AttrDefIndex));
            _context.UpperCase = new UpperCase(GetFile(MasterFileTable.UpCaseIndex));
            _context.SecurityDescriptors = new SecurityDescriptors(GetFile(MasterFileTable.SecureIndex));
            _context.ObjectIds = new ObjectIds(GetFile(GetDirectoryEntry(@"$Extend\$ObjId").Reference));

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

        /// <summary>
        /// Gets the options that control how the file system is interpreted.
        /// </summary>
        public NtfsOptions NtfsOptions
        {
            get { return (NtfsOptions)Options; }
        }

        /// <summary>
        /// Opens the Master File Table as a raw stream.
        /// </summary>
        /// <returns></returns>
        public Stream OpenMasterFileTable()
        {
            return OpenRawAttribute("$MFT", AttributeType.Data, null, FileAccess.Read);
        }

        #region DiscFileSystem Implementation
        /// <summary>
        /// Gets the friendly name for the file system.
        /// </summary>
        public override string FriendlyName
        {
            get { return "Microsoft NTFS"; }
        }

        /// <summary>
        /// Indicates if the file system supports write operations.
        /// </summary>
        public override bool CanWrite
        {
            // For now, we don't...
            get { return false; }
        }

        /// <summary>
        /// Copies an existing file to a new file, allowing overwriting of an existing file.
        /// </summary>
        /// <param name="sourceFile">The source file</param>
        /// <param name="destinationFile">The destination file</param>
        /// <param name="overwrite">Whether to permit over-writing of an existing file.</param>
        public override void CopyFile(string sourceFile, string destinationFile, bool overwrite)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a directory.
        /// </summary>
        /// <param name="path">The path of the new directory</param>
        public override void CreateDirectory(string path)
        {
            string[] pathElements = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            Directory focusDir = GetDirectory(MasterFileTable.RootDirIndex);
            DirectoryEntry focusDirEntry = focusDir.DirectoryEntry;

            for (int i = 0; i < pathElements.Length; ++i)
            {
                DirectoryEntry childDirEntry = focusDir.GetEntryByName(pathElements[i]);
                if (childDirEntry == null)
                {
                    Directory childDir = Directory.CreateNew(_context);
                    childDirEntry = focusDir.AddEntry(childDir, pathElements[i]);

                    // Update the directory entry by which we found the directory we've just modified
                    focusDirEntry.UpdateFrom(focusDir);

                    focusDir = childDir;
                }
                else
                {
                    focusDir = GetDirectory(childDirEntry.Reference);
                }

                focusDirEntry = childDirEntry;
            }
        }

        /// <summary>
        /// Deletes a directory.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public override void DeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new IOException("Unable to delete root directory");
            }

            string parent = Path.GetDirectoryName(path);

            DirectoryEntry parentDirEntry = GetDirectoryEntry(parent);
            if (parentDirEntry == null || (parentDirEntry.Details.Flags & FileAttributeFlags.Directory) == 0)
            {
                throw new DirectoryNotFoundException("No such directory: " + path);
            }
            Directory parentDir = GetDirectory(parentDirEntry.Reference);

            DirectoryEntry dirEntry = parentDir.GetEntryByName(Path.GetFileName(path));
            if ((dirEntry.Details.Flags & FileAttributeFlags.Directory) == 0)
            {
                throw new DirectoryNotFoundException("No such directory: " + path);
            }

            FileReference fileRef = dirEntry.Reference;
            Directory dir = GetDirectory(fileRef);

            if (!dir.IsEmpty)
            {
                throw new IOException("Unable to delete non-empty directory");
            }

            parentDir.RemoveEntry(dirEntry);
            dir.Delete();
        }

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="path">The path of the file to delete.</param>
        public override void DeleteFile(string path)
        {
            string parent = Path.GetDirectoryName(path);

            DirectoryEntry parentDirEntry = GetDirectoryEntry(parent);
            if (parentDirEntry == null || (parentDirEntry.Details.Flags & FileAttributeFlags.Directory) == 0)
            {
                throw new FileNotFoundException("No such file", path);
            }
            Directory parentDir = GetDirectory(parentDirEntry.Reference);

            DirectoryEntry dirEntry = parentDir.GetEntryByName(Path.GetFileName(path));
            if (dirEntry == null || (dirEntry.Details.FileAttributes & FileAttributes.Directory) != 0)
            {
                throw new FileNotFoundException("No such file", path);
            }

            File file = GetFile(dirEntry.Reference);

            parentDir.RemoveEntry(dirEntry);
            file.Delete();
        }

        /// <summary>
        /// Indicates if a directory exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the directory exists</returns>
        public override bool DirectoryExists(string path)
        {
            // Special case - root directory
            if (String.IsNullOrEmpty(path))
            {
                return true;
            }
            else
            {
                DirectoryEntry dirEntry = GetDirectoryEntry(path);
                return (dirEntry != null && (dirEntry.Details.FileAttributes & FileAttributes.Directory) != 0);
            }
        }

        /// <summary>
        /// Indicates if a file exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the file exists</returns>
        public override bool FileExists(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            return (dirEntry != null && (dirEntry.Details.FileAttributes & FileAttributes.Directory) == 0);
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
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> dirs = new List<string>();
            DoSearch(dirs, path, re, searchOption == SearchOption.AllDirectories, true, false);
            return dirs.ToArray();
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
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            List<string> results = new List<string>();
            DoSearch(results, path, re, searchOption == SearchOption.AllDirectories, false, true);
            return results.ToArray();
        }

        /// <summary>
        /// Gets the names of all files and subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files and subdirectories.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            DirectoryEntry parentDirEntry = GetDirectoryEntry(path);
            if (parentDirEntry == null)
            {
                throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture, "The directory '{0}' does not exist", path));
            }

            Directory parentDir = GetDirectory(parentDirEntry.Reference);

            return Utilities.Map<DirectoryEntry, string>(parentDir.GetAllEntries(), (m) => Utilities.CombinePaths(path, m.Details.FileName));
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
            // TODO: Be smarter, use the B*Tree for better performance when the start of the pattern is known
            // characters
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            DirectoryEntry parentDirEntry = GetDirectoryEntry(path);
            if (parentDirEntry == null)
            {
                throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture, "The directory '{0}' does not exist", path));
            }

            Directory parentDir = GetDirectory(parentDirEntry.Reference);

            List<string> result = new List<string>();
            foreach (DirectoryEntry dirEntry in parentDir.GetAllEntries())
            {
                if (re.IsMatch(dirEntry.Details.FileName))
                {
                    result.Add(Path.Combine(path, dirEntry.Details.FileName));
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Moves a directory.
        /// </summary>
        /// <param name="sourceDirectoryName">The directory to move.</param>
        /// <param name="destinationDirectoryName">The target directory name.</param>
        public override void MoveDirectory(string sourceDirectoryName, string destinationDirectoryName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Moves a file, allowing an existing file to be overwritten.
        /// </summary>
        /// <param name="sourceName">The file to move.</param>
        /// <param name="destinationName">The target file name.</param>
        /// <param name="overwrite">Whether to permit a destination file to be overwritten</param>
        public override void MoveFile(string sourceName, string destinationName, bool overwrite)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <param name="access">The access permissions for the created stream.</param>
        /// <returns>The new stream.</returns>
        public override Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            string fileName = Utilities.GetFileFromPath(path);
            string attributeName = null;

            int streamSepPos = fileName.IndexOf(':');
            if (streamSepPos >= 0)
            {
                attributeName = fileName.Substring(streamSepPos + 1);
            }

            DirectoryEntry entry = GetDirectoryEntry(Path.Combine(Path.GetDirectoryName(path),fileName));
            if (entry == null)
            {
                if (mode == FileMode.Open)
                {
                    throw new FileNotFoundException("No such file", path);
                }
                else
                {
                    File file = File.CreateNew(_context, FileRecordFlags.None);

                    DirectoryEntry parentDirEntry = GetDirectoryEntry(Path.GetDirectoryName(path));
                    Directory parentDir = GetDirectory(parentDirEntry.Reference);
                    entry = parentDir.AddEntry(file, Path.GetFileName(path));
                    parentDirEntry.UpdateFrom(parentDir);
                }
            }
            else if (mode == FileMode.CreateNew)
            {
                throw new IOException("File already exists");
            }


            if ((entry.Details.FileAttributes & FileAttributes.Directory) != 0)
            {
                throw new IOException("Attempt to open directory as a file");
            }
            else
            {
                File file = GetFile(entry.Reference);
                NtfsAttribute attr = file.GetAttribute(AttributeType.Data, attributeName);

                if (attr == null)
                {
                    if (mode == FileMode.Create || mode == FileMode.OpenOrCreate)
                    {
                        file.CreateAttribute(AttributeType.Data, attributeName);
                    }
                    else
                    {
                        throw new FileNotFoundException("No such attribute on file", path);
                    }
                }

                SparseStream stream = new NtfsFileStream(this, entry, AttributeType.Data, attributeName, access);

                if (mode == FileMode.Create || mode == FileMode.Truncate)
                {
                    stream.SetLength(0);
                }

                return stream;
            }
        }

        /// <summary>
        /// Opens an existing attribute.
        /// </summary>
        /// <param name="file">The file containing the attribute</param>
        /// <param name="type">The type of the attribute</param>
        /// <param name="name">The name of the attribute</param>
        /// <param name="access">The desired access to the attribute</param>
        /// <returns>A stream with read access to the attribute</returns>
        public Stream OpenRawAttribute(string file, AttributeType type, string name, FileAccess access)
        {
            DirectoryEntry entry = GetDirectoryEntry(file);
            if (entry == null)
            {
                throw new FileNotFoundException("No such file", file);
            }

            File fileObj = GetFile(entry.Reference);
            return fileObj.OpenAttribute(type, name, access);
        }

        /// <summary>
        /// Gets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect</param>
        /// <returns>The attributes of the file or directory</returns>
        public override FileAttributes GetAttributes(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else
            {
                return dirEntry.Details.FileAttributes;
            }
        }

        /// <summary>
        /// Sets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to change</param>
        /// <param name="newValue">The new attributes of the file or directory</param>
        public override void SetAttributes(string path, FileAttributes newValue)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else if((dirEntry.Details.FileAttributes & NonSettableFileAttributes) != (newValue & NonSettableFileAttributes))
            {
                throw new ArgumentException("Attempt to change attributes that are read-only");
            }

            UpdateStandardInformation(path, delegate(StandardInformation si) { si.FileAttributes = FileNameRecord.SetAttributes(newValue, si.FileAttributes); });
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else
            {
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
            UpdateStandardInformation(path, delegate(StandardInformation si) { si.CreationTime = newTime; });
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last access time</returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else
            {
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
            UpdateStandardInformation(path, delegate(StandardInformation si) { si.LastAccessTime = newTime; });
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The last write time</returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else
            {
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
            UpdateStandardInformation(path, delegate(StandardInformation si) { si.ModificationTime = newTime; });
        }

        /// <summary>
        /// Gets the length of a file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>The length in bytes</returns>
        public override long GetFileLength(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            return (long)dirEntry.Details.RealSize;
        }
        #endregion

        #region Cluster Information
        /// <summary>
        /// Gets the size of each cluster (in bytes).
        /// </summary>
        public override long ClusterSize
        {
            get { return _context.BiosParameterBlock.BytesPerCluster; }
        }

        /// <summary>
        /// Gets the total number of clusters managed by the file system.
        /// </summary>
        public override long TotalClusters
        {
            get { return Utilities.Ceil(_context.BiosParameterBlock.TotalSectors64, _context.BiosParameterBlock.SectorsPerCluster); }
        }

        /// <summary>
        /// Converts a file name to the list of clusters occupied by the file's data.
        /// </summary>
        /// <param name="path">The path to inspect</param>
        /// <returns>The clusters</returns>
        /// <remarks>Note that in some file systems, small files may not have dedicated
        /// clusters.  Only dedicated clusters will be returned.</remarks>
        public override Range<long, long>[] PathToClusters(string path)
        {
            string plainPath;
            string attributeName;
            SplitPath(path, out plainPath, out attributeName);


            DirectoryEntry dirEntry = GetDirectoryEntry(plainPath);
            File file = GetFile(dirEntry.Reference);

            NtfsAttribute attr = file.GetAttribute(AttributeType.Data, attributeName);
            return attr.GetClusters();
        }

        /// <summary>
        /// Converts a file name to the extents containing its data.
        /// </summary>
        /// <param name="path">The path to inspect</param>
        /// <returns>The file extents, as absolute byte positions in the underlying stream</returns>
        /// <remarks>Use this method with caution - NTFS supports encrypted, sparse and compressed files
        /// where bytes are not directly stored in extents.  This method
        /// merely indicates where file data is stored, not what's stored.</remarks>
        public override StreamExtent[] PathToExtents(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an object that can convert between clusters and files.
        /// </summary>
        /// <returns>The cluster map</returns>
        public override ClusterMap BuildClusterMap()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region NTFS-specific public methods

        /// <summary>
        /// Creates an NTFS hard link to an existing file.
        /// </summary>
        /// <param name="sourceName">An existing name of the file.</param>
        /// <param name="destinationName">The name of the new hard link to the file.</param>
        public void CreateHardLink(string sourceName, string destinationName)
        {
            DirectoryEntry sourceDirEntry = GetDirectoryEntry(sourceName);
            if (sourceDirEntry == null)
            {
                throw new FileNotFoundException("Source file not found", sourceName);
            }

            string destinationDirName = Path.GetDirectoryName(destinationName);
            DirectoryEntry destinationDirSelfEntry = GetDirectoryEntry(destinationDirName);
            if (destinationDirSelfEntry == null || (destinationDirSelfEntry.Details.FileAttributes & FileAttributes.Directory) == 0)
            {
                throw new FileNotFoundException("Destination directory not found", destinationDirName);
            }

            Directory destinationDir = GetDirectory(destinationDirSelfEntry.Reference);
            if (destinationDir == null)
            {
                throw new FileNotFoundException("Destination directory not found", destinationDirName);
            }

            DirectoryEntry destinationDirEntry = GetDirectoryEntry(destinationDir, Path.GetFileName(destinationName));
            if (destinationDirEntry != null)
            {
                throw new IOException("A file with this name already exists: " + destinationName);
            }

            File file = GetFile(sourceDirEntry.Reference);
            destinationDir.AddEntry(file, Path.GetFileName(destinationName));
            destinationDirSelfEntry.UpdateFrom(destinationDir);
        }

        /// <summary>
        /// Gets the security descriptor associated with the file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect.</param>
        /// <returns>The security descriptor.</returns>
        public FileSystemSecurity GetAccessControl(string path)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else
            {
                File file = GetFile(dirEntry.Reference);

                NtfsAttribute legacyAttr = file.GetAttribute(AttributeType.SecurityDescriptor);
                if (legacyAttr != null)
                {
                    return ((StructuredNtfsAttribute<SecurityDescriptor>)legacyAttr).Content.Descriptor;
                }

                StandardInformation si = file.GetAttributeContent<StandardInformation>(AttributeType.StandardInformation);
                return _context.SecurityDescriptors.GetDescriptorById(si.SecurityId);
            }
        }

        #endregion

        #region Internal File access methods (exposed via NtfsContext)
        internal Directory GetDirectory(long index)
        {
            return (Directory)GetFile(index);
        }

        internal Directory GetDirectory(FileReference fileReference)
        {
            return (Directory)GetFile(fileReference);
        }

        internal File GetFile(FileReference fileReference)
        {
            FileRecord record = _context.Mft.GetRecord(fileReference);
            if (record == null)
            {
                return null;
            }

            File file = _fileCache[fileReference.MftIndex];

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
                result = new Directory(_context, _context.Mft.AllocateRecord(FileRecordFlags.IsDirectory));
            }
            else
            {
                result = new File(_context, _context.Mft.AllocateRecord(FileRecordFlags.None));
            }
            _fileCache[result.MftReference.MftIndex] = result;
            return result;
        }

        internal void DeleteFile(File file)
        {
            if (file.HardLinkCount != 0)
            {
                throw new InvalidOperationException("Attempt to delete an in-use file");
            }

            _context.Mft.RemoveRecord(file.MftReference);
            _fileCache.Remove(file.IndexInMft);
        }

        #endregion

        /// <summary>
        /// Writes a diagnostic dump of key NTFS structures.
        /// </summary>
        /// <param name="writer">The writer to receive the dump.</param>
        /// <param name="linePrefix">The indent to apply to the start of each line of output.</param>
        public void Dump(TextWriter writer, string linePrefix)
        {
            writer.WriteLine(linePrefix + "NTFS File System Dump");
            writer.WriteLine(linePrefix + "=====================");

            //_context.Mft.Dump(writer, linePrefix);

            writer.WriteLine(linePrefix);
            _context.SecurityDescriptors.Dump(writer, linePrefix);

            writer.WriteLine(linePrefix);
            _context.ObjectIds.Dump(writer, linePrefix);

            writer.WriteLine(linePrefix);
            GetDirectory(MasterFileTable.RootDirIndex).Dump(writer, linePrefix);

            writer.WriteLine(linePrefix);
            writer.WriteLine(linePrefix + "FULL FILE LISTING");
            foreach (var record in _context.Mft.Records)
            {
                // Don't go through cache - these are short-lived, and this is (just!) diagnostics
                File f = new File(_context, record);
                f.Dump(writer, linePrefix);

                foreach (var attr in f.AllAttributes)
                {
                    if (attr.Record.AttributeType == AttributeType.IndexRoot)
                    {
                        writer.WriteLine(linePrefix + "  INDEX (" + attr.Name + ")");
                        f.GetIndex(attr.Name).Dump(writer, linePrefix + "    ");
                    }
                }
            }

            writer.WriteLine(linePrefix);
            writer.WriteLine(linePrefix + "DIRECTORY TREE");
            writer.WriteLine(linePrefix + @"\ (5)");
            DumpDirectory(GetDirectory(MasterFileTable.RootDirIndex), writer, linePrefix);  // 5 = Root Dir
        }

        internal DirectoryEntry GetDirectoryEntry(string path)
        {
            return GetDirectoryEntry(GetDirectory(MasterFileTable.RootDirIndex), path);
        }

        private DirectoryEntry GetDirectoryEntry(Directory dir, string path)
        {
            string[] pathElements = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return GetDirectoryEntry(dir, pathElements, 0);
        }

        private void DoSearch(List<string> results, string path, Regex regex, bool subFolders, bool dirs, bool files)
        {
            DirectoryEntry parentDirEntry = GetDirectoryEntry(path);
            Directory parentDir = GetDirectory(parentDirEntry.Reference);

            foreach (DirectoryEntry de in parentDir.GetAllEntries())
            {
                bool isDir = ((de.Details.FileAttributes & FileAttributes.Directory) != 0);

                if ((isDir && dirs) || (!isDir && files))
                {
                    if (regex.IsMatch(de.SearchName))
                    {
                        results.Add(Path.Combine(path, de.Details.FileName));
                    }
                }

                if (subFolders && isDir)
                {
                    DoSearch(results, Path.Combine(path, de.Details.FileName), regex, subFolders, dirs, files);
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
            else
            {
                entry = dir.GetEntryByName(pathEntries[pathOffset]);
                if (entry != null)
                {
                    if (pathOffset == pathEntries.Length - 1)
                    {
                        return entry;
                    }
                    else if ((entry.Details.FileAttributes & FileAttributes.Directory) != 0)
                    {
                        return GetDirectoryEntry(GetDirectory(entry.Reference), pathEntries, pathOffset + 1);
                    }
                    else
                    {
                        throw new IOException(string.Format(CultureInfo.InvariantCulture, "{0} is a file, not a directory", pathEntries[pathOffset]));
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        private void DumpDirectory(Directory dir, TextWriter writer, string indent)
        {
            foreach (DirectoryEntry dirEntry in dir.GetAllEntries())
            {
                File file = GetFile(dirEntry.Reference);
                Directory asDir = file as Directory;
                writer.WriteLine(indent + "+-" + file.ToString() + " (" + file.IndexInMft + ")");

                // Recurse - but avoid infinite recursion via the root dir...
                if (asDir != null && file.IndexInMft != 5)
                {
                    DumpDirectory(asDir, writer, indent + "| ");
                }
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

        private delegate void StandardInformationModifier(StandardInformation si);

        private void UpdateStandardInformation(string path, StandardInformationModifier modifier)
        {
            DirectoryEntry dirEntry = GetDirectoryEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", path);
            }
            else
            {
                File file = GetFile(dirEntry.Reference);

                // Update the standard information attribute - so it reflects the actual file state
                StructuredNtfsAttribute<StandardInformation> saAttr = (StructuredNtfsAttribute<StandardInformation>)file.GetAttribute(AttributeType.StandardInformation);
                modifier(saAttr.Content);
                saAttr.Save();

                // Update the directory entry used to open the file, so it's accurate
                dirEntry.UpdateFrom(file);

                // Write attribute changes back to the Master File Table
                file.UpdateRecordInMft();
            }
        }

    }
}

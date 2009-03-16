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
using System.Text.RegularExpressions;

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class for accessing NTFS file systems.
    /// </summary>
    public sealed class NtfsFileSystem : ReadOnlyDiscFileSystem
    {
        private NtfsOptions _options;

        private Stream _stream;

        private BiosParameterBlock _bpb;

        private MasterFileTable _mft;

        private ClusterBitmap _bitmap;

        private AttributeDefinitions _attrDefs;

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the NTFS file system</param>
        public NtfsFileSystem(Stream stream)
        {
            _options = new NtfsOptions();

            _stream = stream;


            _stream.Position = 0;
            byte[] bytes = Utilities.ReadFully(_stream, 512);

            _bpb = BiosParameterBlock.FromBytes(bytes, 0);

            _stream.Position = _bpb.MftCluster * _bpb.SectorsPerCluster * _bpb.BytesPerSector;
            byte[] mftSelfRecordData = Utilities.ReadFully(_stream, _bpb.MftRecordSize * _bpb.SectorsPerCluster * _bpb.BytesPerSector);
            FileRecord mftSelfRecord = new FileRecord(_bpb.BytesPerSector);
            mftSelfRecord.FromBytes(mftSelfRecordData, 0);

            _mft = new MasterFileTable(this, mftSelfRecord);
            _bitmap = new ClusterBitmap(this, _mft.GetRecord(MasterFileTable.BitmapIndex));
            _attrDefs = new AttributeDefinitions(this, _mft.GetRecord(MasterFileTable.AttrDefIndex));
        }

        /// <summary>
        /// Gets the options that control how the file system is interpreted.
        /// </summary>
        public NtfsOptions Options
        {
            get { return _options; }
        }

        /// <summary>
        /// Opens the Master File Table as a raw stream.
        /// </summary>
        /// <returns></returns>
        public Stream OpenMasterFileTable()
        {
            return _mft.OpenAttribute(AttributeType.Data, FileAccess.Read);
        }

        /// <summary>
        /// Gets the friendly name for the file system.
        /// </summary>
        public override string FriendlyName
        {
            get { return "Microsoft NTFS"; }
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

            Directory parentDir = _mft.GetDirectory(parentDirEntry.Reference);

            return Utilities.Map<DirectoryEntry, string>(parentDir.GetMembers(), (m) => Utilities.CombinePaths(path, m.Details.FileName));
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

            Directory parentDir = _mft.GetDirectory(parentDirEntry.Reference);

            List<string> result = new List<string>();
            foreach (DirectoryEntry dirEntry in parentDir.GetMembers())
            {
                if (re.IsMatch(dirEntry.Details.FileName))
                {
                    result.Add(Path.Combine(path, dirEntry.Details.FileName));
                }
            }
            return result.ToArray();
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
            if (mode != FileMode.Open)
            {
                throw new NotSupportedException("Can only open existing files");
            }

            DirectoryEntry entry = GetDirectoryEntry(path);

            if (entry == null)
            {
                throw new FileNotFoundException("No such file", path);
            }

            if ((entry.Details.FileAttributes & FileAttributes.Directory) != 0)
            {
                throw new IOException("Attempt to open directory as a file");
            }
            else
            {
                string fileName = Utilities.GetFileFromPath(path);
                string attributeName = null;

                int streamSepPos = fileName.IndexOf(':');
                if (streamSepPos >= 0)
                {
                    attributeName = fileName.Substring(streamSepPos + 1);
                }

                return new NtfsFileStream(this, new AttributeReference(entry.Reference, attributeName, AttributeType.Data), access);
            }
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

        internal Stream RawStream
        {
            get { return _stream; }
        }

        internal BiosParameterBlock BiosParameterBlock
        {
            get { return _bpb; }
        }

        internal MasterFileTable MasterFileTable
        {
            get { return _mft; }
        }

        internal ClusterBitmap ClusterBitmap
        {
            get { return _bitmap; }
        }

        internal AttributeDefinitions AttributeDefinitions
        {
            get { return _attrDefs; }
        }

        internal long BytesPerCluster
        {
            get { return _bpb.BytesPerSector * _bpb.SectorsPerCluster; }
        }

        /// <summary>
        /// Writes a diagnostic dump of key NTFS structures.
        /// </summary>
        /// <param name="writer">The writer to receive the dump.</param>
        public void Dump(TextWriter writer)
        {
            writer.WriteLine("NTFS File System Dump");
            writer.WriteLine("=====================");

            _mft.Dump(writer, "");

            writer.WriteLine();
            writer.WriteLine("DIRECTORY TREE");
            writer.WriteLine(@"\ (5)");
            DumpDirectory(_mft.GetDirectory(MasterFileTable.RootDirIndex), writer, "");  // 5 = Root Dir
        }

        internal DirectoryEntry GetDirectoryEntry(string path)
        {
            return GetDirectoryEntry(_mft.GetDirectory(MasterFileTable.RootDirIndex), path);
        }

        private DirectoryEntry GetDirectoryEntry(Directory dir, string path)
        {
            string[] pathElements = path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return GetDirectoryEntry(dir, pathElements, 0);
        }

        private void DoSearch(List<string> results, string path, Regex regex, bool subFolders, bool dirs, bool files)
        {
            DirectoryEntry parentDirEntry = GetDirectoryEntry(path);
            Directory parentDir = _mft.GetDirectory(parentDirEntry.Reference);

            foreach (DirectoryEntry de in parentDir.GetMembers())
            {
                bool isDir = ((de.Details.FileAttributes & FileAttributes.Directory) != 0);

                if ((isDir && dirs) || (!isDir && files))
                {
                    if (regex.IsMatch(de.Details.FileName))
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
                entry = dir.FindEntryByName(pathEntries[pathOffset]);
                if (entry != null)
                {
                    if (pathOffset == pathEntries.Length - 1)
                    {
                        return entry;
                    }
                    else if ((entry.Details.FileAttributes & FileAttributes.Directory) != 0)
                    {
                        return GetDirectoryEntry(_mft.GetDirectory(entry.Reference), pathEntries, pathOffset + 1);
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
            foreach (DirectoryEntry dirEntry in dir.GetMembers())
            {
                File file = _mft.GetFileOrDirectory(dirEntry.Reference);
                Directory asDir = file as Directory;
                writer.WriteLine(indent + "+-" + file.ToString() + " (" + file.IndexInMft + ")");

                // Recurse - but avoid infinite recursion via the root dir...
                if (asDir != null && file.IndexInMft != 5)
                {
                    DumpDirectory(asDir, writer, indent + "| ");
                }
            }
        }
    }
}

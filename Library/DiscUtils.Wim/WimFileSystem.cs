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

namespace DiscUtils.Wim
{
    /// <summary>
    /// Provides access to the file system within a WIM file image.
    /// </summary>
    public class WimFileSystem : ReadOnlyDiscFileSystem, IWindowsFileSystem
    {
        private readonly ObjectCache<long, List<DirectoryEntry>> _dirCache;
        private WimFile _file;
        private Stream _metaDataStream;
        private long _rootDirPos;
        private List<RawSecurityDescriptor> _securityDescriptors;

        internal WimFileSystem(WimFile file, int index)
        {
            _file = file;

            ShortResourceHeader metaDataFileInfo = _file.LocateImage(index);
            if (metaDataFileInfo == null)
            {
                throw new ArgumentException("No such image: " + index, nameof(index));
            }

            _metaDataStream = _file.OpenResourceStream(metaDataFileInfo);
            ReadSecurityDescriptors();

            _dirCache = new ObjectCache<long, List<DirectoryEntry>>();
        }

        /// <summary>
        /// Provides a friendly description of the file system type.
        /// </summary>
        public override string FriendlyName
        {
            get { return "Microsoft WIM"; }
        }

        /// <summary>
        /// Gets the security descriptor associated with the file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect.</param>
        /// <returns>The security descriptor.</returns>
        public RawSecurityDescriptor GetSecurity(string path)
        {
            uint id = GetEntry(path).SecurityId;

            if (id == uint.MaxValue)
            {
                return null;
            }
            if (id >= 0 && id < _securityDescriptors.Count)
            {
                return _securityDescriptors[(int)id];
            }

            // What if there is no descriptor?
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the security descriptor associated with the file or directory.
        /// </summary>
        /// <param name="path">The file or directory to change.</param>
        /// <param name="securityDescriptor">The new security descriptor.</param>
        public void SetSecurity(string path, RawSecurityDescriptor securityDescriptor)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the reparse point data associated with a file or directory.
        /// </summary>
        /// <param name="path">The file to query.</param>
        /// <returns>The reparse point information.</returns>
        public ReparsePoint GetReparsePoint(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);

            ShortResourceHeader hdr = _file.LocateResource(dirEntry.Hash);
            if (hdr == null)
            {
                throw new IOException("No reparse point");
            }

            using (Stream s = _file.OpenResourceStream(hdr))
            {
                byte[] buffer = new byte[s.Length];
                s.Read(buffer, 0, buffer.Length);
                return new ReparsePoint((int)dirEntry.ReparseTag, buffer);
            }
        }

        /// <summary>
        /// Sets the reparse point data on a file or directory.
        /// </summary>
        /// <param name="path">The file to set the reparse point on.</param>
        /// <param name="reparsePoint">The new reparse point.</param>
        public void SetReparsePoint(string path, ReparsePoint reparsePoint)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Removes a reparse point from a file or directory, without deleting the file or directory.
        /// </summary>
        /// <param name="path">The path to the file or directory to remove the reparse point from.</param>
        public void RemoveReparsePoint(string path)
        {
            throw new NotSupportedException();
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
            DirectoryEntry dirEntry = GetEntry(path);
            return dirEntry.ShortName;
        }

        /// <summary>
        /// Sets the short name for a given file or directory.
        /// </summary>
        /// <param name="path">The full path to the file or directory to change.</param>
        /// <param name="shortName">The shortName, which should not include a path.</param>
        public void SetShortName(string path, string shortName)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the standard file information for a file.
        /// </summary>
        /// <param name="path">The full path to the file or directory to query.</param>
        /// <returns>The standard file information.</returns>
        public WindowsFileInformation GetFileStandardInformation(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);

            return new WindowsFileInformation
            {
                CreationTime = DateTime.FromFileTimeUtc(dirEntry.CreationTime),
                LastAccessTime = DateTime.FromFileTimeUtc(dirEntry.LastAccessTime),
                ChangeTime =
                    DateTime.FromFileTimeUtc(Math.Max(dirEntry.LastWriteTime,
                        Math.Max(dirEntry.CreationTime, dirEntry.LastAccessTime))),
                LastWriteTime = DateTime.FromFileTimeUtc(dirEntry.LastWriteTime),
                FileAttributes = dirEntry.Attributes
            };
        }

        /// <summary>
        /// Sets the standard file information for a file.
        /// </summary>
        /// <param name="path">The full path to the file or directory to query.</param>
        /// <param name="info">The standard file information.</param>
        public void SetFileStandardInformation(string path, WindowsFileInformation info)
        {
            throw new NotSupportedException();
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
            DirectoryEntry dirEntry = GetEntry(path);

            List<string> names = new List<string>();
            if (dirEntry.AlternateStreams != null)
            {
                foreach (KeyValuePair<string, AlternateStreamEntry> altStream in dirEntry.AlternateStreams)
                {
                    if (!string.IsNullOrEmpty(altStream.Key))
                    {
                        names.Add(altStream.Key);
                    }
                }
            }

            return names.ToArray();
        }

        /// <summary>
        /// Gets the file id for a given path.
        /// </summary>
        /// <param name="path">The path to get the id of.</param>
        /// <returns>The file id, or -1.</returns>
        /// <remarks>
        /// The returned file id uniquely identifies the file, and is shared by all hard
        /// links to the same file.  The value -1 indicates no unique identifier is
        /// available, and so it can be assumed the file has no hard links.
        /// </remarks>
        public long GetFileId(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            return dirEntry.HardLink == 0 ? -1 : (long)dirEntry.HardLink;
        }

        /// <summary>
        /// Indicates whether the file is known by other names.
        /// </summary>
        /// <param name="path">The file to inspect.</param>
        /// <returns><c>true</c> if the file has other names, else <c>false</c>.</returns>
        public bool HasHardLinks(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            return dirEntry.HardLink != 0;
        }

        /// <summary>
        /// Indicates if a directory exists.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns>true if the directory exists.</returns>
        public override bool DirectoryExists(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            return dirEntry != null && (dirEntry.Attributes & FileAttributes.Directory) != 0;
        }

        /// <summary>
        /// Indicates if a file exists.
        /// </summary>
        /// <param name="path">The path to test.</param>
        /// <returns>true if the file exists.</returns>
        public override bool FileExists(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            return dirEntry != null && (dirEntry.Attributes & FileAttributes.Directory) == 0;
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
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            DirectoryEntry parentDirEntry = GetEntry(path);
            if (parentDirEntry == null)
            {
                throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture,
                    "The directory '{0}' does not exist", path));
            }

            List<DirectoryEntry> parentDir = GetDirectory(parentDirEntry.SubdirOffset);

            return Utilities.Map(parentDir, m => Utilities.CombinePaths(path, m.FileName));
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
            Regex re = Utilities.ConvertWildcardsToRegEx(searchPattern);

            DirectoryEntry parentDirEntry = GetEntry(path);
            if (parentDirEntry == null)
            {
                throw new DirectoryNotFoundException(string.Format(CultureInfo.InvariantCulture,
                    "The directory '{0}' does not exist", path));
            }

            List<DirectoryEntry> parentDir = GetDirectory(parentDirEntry.SubdirOffset);

            List<string> result = new List<string>();
            foreach (DirectoryEntry dirEntry in parentDir)
            {
                if (re.IsMatch(dirEntry.FileName))
                {
                    result.Add(Utilities.CombinePaths(path, dirEntry.FileName));
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
        public override SparseStream OpenFile(string path, FileMode mode, FileAccess access)
        {
            if (mode != FileMode.Open && mode != FileMode.OpenOrCreate)
            {
                throw new NotSupportedException("No write support for WIM files");
            }

            if (access != FileAccess.Read)
            {
                throw new NotSupportedException("No write support for WIM files");
            }

            byte[] streamHash = GetFileHash(path);
            ShortResourceHeader hdr = _file.LocateResource(streamHash);
            if (hdr == null)
            {
                if (Utilities.IsAllZeros(streamHash, 0, streamHash.Length))
                {
                    return new ZeroStream(0);
                }

                throw new IOException("Unable to locate file contents");
            }

            return _file.OpenResourceStream(hdr);
        }

        /// <summary>
        /// Gets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect.</param>
        /// <returns>The attributes of the file or directory.</returns>
        public override FileAttributes GetAttributes(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return dirEntry.Attributes;
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return DateTime.FromFileTimeUtc(dirEntry.CreationTime);
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last access time.</returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return DateTime.FromFileTimeUtc(dirEntry.LastAccessTime);
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The last write time.</returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            DirectoryEntry dirEntry = GetEntry(path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return DateTime.FromFileTimeUtc(dirEntry.LastWriteTime);
        }

        /// <summary>
        /// Gets the length of a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The length in bytes.</returns>
        public override long GetFileLength(string path)
        {
            string filePart;
            string altStreamPart;
            SplitFileName(path, out filePart, out altStreamPart);

            DirectoryEntry dirEntry = GetEntry(filePart);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return dirEntry.GetLength(altStreamPart);
        }

        /// <summary>
        /// Gets the SHA-1 hash of a file's contents.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The 160-bit hash.</returns>
        /// <remarks>The WIM file format internally stores the SHA-1 hash of files.
        /// This method provides access to the stored hash.  Callers can use this
        /// value to compare against the actual hash of the byte stream to validate
        /// the integrity of the file contents.</remarks>
        public byte[] GetFileHash(string path)
        {
            string filePart;
            string altStreamPart;
            SplitFileName(path, out filePart, out altStreamPart);

            DirectoryEntry dirEntry = GetEntry(filePart);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("No such file or directory", path);
            }

            return dirEntry.GetStreamHash(altStreamPart);
        }
 
        /// <summary>
        /// Size of the Filesystem in bytes
        /// </summary>
        public override long Size
        {
            get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
        }
 
        /// <summary>
        /// Used space of the Filesystem in bytes
        /// </summary>
        public override long UsedSpace
        {
            get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
        }
 
        /// <summary>
        /// Available space of the Filesystem in bytes
        /// </summary>
        public override long AvailableSpace
        {
            get { throw new NotSupportedException("Filesystem size is not (yet) supported"); }
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, else <c>false</c>.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                _metaDataStream.Dispose();
                _metaDataStream = null;

                _file = null;
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private static void SplitFileName(string path, out string filePart, out string altStreamPart)
        {
            int streamSepPos = path.IndexOf(":", StringComparison.Ordinal);

            if (streamSepPos >= 0)
            {
                filePart = path.Substring(0, streamSepPos);
                altStreamPart = path.Substring(streamSepPos + 1);
            }
            else
            {
                filePart = path;
                altStreamPart = string.Empty;
            }
        }

        private List<DirectoryEntry> GetDirectory(long id)
        {
            List<DirectoryEntry> dir = _dirCache[id];
            if (dir == null)
            {
                _metaDataStream.Position = id == 0 ? _rootDirPos : id;
                LittleEndianDataReader reader = new LittleEndianDataReader(_metaDataStream);

                dir = new List<DirectoryEntry>();

                DirectoryEntry entry = DirectoryEntry.ReadFrom(reader);
                while (entry != null)
                {
                    dir.Add(entry);
                    entry = DirectoryEntry.ReadFrom(reader);
                }

                _dirCache[id] = dir;
            }

            return dir;
        }

        private void ReadSecurityDescriptors()
        {
            LittleEndianDataReader reader = new LittleEndianDataReader(_metaDataStream);

            long startPos = reader.Position;

            uint totalLength = reader.ReadUInt32();
            uint numEntries = reader.ReadUInt32();
            ulong[] sdLengths = new ulong[numEntries];
            for (uint i = 0; i < numEntries; ++i)
            {
                sdLengths[i] = reader.ReadUInt64();
            }

            _securityDescriptors = new List<RawSecurityDescriptor>((int)numEntries);
            for (uint i = 0; i < numEntries; ++i)
            {
                _securityDescriptors.Add(new RawSecurityDescriptor(reader.ReadBytes((int)sdLengths[i]), 0));
            }

            if (reader.Position < startPos + totalLength)
            {
                reader.Skip((int)(startPos + totalLength - reader.Position));
            }

            _rootDirPos = MathUtilities.RoundUp(startPos + totalLength, 8);
        }

        private DirectoryEntry GetEntry(string path)
        {
            if (path.EndsWith(@"\", StringComparison.Ordinal))
            {
                path = path.Substring(0, path.Length - 1);
            }

            if (!string.IsNullOrEmpty(path) && !path.StartsWith(@"\", StringComparison.OrdinalIgnoreCase))
            {
                path = @"\" + path;
            }

            return GetEntry(GetDirectory(0), path.Split('\\'));
        }

        private DirectoryEntry GetEntry(List<DirectoryEntry> dir, string[] path)
        {
            List<DirectoryEntry> currentDir = dir;
            DirectoryEntry nextEntry = null;

            for (int i = 0; i < path.Length; ++i)
            {
                nextEntry = null;

                foreach (DirectoryEntry entry in currentDir)
                {
                    if (path[i].Equals(entry.FileName, StringComparison.OrdinalIgnoreCase)
                        ||
                        (!string.IsNullOrEmpty(entry.ShortName) &&
                         path[i].Equals(entry.ShortName, StringComparison.OrdinalIgnoreCase)))
                    {
                        nextEntry = entry;
                        break;
                    }
                }

                if (nextEntry == null)
                {
                    return null;
                }
                if (nextEntry.SubdirOffset != 0)
                {
                    currentDir = GetDirectory(nextEntry.SubdirOffset);
                }
            }

            return nextEntry;
        }

        private void DoSearch(List<string> results, string path, Regex regex, bool subFolders, bool dirs, bool files)
        {
            DirectoryEntry parentDirEntry = GetEntry(path);

            if (parentDirEntry.SubdirOffset == 0)
            {
                return;
            }

            List<DirectoryEntry> parentDir = GetDirectory(parentDirEntry.SubdirOffset);

            foreach (DirectoryEntry de in parentDir)
            {
                bool isDir = (de.Attributes & FileAttributes.Directory) != 0;

                if ((isDir && dirs) || (!isDir && files))
                {
                    if (regex.IsMatch(de.SearchName))
                    {
                        results.Add(Utilities.CombinePaths(path, de.FileName));
                    }
                }

                if (subFolders && isDir)
                {
                    DoSearch(results, Utilities.CombinePaths(path, de.FileName), regex, subFolders, dirs, files);
                }
            }
        }
    }
}

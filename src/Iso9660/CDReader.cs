//
// Copyright (c) 2008, Kenneth Bell
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
using System.Text;

namespace DiscUtils.Iso9660
{
    /// <summary>
    /// Class for reading existing ISO images.
    /// </summary>
    public class CDReader : DiscFileSystem
    {
        private Stream _data;
        private CommonVolumeDescriptor _volDesc;
        private List<PathTableRecord> _pathTable;
        private Dictionary<int,int> _pathTableFirstInParent;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="data">The stream to read the ISO image from.</param>
        /// <param name="joliet">Whether to read Joliet extensions.</param>
        public CDReader(Stream data, bool joliet)
        {
            _data = data;

            long vdpos = 0x8000; // Skip lead-in

            byte[] buffer = new byte[2048];

            long pvdPos = 0;
            long svdPos = 0;

            BaseVolumeDescriptor bvd;
            do
            {
                data.Position = vdpos;
                int numRead = data.Read(buffer, 0, 2048);
                if (numRead != 2048)
                {
                    break;
                }


                bvd = new BaseVolumeDescriptor(buffer, 0);
                switch (bvd.VolumeDescriptorType)
                {
                    case VolumeDescriptorType.Boot:
                        break;
                    case VolumeDescriptorType.Primary: //Primary Vol Descriptor
                        pvdPos = vdpos;
                        break;
                    case VolumeDescriptorType.Supplementary: //Supplementary Vol Descriptor
                        svdPos = vdpos;
                        break;
                    case VolumeDescriptorType.Partition: //Volume Partition Descriptor
                        break;
                    case VolumeDescriptorType.SetTerminator: //Volume Descriptor Set Terminator
                        break;
                }

                vdpos += 2048;
            } while (bvd.VolumeDescriptorType != VolumeDescriptorType.SetTerminator);


            if (joliet)
            {
                data.Position = svdPos;
                data.Read(buffer, 0, 2048);
                _volDesc = new SupplementaryVolumeDescriptor(buffer, 0);
            }
            else
            {
                data.Position = pvdPos;
                data.Read(buffer, 0, 2048);
                _volDesc = new PrimaryVolumeDescriptor(buffer, 0);
            }

            // Skip to Path Table
            data.Position = _volDesc.LogicalBlockSize * _volDesc.TypeLPathTableLocation;
            byte[] pathTableBuffer = new byte[_volDesc.PathTableSize];
            data.Read(pathTableBuffer, 0, pathTableBuffer.Length);

            _pathTable = new List<PathTableRecord>();
            _pathTableFirstInParent = new Dictionary<int,int>();
            uint pos = 0;
            int lastParent = 0;
            while (pos < _volDesc.PathTableSize)
            {
                PathTableRecord ptr;
                int length = PathTableRecord.ReadFrom(pathTableBuffer, (int)pos, false, _volDesc.CharacterEncoding, out ptr);

                if (lastParent != ptr.ParentDirectoryNumber)
                {
                    _pathTableFirstInParent[ptr.ParentDirectoryNumber] = _pathTable.Count;
                    lastParent = ptr.ParentDirectoryNumber;
                }

                _pathTable.Add(ptr);

                pos += (uint)length;
            }
        }

        /// <summary>
        /// Provides the friendly name for the CD filesystem.
        /// </summary>
        public override string FriendlyName
        {
            get { return "ISO 9660 (CD-ROM)"; }
        }

        /// <summary>
        /// Indicates ISO files are read-only.
        /// </summary>
        /// <returns>Always returns <c>false</c>.</returns>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the root directory of the ISO.
        /// </summary>
        public override DiscDirectoryInfo Root
        {
            get { return new ReaderDirectoryInfo(this, null, _volDesc.RootDirectory, _volDesc.CharacterEncoding); }
        }

        /// <summary>
        /// Creates a directory, not supported for ISO file systems.
        /// </summary>
        /// <param name="path">The path of the new directory</param>
        public override void CreateDirectory(string path)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Deletes a directory, not supported for ISO file systems.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        public override void DeleteDirectory(string path)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Deletes a directory, optionally with all descendants - not supported for ISO file systems.
        /// </summary>
        /// <param name="path">The path of the directory to delete.</param>
        /// <param name="recursive">Determines if the all descendants should be deleted</param>
        public override void DeleteDirectory(string path, bool recursive)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Deletes a file - not supported for ISO file systems.
        /// </summary>
        /// <param name="path">The path of the file to delete.</param>
        public override void DeleteFile(string path)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Indicates if a directory exists.
        /// </summary>
        /// <param name="path">The path to test</param>
        /// <returns>true if the directory exists</returns>
        public override bool DirectoryExists(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of directories.</returns>
        public override string[] GetDirectories(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of subdirectories in a specified directory matching a specified
        /// search pattern.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of directories matching the search pattern.</returns>
        public override string[] GetDirectories(string path, string searchPattern)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files.</returns>
        public override string[] GetFiles(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of files in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against.</param>
        /// <returns>Array of files matching the search pattern.</returns>
        public override string[] GetFiles(string path, string searchPattern)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the names of all files and subdirectories in a specified directory.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>Array of files and subdirectories matching the search pattern.</returns>
        public override string[] GetFileSystemEntries(string path)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
        /// Opens a file on the ISO image.
        /// </summary>
        /// <param name="path">The full path to the file.</param>
        /// <param name="mode">Must be <c>FileMode.Open</c></param>
        /// <param name="access">Must be <c>FileMode.Read</c></param>
        /// <returns>The file as a stream.</returns>
        public override Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            if (mode != FileMode.Open)
            {
                throw new NotSupportedException("Only existing files can be opened");
            }

            if (access != FileAccess.Read)
            {
                throw new NotSupportedException("Files cannot be opened for write");
            }


            int pos = path.LastIndexOf('\\');
            if (pos == path.Length - 1)
            {
                throw new FileNotFoundException("Invalid path", path);
            }

            string dir = (pos <= 0) ? "\0" : path.Substring(0, pos);
            string file = path.Substring(pos + 1);

            PathTableRecord ptr = SearchPathTable(dir);

            ReaderDirectoryInfo dirInfo = new ReaderDirectoryInfo(
                this,
                null,
                new DirectoryRecord(ptr.DirectoryIdentifier, FileFlags.Directory, ptr.LocationOfExtent, uint.MaxValue),
                _volDesc.CharacterEncoding);

            DiscFileInfo[] fileInfo = dirInfo.GetFiles(file);
            if (fileInfo.Length != 1)
            {
                throw new FileNotFoundException("Ambiguous file, or no such file", path);
            }

            return fileInfo[0].Open(mode);
        }

        /// <summary>
        /// Gets the attributes of a file or directory.
        /// </summary>
        /// <param name="path">The file or directory to inspect</param>
        /// <returns>The attributes of the file or directory</returns>
        public override FileAttributes GetAttributes(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the attributes of a file or directory - not supported for ISO file systems.
        /// </summary>
        /// <param name="path">The file or directory to change</param>
        /// <param name="newValue">The new attributes of the file or directory</param>
        public override void SetAttributes(string path, FileAttributes newValue)
        {
            throw new NotSupportedException();
        }


        /// <summary>
        /// Gets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTime(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the creation time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTime(string path, DateTime newTime)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <returns>The creation time.</returns>
        public override DateTime GetCreationTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the creation time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetCreationTimeUtc(string path, DateTime newTime)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns></returns>
        public override DateTime GetLastAccessTime(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last access time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTime(string path, DateTime newTime)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns></returns>
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last access time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastAccessTimeUtc(string path, DateTime newTime)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns></returns>
        public override DateTime GetLastWriteTime(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last modification time (in local time) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTime(string path, DateTime newTime)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory</param>
        /// <returns></returns>
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the last modification time (in UTC) of a file or directory.
        /// </summary>
        /// <param name="path">The path of the file or directory.</param>
        /// <param name="newTime">The new time to set.</param>
        public override void SetLastWriteTimeUtc(string path, DateTime newTime)
        {
            throw new NotSupportedException();
        }

        private PathTableRecord SearchPathTable(string path)
        {
            string[] pathParts = path.Split(new char[]{'\\'},StringSplitOptions.RemoveEmptyEntries);
            int part = 0;
            int pathTableIdx = 0;
            ushort parent = 1;

            string partStr = pathParts[part].ToUpperInvariant();
            PathTableRecord ptr = _pathTable[pathTableIdx];
            while (ptr.ParentDirectoryNumber == parent)
            {
                if (ptr.DirectoryIdentifier.ToUpperInvariant() == partStr)
                {
                    int newIdx;

                    if (part == pathParts.Length - 1)
                    {
                        // Found all parts of the path - we're done
                        return ptr;
                    }
                    else if (_pathTableFirstInParent.TryGetValue(pathTableIdx + 1, out newIdx))
                    {
                        // This dir has sub-dirs, so start searching them, moving on to next part
                        // of the requested path
                        parent = (ushort)(pathTableIdx + 1);
                        pathTableIdx = newIdx;

                        part++;
                        partStr = pathParts[part].ToUpperInvariant();
                    }
                    else
                    {
                        // No sub-dirs for this dir and not at final part of the path
                        throw new FileNotFoundException("No such directory", path);
                    }
                }
                else
                {
                    pathTableIdx++;
                }
                ptr = _pathTable[pathTableIdx];
            }

            // Fell off the end of parent's records
            throw new FileNotFoundException("No such directory", path);
        }

        internal Stream GetExtentStream(DirectoryRecord record)
        {
            return new ExtentStream(_data, record.LocationOfExtent, record.DataLength, record.FileUnitSize, record.InterleaveGapSize);
        }

        /// <summary>
        /// Gets an object representing a possible file.
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file does not need to exist</remarks>
        public override DiscFileInfo GetFileInfo(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an object representing a possible directory.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The directory does not need to exist</remarks>
        public override DiscDirectoryInfo GetDirectoryInfo(string path)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an object representing a possible file system object (file or directory).
        /// </summary>
        /// <param name="path">The file system path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file system object does not need to exist</remarks>
        public override DiscFileSystemInfo GetFileSystemInfo(string path)
        {
            throw new NotImplementedException();
        }
    }

}

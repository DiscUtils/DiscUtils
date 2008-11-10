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
        /// Opens a file on the ISO image.
        /// </summary>
        /// <param name="path">The full path to the file.</param>
        /// <param name="mode">Must be <c>FileMode.Open</c></param>
        /// <param name="access">Must be <c>FileMode.Read</c></param>
        /// <returns>The file as a stream.</returns>
        public override Stream Open(string path, FileMode mode, FileAccess access)
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

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
    internal class CDBuildStream : Stream
    {
        private List<BuildFileInfo> _files;
        private List<BuildDirectoryInfo> _dirs;
        private BuildDirectoryInfo _rootDirectory;
        private BuildParameters _buildParams;

        private DateTime _buildTime;
        private Encoding _suppEncoding;
        private Dictionary<BuildDirectoryMember, uint> _primaryLocationTable;
        private Dictionary<BuildDirectoryMember, uint> _supplementaryLocationTable;
        private List<DiskRegion> _fixedRegions;

        private DiskRegion _currentRegion;
        private long _position;
        private byte[] _blockBuffer = new byte[2048];

        private const long DiskStart = 0x8000;
        private long _endOfDisk;

        public CDBuildStream(List<BuildFileInfo> files, List<BuildDirectoryInfo> dirs, BuildDirectoryInfo rootDirectory, BuildParameters buildParams)
        {
            _files = files;
            _dirs = dirs;
            _rootDirectory = rootDirectory;
            _buildParams = buildParams;

            Fix();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (_currentRegion != null)
                {
                    _currentRegion.DisposeReadState();
                }
            }
        }

        /// <summary>
        /// Fixes the data structures of the ISO, setting timestamps and locations.
        /// </summary>
        private void Fix()
        {
            _buildTime = DateTime.UtcNow;

            _suppEncoding = _buildParams.UseJoliet ? Encoding.BigEndianUnicode : Encoding.ASCII;

            _primaryLocationTable = new Dictionary<BuildDirectoryMember, uint>();
            _supplementaryLocationTable = new Dictionary<BuildDirectoryMember, uint>();
            _fixedRegions = new List<DiskRegion>();

            long focus = DiskStart + 3 * 2048; // Primary, Supplementary, End (fixed at end...)

            focus = FixFiles(focus);

            // There are two directory tables
            //  1. Primary        (std ISO9660)
            //  2. Supplementary  (Joliet)

            // Find start of the second set of directory data, fixing ASCII directories in place.
            long startOfFirstDirData = focus;
            focus = FixDirectories(focus, _primaryLocationTable, Encoding.ASCII);

            // Find end of the second directory table, fixing supplementary directories in place.
            long startOfSecondDirData = focus;
            focus = FixDirectories(focus, _supplementaryLocationTable, _suppEncoding);

            // There are four path tables:
            //  1. LE, ASCII
            //  2. BE, ASCII
            //  3. LE, Supp Encoding (Joliet)
            //  4. BE, Supp Encoding (Joliet)

            // Find end of the path table
            long startOfFirstPathTable = focus;
            PathTable pathTable = new PathTable(false, Encoding.ASCII, _dirs, _primaryLocationTable, focus);
            _fixedRegions.Add(pathTable);
            focus += pathTable.DiskLength;
            long primaryPathTableLength = pathTable.DataLength;

            long startOfSecondPathTable = focus;
            pathTable = new PathTable(true, Encoding.ASCII, _dirs, _primaryLocationTable, focus);
            _fixedRegions.Add(pathTable);
            focus += pathTable.DiskLength;

            long startOfThirdPathTable = focus;
            pathTable = new PathTable(false, _suppEncoding, _dirs, _supplementaryLocationTable, focus);
            _fixedRegions.Add(pathTable);
            focus += pathTable.DiskLength;
            long supplementaryPathTableLength = pathTable.DataLength;

            long startOfFourthPathTable = focus;
            pathTable = new PathTable(true, _suppEncoding, _dirs, _supplementaryLocationTable, focus);
            _fixedRegions.Add(pathTable);
            focus += pathTable.DiskLength;

            // Find the end of the disk
            _endOfDisk = focus;


            PrimaryVolumeDescriptor pvDesc = new PrimaryVolumeDescriptor(
                (uint)(_endOfDisk / 2048),                        // VolumeSpaceSize
                (uint)(primaryPathTableLength),                  // PathTableSize
                (uint)(startOfFirstPathTable / 2048),            // TypeLPathTableLocation
                (uint)(startOfSecondPathTable / 2048),           // TypeMPathTableLocation
                (uint)(startOfFirstDirData / 2048),              // RootDirectory.LocationOfExtent
                (uint)_rootDirectory.GetDataSize(Encoding.ASCII), // RootDirectory.DataLength
                _buildTime
                );
            pvDesc.VolumeIdentifier = _buildParams.VolumeIdentifier;
            PrimaryVolumeDescriptorRegion pvdr = new PrimaryVolumeDescriptorRegion(pvDesc, DiskStart);
            _fixedRegions.Insert(0, pvdr);

            SupplementaryVolumeDescriptor svDesc = new SupplementaryVolumeDescriptor(
                (uint)(_endOfDisk / 2048),                        // VolumeSpaceSize
                (uint)(supplementaryPathTableLength),            // PathTableSize
                (uint)(startOfThirdPathTable / 2048),            // TypeLPathTableLocation
                (uint)(startOfFourthPathTable / 2048),           // TypeMPathTableLocation
                (uint)(startOfSecondDirData / 2048),             // RootDirectory.LocationOfExtent
                (uint)_rootDirectory.GetDataSize(_suppEncoding),   // RootDirectory.DataLength
                _buildTime,
                _suppEncoding
                );
            svDesc.VolumeIdentifier = _buildParams.VolumeIdentifier;
            SupplementaryVolumeDescriptorRegion svdr = new SupplementaryVolumeDescriptorRegion(svDesc, DiskStart + 2048);
            _fixedRegions.Insert(1, svdr);

            VolumeDescriptorSetTerminator evDesc = new VolumeDescriptorSetTerminator();
            VolumeDescriptorSetTerminatorRegion evdr = new VolumeDescriptorSetTerminatorRegion(evDesc, DiskStart + 4096);
            _fixedRegions.Insert(2, evdr);
        }

        private long FixDirectories(long focus, Dictionary<BuildDirectoryMember, uint> locationTable, Encoding enc)
        {
            foreach (BuildDirectoryInfo di in _dirs)
            {
                locationTable.Add(di, (uint)(focus / 2048));
                DirectoryExtent extent = new DirectoryExtent(di, locationTable, enc, focus);
                _fixedRegions.Add(extent);
                focus += extent.DiskLength;
            }
            return focus;
        }

        private long FixFiles(long focus)
        {
            // Find end of the file data, fixing the files in place as we go
            foreach (BuildFileInfo fi in _files)
            {
                _primaryLocationTable.Add(fi, (uint)(focus / 2048));
                _supplementaryLocationTable.Add(fi, (uint)(focus / 2048));
                FileExtent extent = new FileExtent(fi, focus);

                // Only remember files of non-zero length (otherwise we'll stomp on a valid file)
                if (extent.DiskLength != 0)
                {
                    _fixedRegions.Add(extent);
                }

                focus += extent.DiskLength;
            }
            return focus;
        }

        private void GetLogicalBlock(long start, byte[] buffer, int offset)
        {
            // If current region is outside the area of interest, clean it up
            if (_currentRegion != null && (start < _currentRegion.DiskStart || start >= _currentRegion.DiskStart + _currentRegion.DiskLength))
            {
                _currentRegion.DisposeReadState();
                _currentRegion = null;
            }

            // If we need to find a new region, look for it
            if (_currentRegion == null)
            {
                int idx = _fixedRegions.BinarySearch(new SearchDiskRegion(start), new DiskRegionComparer());
                if (idx >= 0)
                {
                    DiskRegion region = _fixedRegions[idx];
                    region.PrepareForRead();
                    _currentRegion = region;
                }
            }

            // If the block is outside any known region, fill with zeros, else read the block
            if (_currentRegion == null)
            {
                Array.Clear(buffer, offset, 2048);
            }
            else
            {
                _currentRegion.ReadLogicalBlock(start, buffer, offset);
            }
        }

        private class DiskRegionComparer : IComparer<DiskRegion>
        {

            #region IComparer<DiskRegion> Members

            public int Compare(DiskRegion x, DiskRegion y)
            {
                if (x.DiskStart < y.DiskStart && (x.DiskStart + x.DiskLength) <= (y.DiskStart + y.DiskLength))
                {
                    // x < y, with no intersection
                    return -1;
                }
                else if (x.DiskStart > y.DiskStart && (x.DiskStart + x.DiskLength) > (y.DiskStart + y.DiskLength))
                {
                    // x > y, with no intersection
                    return 1;
                }

                // x intersects y
                return 0;
            }

            #endregion
        }

        #region Stream Members
        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            return;
        }

        public override long Length
        {
            get { return _endOfDisk; }
        }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _endOfDisk)
            {
                return 0;
            }

            // Get the current block
            GetLogicalBlock((_position / 2048) * 2048, _blockBuffer, 0);

            // Read up to the block boundary only
            int numRead = (int)Math.Min(count, 2048 - (_position % 2048));
            Array.Copy(_blockBuffer, _position % 2048, buffer, offset, numRead);
            _position += numRead;
            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = offset;
            if (origin == SeekOrigin.Current)
            {
                newPos += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                newPos += Length;
            }
            _position = newPos;
            return newPos;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        #endregion
    }
}

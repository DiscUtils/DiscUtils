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
using System.Text;
using System.IO;

namespace DiscUtils.Iso9660
{
    internal class PathTable : DiskRegion
    {
        private bool byteSwap;
        private Encoding enc;
        private List<BuildDirectoryInfo> dirs;
        private Dictionary<BuildDirectoryMember, uint> locations;

        private uint dataLength;

        private byte[] readCache;

        public PathTable(bool byteSwap, Encoding enc, List<BuildDirectoryInfo> dirs, Dictionary<BuildDirectoryMember, uint> locations, long start)
            : base(start)
        {
            this.byteSwap = byteSwap;
            this.enc = enc;
            this.dirs = dirs;
            this.locations = locations;

            uint length = 0;
            foreach (BuildDirectoryInfo di in dirs)
            {
                length += di.GetPathTableEntrySize(enc);
            }
            dataLength = length;
            DiskLength = ((length + 2047) / 2048) * 2048;
        }

        public uint DataLength
        {
            get { return dataLength; }
        }

        internal override void PrepareForRead()
        {
            readCache = new byte[DiskLength];
            int pos = 0;

            List<BuildDirectoryInfo> sortedList = new List<BuildDirectoryInfo>(dirs);
            sortedList.Sort(BuildDirectoryInfo.PathTableSortComparison);

            Dictionary<BuildDirectoryInfo, ushort> dirNumbers = new Dictionary<BuildDirectoryInfo, ushort>(dirs.Count);
            ushort i = 1;
            foreach (BuildDirectoryInfo di in sortedList)
            {
                dirNumbers[di] = i++;
                PathTableRecord ptr = new PathTableRecord();
                ptr.DirectoryIdentifier = di.PickName(null, enc);
                ptr.LocationOfExtent = locations[di];
                ptr.ParentDirectoryNumber = dirNumbers[di.Parent];

                pos += ptr.Write(byteSwap, enc, readCache, pos);
            }
        }

        internal override void ReadLogicalBlock(long diskOffset, byte[] buffer, int offset)
        {
            long relPos = diskOffset - DiskStart;
            Array.Copy(readCache, relPos, buffer, offset, 2048);
        }

        internal override void DisposeReadState()
        {
            readCache = null;
        }

    }
}

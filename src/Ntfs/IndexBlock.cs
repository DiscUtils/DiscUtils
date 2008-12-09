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

using System.Collections.Generic;

namespace DiscUtils.Ntfs
{
    internal class IndexBlock<K, D> : FixupRecordBase
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        public ulong LogSequenceNumber;
        public ulong IndexBlockVcn; // Virtual Cluster Number (maybe in sectors sometimes...?)
        public IndexEntryHeader IndexHeader;

        public List<IndexEntry<K,D>> IndexEntries;

        public IndexBlock(int sectorSize)
            : base(sectorSize)
        {

        }

        protected override void Read(byte[] buffer, int offset)
        {
            LogSequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            IndexBlockVcn = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            IndexHeader = new IndexEntryHeader(buffer, offset + 0x18);

            IndexEntries = new List<IndexEntry<K,D>>();
            long pos = 0;
            while (pos < IndexHeader.TotalSizeOfEntries)
            {
                IndexEntry<K, D> entry = new IndexEntry<K, D>(buffer, (int)(offset + IndexHeader.OffsetToFirstEntry + 0x18 + pos));
                if ((entry.Flags & IndexEntryFlags.End) != 0)
                {
                    break;
                }

                IndexEntries.Add(entry);

                pos += entry.Length;
            }
        }
    }
}

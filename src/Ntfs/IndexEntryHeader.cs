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

namespace DiscUtils.Ntfs
{
    internal class IndexEntryHeader
    {
        public const int Size = 0x10;

        public uint OffsetToFirstEntry;
        public uint TotalSizeOfEntries;
        public uint AllocatedSizeOfEntries;
        public IndexEntryFlags Flags;

        public IndexEntryHeader(byte[] data, int offset)
        {
            OffsetToFirstEntry = Utilities.ToUInt32LittleEndian(data, offset + 0x00);
            TotalSizeOfEntries = Utilities.ToUInt32LittleEndian(data, offset + 0x04);
            AllocatedSizeOfEntries = Utilities.ToUInt32LittleEndian(data, offset + 0x08);
            Flags = (IndexEntryFlags)Utilities.ToUInt16LittleEndian(data, offset + 0x0C);
        }
    }

    [Flags]
    internal enum IndexEntryFlags : ushort
    {
        None = 0x00,
        Node = 0x01,
        End = 0x02
    }
}

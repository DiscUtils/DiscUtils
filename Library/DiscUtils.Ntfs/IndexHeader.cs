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

using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class IndexHeader
    {
        public const int Size = 0x10;
        public uint AllocatedSizeOfEntries;
        public byte HasChildNodes;

        public uint OffsetToFirstEntry;
        public uint TotalSizeOfEntries;

        public IndexHeader(uint allocatedSize)
        {
            AllocatedSizeOfEntries = allocatedSize;
        }

        public IndexHeader(byte[] data, int offset)
        {
            OffsetToFirstEntry = EndianUtilities.ToUInt32LittleEndian(data, offset + 0x00);
            TotalSizeOfEntries = EndianUtilities.ToUInt32LittleEndian(data, offset + 0x04);
            AllocatedSizeOfEntries = EndianUtilities.ToUInt32LittleEndian(data, offset + 0x08);
            HasChildNodes = data[offset + 0x0C];
        }

        internal void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(OffsetToFirstEntry, buffer, offset + 0x00);
            EndianUtilities.WriteBytesLittleEndian(TotalSizeOfEntries, buffer, offset + 0x04);
            EndianUtilities.WriteBytesLittleEndian(AllocatedSizeOfEntries, buffer, offset + 0x08);
            buffer[offset + 0x0C] = HasChildNodes;
            buffer[offset + 0x0D] = 0;
            buffer[offset + 0x0E] = 0;
            buffer[offset + 0x0F] = 0;
        }
    }
}
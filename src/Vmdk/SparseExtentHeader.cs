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


namespace DiscUtils.Vmdk
{
    internal class SparseExtentHeader
    {
        public const uint VmdkMagicNumber = 0x564d444b;

        public uint MagicNumber;
        public uint Version;
        public uint Flags;
        public long Capacity;
        public long GrainSize;
        public long DescriptorOffset;
        public long DescriptorSize;
        public uint NumGTEsPerGT;
        public long RgdOffset;
        public long GdOffset;
        public long Overhead;
        public byte UncleanShutdown;
        public byte SingleEndLineChar;
        public byte NonEndLineChar;
        public byte DoubleEndLineChar1;
        public byte DoubleEndLineChar2;
        public ushort CompressAlgorithm;

        public static SparseExtentHeader Read(byte[] buffer, int offset)
        {
            SparseExtentHeader hdr = new SparseExtentHeader();
            hdr.MagicNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
            hdr.Version = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
            hdr.Flags = Utilities.ToUInt32LittleEndian(buffer, offset + 8);
            hdr.Capacity = Utilities.ToInt64LittleEndian(buffer, offset + 0x0C);
            hdr.GrainSize = Utilities.ToInt64LittleEndian(buffer, offset + 0x14);
            hdr.DescriptorOffset = Utilities.ToInt64LittleEndian(buffer, offset + 0x1C);
            hdr.DescriptorSize = Utilities.ToInt64LittleEndian(buffer, offset + 0x24);
            hdr.NumGTEsPerGT = Utilities.ToUInt32LittleEndian(buffer, offset + 0x2C);
            hdr.RgdOffset = Utilities.ToInt64LittleEndian(buffer, offset + 0x30);
            hdr.GdOffset = Utilities.ToInt64LittleEndian(buffer, offset + 0x38);
            hdr.Overhead = Utilities.ToInt64LittleEndian(buffer, offset + 0x40);
            hdr.UncleanShutdown = buffer[offset + 0x48];
            hdr.SingleEndLineChar = buffer[offset + 0x49];
            hdr.NonEndLineChar = buffer[offset + 0x4A];
            hdr.DoubleEndLineChar1 = buffer[offset + 0x4B];
            hdr.DoubleEndLineChar2 = buffer[offset + 0x4C];
            hdr.CompressAlgorithm = Utilities.ToUInt16LittleEndian(buffer, offset + 0x4D);

            return hdr;
        }
    }
}

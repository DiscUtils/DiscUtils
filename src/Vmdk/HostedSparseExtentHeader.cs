//
// Copyright (c) 2008-2010, Kenneth Bell
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
    internal class HostedSparseExtentHeader : CommonSparseExtentHeader
    {
        public const uint VmdkMagicNumber = 0x564d444b;

        public HostedSparseExtentFlags Flags;
        public long DescriptorOffset;
        public long DescriptorSize;
        public long RgdOffset;
        public long Overhead;
        public byte UncleanShutdown;
        public byte SingleEndLineChar;
        public byte NonEndLineChar;
        public byte DoubleEndLineChar1;
        public byte DoubleEndLineChar2;
        public ushort CompressAlgorithm;

        public HostedSparseExtentHeader()
        {
            MagicNumber = VmdkMagicNumber;
            Version = 1;
            SingleEndLineChar = (byte)'\n';
            NonEndLineChar = (byte)' ';
            DoubleEndLineChar1 = (byte)'\r';
            DoubleEndLineChar2 = (byte)'\n';
        }

        public static HostedSparseExtentHeader Read(byte[] buffer, int offset)
        {
            HostedSparseExtentHeader hdr = new HostedSparseExtentHeader();
            hdr.MagicNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
            hdr.Version = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
            hdr.Flags = (HostedSparseExtentFlags)Utilities.ToUInt32LittleEndian(buffer, offset + 8);
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

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Sizes.Sector];
            Utilities.WriteBytesLittleEndian(MagicNumber, buffer, 0x00);
            Utilities.WriteBytesLittleEndian(Version, buffer, 0x04);
            Utilities.WriteBytesLittleEndian((uint)Flags, buffer, 0x08);
            Utilities.WriteBytesLittleEndian(Capacity, buffer, 0x0C);
            Utilities.WriteBytesLittleEndian(GrainSize, buffer, 0x14);
            Utilities.WriteBytesLittleEndian(DescriptorOffset, buffer, 0x1C);
            Utilities.WriteBytesLittleEndian(DescriptorSize, buffer, 0x24);
            Utilities.WriteBytesLittleEndian(NumGTEsPerGT, buffer, 0x2C);
            Utilities.WriteBytesLittleEndian(RgdOffset, buffer, 0x30);
            Utilities.WriteBytesLittleEndian(GdOffset, buffer, 0x38);
            Utilities.WriteBytesLittleEndian(Overhead, buffer, 0x40);
            buffer[0x48] = UncleanShutdown;
            buffer[0x49] = SingleEndLineChar;
            buffer[0x4A] = NonEndLineChar;
            buffer[0x4B] = DoubleEndLineChar1;
            buffer[0x4C] = DoubleEndLineChar2;
            Utilities.WriteBytesLittleEndian(CompressAlgorithm, buffer, 0x4D);
            return buffer;
        }
    }
}

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

namespace DiscUtils.Vmdk
{
    internal class HostedSparseExtentHeader : CommonSparseExtentHeader
    {
        public const uint VmdkMagicNumber = 0x564d444b;
        public ushort CompressAlgorithm;
        public long DescriptorOffset;
        public long DescriptorSize;
        public byte DoubleEndLineChar1;
        public byte DoubleEndLineChar2;

        public HostedSparseExtentFlags Flags;
        public byte NonEndLineChar;
        public long Overhead;
        public long RgdOffset;
        public byte SingleEndLineChar;
        public byte UncleanShutdown;

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
            hdr.MagicNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
            hdr.Version = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
            hdr.Flags = (HostedSparseExtentFlags)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8);
            hdr.Capacity = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x0C);
            hdr.GrainSize = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x14);
            hdr.DescriptorOffset = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x1C);
            hdr.DescriptorSize = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x24);
            hdr.NumGTEsPerGT = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x2C);
            hdr.RgdOffset = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x30);
            hdr.GdOffset = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x38);
            hdr.Overhead = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x40);
            hdr.UncleanShutdown = buffer[offset + 0x48];
            hdr.SingleEndLineChar = buffer[offset + 0x49];
            hdr.NonEndLineChar = buffer[offset + 0x4A];
            hdr.DoubleEndLineChar1 = buffer[offset + 0x4B];
            hdr.DoubleEndLineChar2 = buffer[offset + 0x4C];
            hdr.CompressAlgorithm = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x4D);

            return hdr;
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Sizes.Sector];
            EndianUtilities.WriteBytesLittleEndian(MagicNumber, buffer, 0x00);
            EndianUtilities.WriteBytesLittleEndian(Version, buffer, 0x04);
            EndianUtilities.WriteBytesLittleEndian((uint)Flags, buffer, 0x08);
            EndianUtilities.WriteBytesLittleEndian(Capacity, buffer, 0x0C);
            EndianUtilities.WriteBytesLittleEndian(GrainSize, buffer, 0x14);
            EndianUtilities.WriteBytesLittleEndian(DescriptorOffset, buffer, 0x1C);
            EndianUtilities.WriteBytesLittleEndian(DescriptorSize, buffer, 0x24);
            EndianUtilities.WriteBytesLittleEndian(NumGTEsPerGT, buffer, 0x2C);
            EndianUtilities.WriteBytesLittleEndian(RgdOffset, buffer, 0x30);
            EndianUtilities.WriteBytesLittleEndian(GdOffset, buffer, 0x38);
            EndianUtilities.WriteBytesLittleEndian(Overhead, buffer, 0x40);
            buffer[0x48] = UncleanShutdown;
            buffer[0x49] = SingleEndLineChar;
            buffer[0x4A] = NonEndLineChar;
            buffer[0x4B] = DoubleEndLineChar1;
            buffer[0x4C] = DoubleEndLineChar2;
            EndianUtilities.WriteBytesLittleEndian(CompressAlgorithm, buffer, 0x4D);
            return buffer;
        }
    }
}
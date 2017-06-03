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

using System;
using DiscUtils.Streams;

namespace DiscUtils.SquashFs
{
    internal class SuperBlock : IByteArraySerializable
    {
        public const uint SquashFsMagic = 0x73717368;
        public uint BlockSize;
        public ushort BlockSizeLog2;
        public long BytesUsed;
        public ushort Compression;
        public DateTime CreationTime;
        public long DirectoryTableStart;
        public long ExtendedAttrsTableStart;
        public ushort Flags;
        public uint FragmentsCount;
        public long FragmentTableStart;
        public uint InodesCount;
        public long InodeTableStart;
        public long LookupTableStart;

        public uint Magic;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public MetadataRef RootInode;
        public ushort UidGidCount;
        public long UidGidTableStart;

        public int Size
        {
            get { return 96; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Magic = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
            InodesCount = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
            CreationTime = ((long) EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8)).FromUnixTimeSeconds().DateTime;
            BlockSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 12);
            FragmentsCount = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 16);
            Compression = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 20);
            BlockSizeLog2 = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 22);
            Flags = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 24);
            UidGidCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 26);
            MajorVersion = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 28);
            MinorVersion = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 30);
            RootInode = new MetadataRef(EndianUtilities.ToInt64LittleEndian(buffer, offset + 32));
            BytesUsed = EndianUtilities.ToInt64LittleEndian(buffer, offset + 40);
            UidGidTableStart = EndianUtilities.ToInt64LittleEndian(buffer, offset + 48);
            ExtendedAttrsTableStart = EndianUtilities.ToInt64LittleEndian(buffer, offset + 56);
            InodeTableStart = EndianUtilities.ToInt64LittleEndian(buffer, offset + 64);
            DirectoryTableStart = EndianUtilities.ToInt64LittleEndian(buffer, offset + 72);
            FragmentTableStart = EndianUtilities.ToInt64LittleEndian(buffer, offset + 80);
            LookupTableStart = EndianUtilities.ToInt64LittleEndian(buffer, offset + 88);

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(Magic, buffer, offset + 0);
            EndianUtilities.WriteBytesLittleEndian(InodesCount, buffer, offset + 4);
            EndianUtilities.WriteBytesLittleEndian(Convert.ToUInt32((new DateTimeOffset(CreationTime)).ToUnixTimeSeconds()), buffer, offset + 8);
            EndianUtilities.WriteBytesLittleEndian(BlockSize, buffer, offset + 12);
            EndianUtilities.WriteBytesLittleEndian(FragmentsCount, buffer, offset + 16);
            EndianUtilities.WriteBytesLittleEndian(Compression, buffer, offset + 20);
            EndianUtilities.WriteBytesLittleEndian(BlockSizeLog2, buffer, offset + 22);
            EndianUtilities.WriteBytesLittleEndian(Flags, buffer, offset + 24);
            EndianUtilities.WriteBytesLittleEndian(UidGidCount, buffer, offset + 26);
            EndianUtilities.WriteBytesLittleEndian(MajorVersion, buffer, offset + 28);
            EndianUtilities.WriteBytesLittleEndian(MinorVersion, buffer, offset + 30);
            EndianUtilities.WriteBytesLittleEndian(RootInode.Value, buffer, offset + 32);
            EndianUtilities.WriteBytesLittleEndian(BytesUsed, buffer, offset + 40);
            EndianUtilities.WriteBytesLittleEndian(UidGidTableStart, buffer, offset + 48);
            EndianUtilities.WriteBytesLittleEndian(ExtendedAttrsTableStart, buffer, offset + 56);
            EndianUtilities.WriteBytesLittleEndian(InodeTableStart, buffer, offset + 64);
            EndianUtilities.WriteBytesLittleEndian(DirectoryTableStart, buffer, offset + 72);
            EndianUtilities.WriteBytesLittleEndian(FragmentTableStart, buffer, offset + 80);
            EndianUtilities.WriteBytesLittleEndian(LookupTableStart, buffer, offset + 88);
        }
    }
}
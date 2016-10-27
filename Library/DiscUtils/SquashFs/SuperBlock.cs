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

namespace DiscUtils.SquashFs
{
    using System;
    using System.IO;

    internal class SuperBlock : IByteArraySerializable
    {
        public const uint SquashFsMagic = 0x73717368;

        public uint Magic;
        public uint InodesCount;
        public DateTime CreationTime;
        public uint BlockSize;
        public uint FragmentsCount;
        public ushort Compression;
        public ushort BlockSizeLog2;
        public ushort Flags;
        public ushort UidGidCount;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public MetadataRef RootInode;
        public long BytesUsed;
        public long UidGidTableStart;
        public long ExtendedAttrsTableStart;
        public long InodeTableStart;
        public long DirectoryTableStart;
        public long FragmentTableStart;
        public long LookupTableStart;

        public int Size
        {
            get { return 96; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Magic = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
            InodesCount = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
            CreationTime = Utilities.DateTimeFromUnix(Utilities.ToUInt32LittleEndian(buffer, offset + 8));
            BlockSize = Utilities.ToUInt32LittleEndian(buffer, offset + 12);
            FragmentsCount = Utilities.ToUInt32LittleEndian(buffer, offset + 16);
            Compression = Utilities.ToUInt16LittleEndian(buffer, offset + 20);
            BlockSizeLog2 = Utilities.ToUInt16LittleEndian(buffer, offset + 22);
            Flags = Utilities.ToUInt16LittleEndian(buffer, offset + 24);
            UidGidCount = Utilities.ToUInt16LittleEndian(buffer, offset + 26);
            MajorVersion = Utilities.ToUInt16LittleEndian(buffer, offset + 28);
            MinorVersion = Utilities.ToUInt16LittleEndian(buffer, offset + 30);
            RootInode = new MetadataRef(Utilities.ToInt64LittleEndian(buffer, offset + 32));
            BytesUsed = Utilities.ToInt64LittleEndian(buffer, offset + 40);
            UidGidTableStart = Utilities.ToInt64LittleEndian(buffer, offset + 48);
            ExtendedAttrsTableStart = Utilities.ToInt64LittleEndian(buffer, offset + 56);
            InodeTableStart = Utilities.ToInt64LittleEndian(buffer, offset + 64);
            DirectoryTableStart = Utilities.ToInt64LittleEndian(buffer, offset + 72);
            FragmentTableStart = Utilities.ToInt64LittleEndian(buffer, offset + 80);
            LookupTableStart = Utilities.ToInt64LittleEndian(buffer, offset + 88);

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            Utilities.WriteBytesLittleEndian(Magic, buffer, offset + 0);
            Utilities.WriteBytesLittleEndian(InodesCount, buffer, offset + 4);
            Utilities.WriteBytesLittleEndian(Utilities.DateTimeToUnix(CreationTime), buffer, offset + 8);
            Utilities.WriteBytesLittleEndian(BlockSize, buffer, offset + 12);
            Utilities.WriteBytesLittleEndian(FragmentsCount, buffer, offset + 16);
            Utilities.WriteBytesLittleEndian(Compression, buffer, offset + 20);
            Utilities.WriteBytesLittleEndian(BlockSizeLog2, buffer, offset + 22);
            Utilities.WriteBytesLittleEndian(Flags, buffer, offset + 24);
            Utilities.WriteBytesLittleEndian(UidGidCount, buffer, offset + 26);
            Utilities.WriteBytesLittleEndian(MajorVersion, buffer, offset + 28);
            Utilities.WriteBytesLittleEndian(MinorVersion, buffer, offset + 30);
            Utilities.WriteBytesLittleEndian(RootInode.Value, buffer, offset + 32);
            Utilities.WriteBytesLittleEndian(BytesUsed, buffer, offset + 40);
            Utilities.WriteBytesLittleEndian(UidGidTableStart, buffer, offset + 48);
            Utilities.WriteBytesLittleEndian(ExtendedAttrsTableStart, buffer, offset + 56);
            Utilities.WriteBytesLittleEndian(InodeTableStart, buffer, offset + 64);
            Utilities.WriteBytesLittleEndian(DirectoryTableStart, buffer, offset + 72);
            Utilities.WriteBytesLittleEndian(FragmentTableStart, buffer, offset + 80);
            Utilities.WriteBytesLittleEndian(LookupTableStart, buffer, offset + 88);
        }
    }
}

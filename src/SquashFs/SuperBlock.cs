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
        public uint CreationTime;
        public uint BlockSize;
        public uint FragmentsCount;
        public ushort Compression;
        public ushort BlockLog;
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
            CreationTime = Utilities.ToUInt32LittleEndian(buffer, offset + 8);
            BlockSize = Utilities.ToUInt32LittleEndian(buffer, offset + 12);
            FragmentsCount = Utilities.ToUInt32LittleEndian(buffer, offset + 16);
            Compression = Utilities.ToUInt16LittleEndian(buffer, offset + 20);
            BlockLog = Utilities.ToUInt16LittleEndian(buffer, offset + 22);
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

            if (Magic != SquashFsMagic)
            {
                throw new IOException("Invalid SquashFs superblock - magic mismatch");
            }

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

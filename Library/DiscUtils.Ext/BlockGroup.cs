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
using DiscUtils.Internal;

namespace DiscUtils.Ext
{
    internal class BlockGroup64 : BlockGroup
    {
        public const int DescriptorSize64 = 64;

        public uint BlockBitmapBlockHigh;
        public uint InodeBitmapBlockHigh;
        public uint InodeTableBlockHigh;
        public ushort FreeBlocksCountHigh;
        public ushort FreeInodesCountHigh;
        public ushort UsedDirsCountHigh;

        public override int Size { get { return DescriptorSize64; } }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            base.ReadFrom(buffer, offset);

            BlockBitmapBlockHigh = Utilities.ToUInt32LittleEndian(buffer, offset + 0x20);
            InodeBitmapBlockHigh = Utilities.ToUInt32LittleEndian(buffer, offset + 0x24);
            InodeTableBlockHigh = Utilities.ToUInt32LittleEndian(buffer, offset + 0x28);
            FreeBlocksCountHigh = Utilities.ToUInt16LittleEndian(buffer, offset + 0x2C);
            FreeInodesCountHigh = Utilities.ToUInt16LittleEndian(buffer, offset + 0x2E);
            UsedDirsCountHigh = Utilities.ToUInt16LittleEndian(buffer, offset + 0x30);

            return DescriptorSize64;
        }
    }

    internal class BlockGroup : IByteArraySerializable
    {
        public const int DescriptorSize = 32;

        public uint BlockBitmapBlock;
        public ushort FreeBlocksCount;
        public ushort FreeInodesCount;
        public uint InodeBitmapBlock;
        public uint InodeTableBlock;
        public ushort UsedDirsCount;

        public virtual int Size
        {
            get { return DescriptorSize; }
        }

        public virtual int ReadFrom(byte[] buffer, int offset)
        {
            BlockBitmapBlock = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
            InodeBitmapBlock = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
            InodeTableBlock = Utilities.ToUInt32LittleEndian(buffer, offset + 8);
            FreeBlocksCount = Utilities.ToUInt16LittleEndian(buffer, offset + 12);
            FreeInodesCount = Utilities.ToUInt16LittleEndian(buffer, offset + 14);
            UsedDirsCount = Utilities.ToUInt16LittleEndian(buffer, offset + 16);

            return DescriptorSize;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

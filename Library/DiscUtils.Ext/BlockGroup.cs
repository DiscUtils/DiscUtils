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

            BlockBitmapBlockHigh = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x20);
            InodeBitmapBlockHigh = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x24);
            InodeTableBlockHigh = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x28);
            FreeBlocksCountHigh = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x2C);
            FreeInodesCountHigh = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x2E);
            UsedDirsCountHigh = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x30);

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
            BlockBitmapBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
            InodeBitmapBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
            InodeTableBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8);
            FreeBlocksCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 12);
            FreeInodesCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 14);
            UsedDirsCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 16);

            return DescriptorSize;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

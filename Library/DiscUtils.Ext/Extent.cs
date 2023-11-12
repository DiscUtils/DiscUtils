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
    internal class Extent : IByteArraySerializable
    {
        public uint FirstLogicalBlock;
        public ushort FirstPhysicalBlockHi;
        public uint FirstPhysicalBlockLow;
        public ushort NumBlocks;
        public bool IsInitialized;

        public ulong FirstPhysicalBlock
        {
            get { return FirstPhysicalBlockLow | ((ulong)FirstPhysicalBlockHi << 32); }
        }

        public int Size
        {
            get { return 12; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            FirstLogicalBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
            NumBlocks = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 4);
            FirstPhysicalBlockHi = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 6);
            FirstPhysicalBlockLow = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8);

            // Mask out high-order bit of NumBlocks if needed.  See https://www.kernel.org/doc/html/v4.19/filesystems/ext4/ondisk/#extent-tree
            IsInitialized = NumBlocks <= 0x8000;
            if (!IsInitialized)
            {
                NumBlocks &= 0x7FFF;
            }

            return 12;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
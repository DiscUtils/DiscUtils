//
// Copyright (c) 2019, Bianco Veigel
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

namespace DiscUtils.Xfs
{
    using DiscUtils.Streams;
    using System;
    using System.Collections.Generic;

    internal abstract class BTreeExtentHeaderV5 : BTreeExtentHeader
    {
        public const uint BtreeMagicV5 = 0x424d4133;
        
        public ulong BlockNumber { get; private set; }
        
        public ulong LogSequenceNumber { get; private set; }
        
        public Guid Uuid { get; private set; }
        
        public ulong Owner { get; private set; }
        
        public uint Crc { get; private set; }

        public override int Size
        {
            get { return base.Size + 48; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            offset += base.ReadFrom(buffer, offset);
            BlockNumber = EndianUtilities.ToUInt64BigEndian(buffer, offset);
            LogSequenceNumber = EndianUtilities.ToUInt64BigEndian(buffer, offset + 0x8);
            Uuid = EndianUtilities.ToGuidBigEndian(buffer, offset + 0x10);
            Owner = EndianUtilities.ToUInt64BigEndian(buffer, offset + 0x20);
            Crc = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x28);
            return base.Size + 48;
        }
    }
}

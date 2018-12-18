//
// Copyright (c) 2016, Bianco Veigel
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

    internal abstract class BTreeExtentHeader : IByteArraySerializable
    {
        public const uint BtreeMagic = 0x424d4150;

        public uint Magic { get; private set; }

        public ushort Level { get; protected set; }

        public ushort NumberOfRecords { get; protected set; }

        public long LeftSibling { get; private set; }

        public long RightSibling { get; private set; }

        public virtual int Size
        {
            get { return 24; }
        }

        public virtual int ReadFrom(byte[] buffer, int offset)
        {
            Magic = EndianUtilities.ToUInt32BigEndian(buffer, offset);
            Level = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x4);
            NumberOfRecords = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x6);
            LeftSibling = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x8);
            RightSibling = EndianUtilities.ToInt64BigEndian(buffer, offset + 0xC);
            return 24;
        }

        public virtual void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public abstract void LoadBtree(Context context);

        public abstract IList<Extent> GetExtents();
    }
}

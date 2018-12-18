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
    using System.IO;

    internal class BTreeExtentNode : BTreeExtentHeader
    {
        public ulong[] Keys { get; protected set; }

        public ulong[] Pointer { get; protected set; }

        public Dictionary<ulong, BTreeExtentHeader> Children { get; protected set; }

        public override int Size
        {
            get { return base.Size + (NumberOfRecords * 0x8); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            offset += base.ReadFrom(buffer, offset);
            if (Level == 0)
                throw new IOException("invalid B+tree level - expected >= 1");
            Keys = new ulong[NumberOfRecords];
            Pointer = new ulong[NumberOfRecords];
            for (int i = 0; i < NumberOfRecords; i++)
            {
                Keys[i] = EndianUtilities.ToUInt64BigEndian(buffer, offset + i * 0x8);
            }
            offset += ((buffer.Length - offset) / 16) * 8;
            for (int i = 0; i < NumberOfRecords; i++)
            {
                Pointer[i] = EndianUtilities.ToUInt64BigEndian(buffer, offset + i * 0x8);
            }
            return Size;
        }

        public override void LoadBtree(Context context)
        {
            Children = new Dictionary<ulong, BTreeExtentHeader>(NumberOfRecords);
            for (int i = 0; i < NumberOfRecords; i++)
            {
                BTreeExtentHeader child;
                if (Level == 1)
                {
                    child = new BTreeExtentLeaf();
                }
                else
                {
                    child = new BTreeExtentNode();
                }
                var data = context.RawStream;
                data.Position = Extent.GetOffset(context, Pointer[i]);
                var buffer = StreamUtilities.ReadExact(data, (int)context.SuperBlock.Blocksize);
                child.ReadFrom(buffer, 0);
                if (child.Magic != BtreeMagic)
                {
                    throw new IOException("invalid btree directory magic");
                }
                child.LoadBtree(context);
                Children.Add(Keys[i], child);
            }
        }

        /// <inheritdoc />
        public override IList<Extent> GetExtents()
        {
            var result = new List<Extent>();
            foreach (var child in Children)
            {
                result.AddRange(child.Value.GetExtents());
            }
            return result;
        }
    }
}

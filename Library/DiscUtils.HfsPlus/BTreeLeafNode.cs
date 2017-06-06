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

using System.Collections.Generic;
using DiscUtils.Streams;

namespace DiscUtils.HfsPlus
{
    internal sealed class BTreeLeafNode<TKey> : BTreeKeyedNode<TKey>
        where TKey : BTreeKey, new()
    {
        private BTreeLeafRecord<TKey>[] _records;

        public BTreeLeafNode(BTree tree, BTreeNodeDescriptor descriptor)
            : base(tree, descriptor) {}

        public override byte[] FindKey(TKey key)
        {
            int idx = 0;
            while (idx < _records.Length)
            {
                int compResult = key.CompareTo(_records[idx].Key);
                if (compResult == 0)
                {
                    return _records[idx].Data;
                }

                if (compResult < 0)
                {
                    return null;
                }

                ++idx;
            }

            return null;
        }

        public override void VisitRange(BTreeVisitor<TKey> visitor)
        {
            int idx = 0;
            while (idx < _records.Length && visitor(_records[idx].Key, _records[idx].Data) <= 0)
            {
                idx++;
            }
        }

        protected override IList<BTreeNodeRecord> ReadRecords(byte[] buffer, int offset)
        {
            int numRecords = Descriptor.NumRecords;
            int nodeSize = Tree.NodeSize;

            _records = new BTreeLeafRecord<TKey>[numRecords];

            int start = EndianUtilities.ToUInt16BigEndian(buffer, offset + nodeSize - 2);

            for (int i = 0; i < numRecords; ++i)
            {
                int end = EndianUtilities.ToUInt16BigEndian(buffer, offset + nodeSize - (i + 2) * 2);

                _records[i] = new BTreeLeafRecord<TKey>(end - start);
                _records[i].ReadFrom(buffer, offset + start);

                start = end;
            }

            return _records;
        }
    }
}
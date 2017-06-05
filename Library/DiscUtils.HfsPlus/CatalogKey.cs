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

namespace DiscUtils.HfsPlus
{
    internal sealed class CatalogKey : BTreeKey, IComparable<CatalogKey>
    {
        private ushort _keyLength;

        public CatalogKey() {}

        public CatalogKey(CatalogNodeId nodeId, string name)
        {
            NodeId = nodeId;
            Name = name;
        }

        public string Name { get; private set; }

        public CatalogNodeId NodeId { get; private set; }

        public override int Size
        {
            get { throw new NotImplementedException(); }
        }

        public int CompareTo(CatalogKey other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (NodeId != other.NodeId)
            {
                return NodeId < other.NodeId ? -1 : 1;
            }

            return HfsPlusUtilities.FastUnicodeCompare(Name, other.Name);
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            _keyLength = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0);
            NodeId = new CatalogNodeId(EndianUtilities.ToUInt32BigEndian(buffer, offset + 2));
            Name = HfsPlusUtilities.ReadUniStr255(buffer, offset + 6);

            return _keyLength + 2;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override int CompareTo(BTreeKey other)
        {
            return CompareTo(other as CatalogKey);
        }

        public override string ToString()
        {
            return Name + " (" + NodeId + ")";
        }
    }
}
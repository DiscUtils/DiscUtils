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

namespace DiscUtils.HfsPlus
{
    using System;

    internal sealed class ExtentKey : BTreeKey, IComparable<ExtentKey>
    {
        private ushort _keyLength;
        private byte _forkType; // 0 is data, 0xff is rsrc
        private CatalogNodeId _nodeId;
        private uint _startBlock;

        public ExtentKey()
        {
        }

        public ExtentKey(CatalogNodeId cnid, uint startBlock, bool resource_fork = false)
        {
            _keyLength = 10;
            _nodeId = cnid;
            _startBlock = startBlock;
            _forkType = (byte)(resource_fork ? 0xff : 0x00);
        }

        public CatalogNodeId NodeId
        {
            get { return _nodeId; }
        }

        public override int Size
        {
            get { return 12; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            _keyLength = Utilities.ToUInt16BigEndian(buffer, offset + 0);
            _forkType = buffer[offset + 2];
            _nodeId = new CatalogNodeId(Utilities.ToUInt32BigEndian(buffer, offset + 4));
            _startBlock = Utilities.ToUInt32BigEndian(buffer, offset + 8);
            return _keyLength + 2;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override int CompareTo(BTreeKey other)
        {
            return CompareTo(other as ExtentKey);
        }

        public int CompareTo(ExtentKey other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            // Sort by file id, fork type, then starting block
            if (_nodeId != other._nodeId)
            {
                return _nodeId < other._nodeId ? -1 : 1;
            }

            if (_forkType != other._forkType)
            {
                return (_forkType < other._forkType) ? -1 : 1;
            }

            if (_startBlock != other._startBlock)
            {
                return (_startBlock < other._startBlock) ? -1 : 1;
            }

            return 0;
        }

        public override string ToString()
        {
            return "ExtentKey (" + _nodeId + " - " + _startBlock + ")";
        }
    }
}

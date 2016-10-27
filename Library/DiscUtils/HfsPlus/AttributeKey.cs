//
// Copyright (c) 2014, Quamotion
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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.HfsPlus
{
    internal class AttributeKey : BTreeKey
    {
        private ushort _keyLength;
        private ushort _pad;
        private CatalogNodeId _fileId;
        private uint _startBlock;
        private string _name;

        public AttributeKey()
        {
        }

        public AttributeKey(CatalogNodeId nodeId, string name)
        {
            _fileId = nodeId;
            _name = name;
        }

        public CatalogNodeId FileId
        {
            get { return _fileId; }
        }

        public string Name
        {
            get { return _name; }
        }

        public override int Size
        {
            get { throw new NotImplementedException(); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            _keyLength = Utilities.ToUInt16BigEndian(buffer, offset + 0);
            _pad = Utilities.ToUInt16BigEndian(buffer, offset + 2);
            _fileId = new CatalogNodeId(Utilities.ToUInt32BigEndian(buffer, offset + 4));
            _startBlock = Utilities.ToUInt32BigEndian(buffer, offset + 8);
            _name = HfsPlusUtilities.ReadUniStr255(buffer, offset + 12);

            return _keyLength + 2;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override int CompareTo(BTreeKey other)
        {
            return CompareTo(other as AttributeKey);
        }

        public int CompareTo(AttributeKey other)
        {
            if (other == null)
            {
                throw new ArgumentNullException("other");
            }

            if (_fileId != other._fileId)
            {
                return _fileId < other._fileId ? -1 : 1;
            }

            return HfsPlusUtilities.FastUnicodeCompare(_name, other._name);
        }

        public override string ToString()
        {
            return _name + " (" + _fileId + ")";
        }

    }
}

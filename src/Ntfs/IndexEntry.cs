//
// Copyright (c) 2008, Kenneth Bell
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


namespace DiscUtils.Ntfs
{
    internal class IndexEntry<K,D>
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        private ushort _dataOffset;
        private ushort _dataLength;
        private ushort _length;
        private ushort _keyLength;
        private IndexEntryFlags _flags;

        // Only valid if Node flag set
        private long _vcn;

        private K _key;

        private D _data;

        public IndexEntry(byte[] buffer, int offset)
        {
            _dataOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x00);
            _dataLength = Utilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            _length = Utilities.ToUInt16LittleEndian(buffer, offset + 0x08);
            _keyLength = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            _flags = (IndexEntryFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x0C);

            if(_keyLength > 0 && (_flags & IndexEntryFlags.End) == 0)
            {
                _key = new K();
                _key.ReadFrom(buffer, offset + 0x10);
            }

            if (typeof(D) == typeof(FileReference))
            {
                // Special case, for file indexes, the MFT ref is held where the data offset & length go
                _data = new D();
                _data.ReadFrom(buffer, offset + 0x00);
            }
            else if (_dataLength > 0)
            {
                _data = new D();
                _data.ReadFrom(buffer, offset + 0x10 + _keyLength);
            }

            if ((_flags & IndexEntryFlags.Node) != 0)
            {
                _vcn = Utilities.ToInt64LittleEndian(buffer, offset + _length - 8);
            }
        }

        public ushort Length
        {
            get { return _length; }
        }

        public IndexEntryFlags Flags
        {
            get { return _flags; }
        }

        public long ChildrenVirtualCluster
        {
            get { return _vcn; }
        }

        public K Key
        {
            get { return _key; }
        }

        public D Data
        {
            get { return _data; }
        }
    }
}

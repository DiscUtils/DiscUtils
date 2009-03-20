//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum IndexEntryFlags : ushort
    {
        None = 0x00,
        Node = 0x01,
        End = 0x02
    }

    internal class IndexEntry<K, D>
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        private bool _isFileIndexEntry;

        private IndexEntryFlags _flags;

        // Only valid if Node flag set
        private long _vcn;

        private K _key;
        private D _data;

        public IndexEntry(IndexEntry<K, D> toCopy, K newKey, D newData)
        {
            _isFileIndexEntry = toCopy._isFileIndexEntry;
            _flags = toCopy.Flags;
            _vcn = toCopy._vcn;

            _key = newKey;
            _data = newData;
        }

        public IndexEntry(K newKey, D newData)
        {
            _isFileIndexEntry = typeof(D) == typeof(FileReference);
            _flags = IndexEntryFlags.None;
            _key = newKey;
            _data = newData;
        }

        public IndexEntry(byte[] buffer, int offset)
        {
            _isFileIndexEntry = typeof(D) == typeof(FileReference);

            ushort dataOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x00);
            ushort dataLength = Utilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            ushort length = Utilities.ToUInt16LittleEndian(buffer, offset + 0x08);
            ushort keyLength = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            _flags = (IndexEntryFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x0C);

            if ((_flags & IndexEntryFlags.End) == 0)
            {
                _key = new K();
                _key.ReadFrom(buffer, offset + 0x10);

                _data = new D();
                if (_isFileIndexEntry)
                {
                    // Special case, for file indexes, the MFT ref is held where the data offset & length go
                    _data.ReadFrom(buffer, offset + 0x00);
                }
                else
                {
                    _data.ReadFrom(buffer, offset + 0x10 + keyLength);
                }
            }

            if ((_flags & IndexEntryFlags.Node) != 0)
            {
                _vcn = Utilities.ToInt64LittleEndian(buffer, offset + length - 8);
            }

            if (length != Size)
            {
                throw new Exception();
            }
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            ushort length = (ushort)Size;

            if ((_flags & IndexEntryFlags.End) == 0)
            {
                ushort keyLength = (ushort)_key.Size;

                if (_isFileIndexEntry)
                {
                    _data.WriteTo(buffer, offset + 0x00);
                }
                else
                {
                    ushort dataOffset = (ushort)(_isFileIndexEntry ? 0 : (0x10 + keyLength));
                    ushort dataLength = (ushort)_data.Size;

                    Utilities.WriteBytesLittleEndian(dataOffset, buffer, offset + 0x00);
                    Utilities.WriteBytesLittleEndian(dataLength, buffer, offset + 0x02);
                    _data.WriteTo(buffer, offset + dataOffset);
                }

                Utilities.WriteBytesLittleEndian(keyLength, buffer, offset + 0x0A);
                _key.WriteTo(buffer, offset + 0x10);
            }
            else
            {
                Utilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x00); //dataOffset
                Utilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x02); //dataLength
                Utilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x0A); //keyLength
            }

            Utilities.WriteBytesLittleEndian(length, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x0C);
            if ((_flags & IndexEntryFlags.Node) != 0)
            {
                Utilities.WriteBytesLittleEndian(_vcn, buffer, offset + length - 8);
            }
        }

        public int Size
        {
            get
            {
                int size = 0x10; // start of variable data

                if ((_flags & IndexEntryFlags.End) == 0)
                {
                    size += _key.Size;
                    size += _isFileIndexEntry ? 0 : _data.Size;
                }

                size = Utilities.RoundUp(size, 8);

                if ((_flags & IndexEntryFlags.Node) != 0)
                {
                    size += 8;
                }

                return size;
            }
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

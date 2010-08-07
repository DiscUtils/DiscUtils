//
// Copyright (c) 2008-2010, Kenneth Bell
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

    internal class IndexEntry
    {
        public const int EndNodeSize = 0x18;

        private bool _isFileIndexEntry;
        protected IndexEntryFlags _flags;
        protected long _vcn; // Only valid if Node flag set

        protected byte[] _keyBuffer;
        protected byte[] _dataBuffer;

        public IndexEntry(bool isFileIndexEntry)
        {
            _isFileIndexEntry = isFileIndexEntry;
        }

        public IndexEntry(IndexEntry toCopy, byte[] newKey, byte[] newData)
        {
            _isFileIndexEntry = toCopy._isFileIndexEntry;
            _flags = toCopy._flags;
            _vcn = toCopy._vcn;
            _keyBuffer = newKey;
            _dataBuffer = newData;
        }

        public IndexEntry(byte[] key, byte[] data, bool isFileIndexEntry)
        {
            _isFileIndexEntry = isFileIndexEntry;
            _flags = IndexEntryFlags.None;
            _keyBuffer = key;
            _dataBuffer = data;
        }

        protected bool IsFileIndexEntry
        {
            get { return _isFileIndexEntry; }
        }

        public byte[] KeyBuffer
        {
            get { return _keyBuffer; }
            set { _keyBuffer = value; }
        }

        public byte[] DataBuffer
        {
            get { return _dataBuffer; }
            set { _dataBuffer = value; }
        }

        public virtual void Read(byte[] buffer, int offset)
        {
            ushort dataOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x00);
            ushort dataLength = Utilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            ushort length = Utilities.ToUInt16LittleEndian(buffer, offset + 0x08);
            ushort keyLength = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            _flags = (IndexEntryFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x0C);

            if ((_flags & IndexEntryFlags.End) == 0)
            {
                _keyBuffer = new byte[keyLength];
                Array.Copy(buffer, offset + 0x10, _keyBuffer, 0, keyLength);

                if (IsFileIndexEntry)
                {
                    // Special case, for file indexes, the MFT ref is held where the data offset & length go
                    _dataBuffer = new byte[8];
                    Array.Copy(buffer, offset + 0x00, _dataBuffer, 0, 8);
                }
                else
                {
                    _dataBuffer = new byte[dataLength];
                    Array.Copy(buffer, offset + 0x10 + keyLength, _dataBuffer, 0, dataLength);
                }
            }

            if ((_flags & IndexEntryFlags.Node) != 0)
            {
                _vcn = Utilities.ToInt64LittleEndian(buffer, offset + length - 8);
            }
        }

        public virtual int Size
        {
            get
            {
                int size = 0x10; // start of variable data

                if ((_flags & IndexEntryFlags.End) == 0)
                {
                    size += _keyBuffer.Length;
                    size += IsFileIndexEntry ? 0 : _dataBuffer.Length;
                }

                size = Utilities.RoundUp(size, 8);

                if ((_flags & IndexEntryFlags.Node) != 0)
                {
                    size += 8;
                }

                return size;
            }
        }

        public virtual void WriteTo(byte[] buffer, int offset)
        {
            ushort length = (ushort)Size;

            if ((_flags & IndexEntryFlags.End) == 0)
            {
                ushort keyLength = (ushort)_keyBuffer.Length;

                if (IsFileIndexEntry)
                {
                    Array.Copy(_dataBuffer, 0, buffer, offset + 0x00, 8);
                }
                else
                {
                    ushort dataOffset = (ushort)(IsFileIndexEntry ? 0 : (0x10 + keyLength));
                    ushort dataLength = (ushort)_dataBuffer.Length;

                    Utilities.WriteBytesLittleEndian(dataOffset, buffer, offset + 0x00);
                    Utilities.WriteBytesLittleEndian(dataLength, buffer, offset + 0x02);
                    Array.Copy(_dataBuffer, 0, buffer, offset + dataOffset, _dataBuffer.Length);
                }

                Utilities.WriteBytesLittleEndian(keyLength, buffer, offset + 0x0A);
                Array.Copy(_keyBuffer, 0, buffer, offset + 0x10, _keyBuffer.Length);
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

        public IndexEntryFlags Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public long ChildrenVirtualCluster
        {
            get { return _vcn; }
            set { _vcn = value; }
        }
    }
}

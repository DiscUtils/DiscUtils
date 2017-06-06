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

namespace DiscUtils.Ntfs
{
    internal class IndexEntry
    {
        public const int EndNodeSize = 0x18;
        protected byte[] _dataBuffer;

        protected IndexEntryFlags _flags;

        protected byte[] _keyBuffer;
        protected long _vcn; // Only valid if Node flag set

        public IndexEntry(bool isFileIndexEntry)
        {
            IsFileIndexEntry = isFileIndexEntry;
        }

        public IndexEntry(IndexEntry toCopy, byte[] newKey, byte[] newData)
        {
            IsFileIndexEntry = toCopy.IsFileIndexEntry;
            _flags = toCopy._flags;
            _vcn = toCopy._vcn;
            _keyBuffer = newKey;
            _dataBuffer = newData;
        }

        public IndexEntry(byte[] key, byte[] data, bool isFileIndexEntry)
        {
            IsFileIndexEntry = isFileIndexEntry;
            _flags = IndexEntryFlags.None;
            _keyBuffer = key;
            _dataBuffer = data;
        }

        public long ChildrenVirtualCluster
        {
            get { return _vcn; }
            set { _vcn = value; }
        }

        public byte[] DataBuffer
        {
            get { return _dataBuffer; }
            set { _dataBuffer = value; }
        }

        public IndexEntryFlags Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        protected bool IsFileIndexEntry { get; }

        public byte[] KeyBuffer
        {
            get { return _keyBuffer; }
            set { _keyBuffer = value; }
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

                size = MathUtilities.RoundUp(size, 8);

                if ((_flags & IndexEntryFlags.Node) != 0)
                {
                    size += 8;
                }

                return size;
            }
        }

        public virtual void Read(byte[] buffer, int offset)
        {
            ushort dataOffset = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x00);
            ushort dataLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            ushort length = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x08);
            ushort keyLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            _flags = (IndexEntryFlags)EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x0C);

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
                _vcn = EndianUtilities.ToInt64LittleEndian(buffer, offset + length - 8);
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
                    ushort dataOffset = (ushort)(IsFileIndexEntry ? 0 : 0x10 + keyLength);
                    ushort dataLength = (ushort)_dataBuffer.Length;

                    EndianUtilities.WriteBytesLittleEndian(dataOffset, buffer, offset + 0x00);
                    EndianUtilities.WriteBytesLittleEndian(dataLength, buffer, offset + 0x02);
                    Array.Copy(_dataBuffer, 0, buffer, offset + dataOffset, _dataBuffer.Length);
                }

                EndianUtilities.WriteBytesLittleEndian(keyLength, buffer, offset + 0x0A);
                Array.Copy(_keyBuffer, 0, buffer, offset + 0x10, _keyBuffer.Length);
            }
            else
            {
                EndianUtilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x00); // dataOffset
                EndianUtilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x02); // dataLength
                EndianUtilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x0A); // keyLength
            }

            EndianUtilities.WriteBytesLittleEndian(length, buffer, offset + 0x08);
            EndianUtilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x0C);
            if ((_flags & IndexEntryFlags.Node) != 0)
            {
                EndianUtilities.WriteBytesLittleEndian(_vcn, buffer, offset + length - 8);
            }
        }
    }
}
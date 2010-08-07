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

namespace DiscUtils.Partitions
{
    internal class BiosPartitionRecord : IComparable<BiosPartitionRecord>
    {
        private uint _lbaOffset;

        private byte _status;
        private ushort _startCylinder;
        private byte _startHead;
        private byte _startSector;
        private byte _type;
        private ushort _endCylinder;
        private byte _endHead;
        private byte _endSector;
        private uint _lbaStart;
        private uint _lbaLength;
        private int _index;

        public BiosPartitionRecord()
        {
        }

        public BiosPartitionRecord(byte[] data, int offset, uint lbaOffset, int index)
        {
            _lbaOffset = lbaOffset;

            _status = data[offset];
            _startHead = data[offset + 1];
            _startSector = (byte)(data[offset + 2] & 0x3F);
            _startCylinder = (ushort)(data[offset + 3] | ((data[offset + 2] & 0xC0) << 2));
            _type = data[offset + 4];
            _endHead = data[offset + 5];
            _endSector = (byte)(data[offset + 6] & 0x3F);
            _endCylinder = (ushort)(data[offset + 7] | ((data[offset + 6] & 0xC0) << 2));
            _lbaStart = Utilities.ToUInt32LittleEndian(data, offset + 8);
            _lbaLength = Utilities.ToUInt32LittleEndian(data, offset + 12);
            _index = index;
        }

        public bool IsValid
        {
            get
            {
                return _endHead != 0 || _endSector != 0 || _endCylinder != 0 || _lbaLength != 0;
            }
        }

        public byte Status
        {
            get { return _status; }
            set { _status = value; }
        }

        public ushort StartCylinder
        {
            get { return _startCylinder; }
            set { _startCylinder = value; }
        }

        public byte StartHead
        {
            get { return _startHead; }
            set { _startHead = value; }
        }

        public byte StartSector
        {
            get { return _startSector; }
            set { _startSector = value; }
        }

        public byte PartitionType
        {
            get { return _type; }
            set { _type = value; }
        }

        public string FriendlyPartitionType
        {
            get { return BiosPartitionTypes.ToString(_type); }
        }

        public ushort EndCylinder
        {
            get { return _endCylinder; }
            set { _endCylinder = value; }
        }

        public byte EndHead
        {
            get { return _endHead; }
            set { _endHead = value; }
        }

        public byte EndSector
        {
            get { return _endSector; }
            set { _endSector = value; }
        }

        public uint LBAStart
        {
            get { return _lbaStart; }
            set { _lbaStart = value; }
        }

        public uint LBALength
        {
            get { return _lbaLength; }
            set { _lbaLength = value; }
        }

        public uint LBAStartAbsolute
        {
            get { return _lbaStart + _lbaOffset; }
        }

        public int Index
        {
            get { return _index; }
        }

        internal void WriteTo(byte[] buffer, int offset)
        {
            buffer[offset] = _status;
            buffer[offset + 1] = _startHead;
            buffer[offset + 2] = (byte)((_startSector & 0x3F) | ((_startCylinder >> 2) & 0xC0));
            buffer[offset + 3] = (byte)_startCylinder;
            buffer[offset + 4] = _type;
            buffer[offset + 5] = _endHead;
            buffer[offset + 6] = (byte)((_endSector & 0x3F) | ((_endCylinder >> 2) & 0xC0));
            buffer[offset + 7] = (byte)_endCylinder;
            Utilities.WriteBytesLittleEndian((uint)_lbaStart, buffer, offset + 8);
            Utilities.WriteBytesLittleEndian((uint)_lbaLength, buffer, offset + 12);
        }

        #region IComparable<BiosPartitionRecord> Members

        public int CompareTo(BiosPartitionRecord other)
        {
            return LBAStartAbsolute.CompareTo(other.LBAStartAbsolute);
        }

        #endregion
    }
}

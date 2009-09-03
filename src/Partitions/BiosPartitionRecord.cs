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
            get
            {
                switch (_type)
                {
                    case 0x00: return "Unused";
                    case 0x01: return "FAT12";
                    case 0x02: return "XENIX root";
                    case 0x03: return "XENIX /usr";
                    case 0x04: return "FAT16 (<32M)";
                    case 0x05: return "Extended (non-LBA)";
                    case 0x06: return "FAT16 (>32M)";
                    case 0x07: return "IFS (NTFS or HPFS)";
                    case 0x0B: return "FAT32 (non-LBA)";
                    case 0x0C: return "FAT32 (LBA)";
                    case 0x0E: return "FAT16 (LBA)";
                    case 0x0F: return "Extended (LBA)";
                    case 0x11: return "Hidden FAT12";
                    case 0x12: return "Vendor Config/Recovery/Diagnostics";
                    case 0x14: return "Hidden FAT16 (<32M)";
                    case 0x16: return "Hidden FAT16 (>32M)";
                    case 0x17: return "Hidden IFS (NTFS or HPFS)";
                    case 0x1B: return "Hidden FAT32 (non-LBA)";
                    case 0x1C: return "Hidden FAT32 (LBA)";
                    case 0x1E: return "Hidden FAT16 (LBA)";
                    case 0x27: return "Windows Recovery Environment";
                    case 0x42: return "Windows Dynamic Volume";
                    case 0x80: return "Minix v1.1 - v1.4a";
                    case 0x81: return "Minix / Early Linux";
                    case 0x82: return "Linux Swap";
                    case 0x83: return "Linux Native";
                    case 0x84: return "Hibernation";
                    case 0x8E: return "Linux LVM";
                    case 0xA0: return "Laptop Hibernation";
                    case 0xA8: return "Mac OS-X";
                    case 0xAB: return "Mac OS-X Boot";
                    case 0xAF: return "Mac OS-X HFS";
                    case 0xC0: return "NTFT";
                    case 0xDE: return "Dell OEM";
                    case 0xEE: return "GPT Protective";
                    case 0xEF: return "EFI";
                    case 0xFB: return "VMWare File System";
                    case 0xFC: return "VMWare Swap";
                    case 0xFE: return "IBM OEM";
                    default: return "Unknown";
                }
            }
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

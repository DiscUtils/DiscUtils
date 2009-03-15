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
using System.IO;

namespace DiscUtils.Ntfs
{
    internal abstract class FixupRecordBase
    {
        private int _sectorSize;

        private string _magic;
        private ushort _updateSequenceOffset;
        private ushort _updateSequenceSize;

        private ushort _updateSequenceNumber;
        private ushort[] _updateSequenceArray;

        public FixupRecordBase(int sectorSize)
        {
            _sectorSize = sectorSize;
        }

        public string Magic
        {
            get { return _magic; }
            set { _magic = value; }
        }

        public ushort UpdateSequenceOffset
        {
            get { return _updateSequenceOffset; }
            set { _updateSequenceOffset = value; }
        }

        public ushort UpdateSequenceSize
        {
            get { return _updateSequenceSize; }
        }

        public ushort UpdateSequenceNumber
        {
            get { return _updateSequenceNumber; }
        }

        public void FromBytes(byte[] buffer, int offset)
        {
            _magic = Utilities.BytesToString(buffer, offset + 0x00, 4);
            _updateSequenceOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x04);
            _updateSequenceSize = Utilities.ToUInt16LittleEndian(buffer, offset + 0x06);

            _updateSequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + _updateSequenceOffset);
            _updateSequenceArray = new ushort[_updateSequenceSize - 1];
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                _updateSequenceArray[i] = Utilities.ToUInt16LittleEndian(buffer, offset + _updateSequenceOffset + 2 * (i + 1));
            }

            UnprotectBuffer(buffer, offset);

            Read(buffer, offset);
        }

        public int Size
        {
            get
            {
                return CalcSize(_updateSequenceSize * 2);
            }
        }

        public void ToBytes(byte[] buffer, int offset)
        {
            _updateSequenceOffset = Write(buffer, offset, (ushort)(_updateSequenceSize * 2));

            ProtectBuffer(buffer, offset);

            Utilities.StringToBytes(_magic, buffer, offset + 0x00, 4);
            Utilities.WriteBytesLittleEndian(_updateSequenceOffset, buffer, offset + 0x04);
            Utilities.WriteBytesLittleEndian(_updateSequenceSize, buffer, offset + 0x06);

            Utilities.WriteBytesLittleEndian(_updateSequenceNumber, buffer, offset + _updateSequenceOffset);
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                Utilities.WriteBytesLittleEndian(_updateSequenceArray[i], buffer, offset + _updateSequenceOffset + 2 * (i + 1));
            }
        }

        protected abstract void Read(byte[] buffer, int offset);
        protected abstract ushort Write(byte[] buffer, int offset, ushort updateSeqSize);
        protected abstract int CalcSize(int updateSeqSize);

        private void UnprotectBuffer(byte[] buffer, int offset)
        {
            // First do validation check - make sure the USN matches on all sectors)
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                if (_updateSequenceNumber != Utilities.ToUInt16LittleEndian(buffer, offset + (_sectorSize * (i + 1)) - 2))
                {
                    throw new IOException("Corrupt file system record found");
                }
            }

            // Now replace the USNs with the actual data from the sequence array
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                Utilities.WriteBytesLittleEndian(_updateSequenceArray[i], buffer, offset + (_sectorSize * (i + 1)) - 2);
            }
        }

        private void ProtectBuffer(byte[] buffer, int offset)
        {
            _updateSequenceNumber++;

            // Read in the bytes that are replaced by the USN
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                _updateSequenceArray[i] = Utilities.ToUInt16LittleEndian(buffer, offset + (_sectorSize * (i + 1)) - 2);
            }

            // Overwrite the bytes that are replaced with the USN
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                Utilities.WriteBytesLittleEndian(_updateSequenceNumber, buffer, offset + (_sectorSize * (i + 1)) - 2);
            }
        }
    }
}

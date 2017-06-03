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

using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal abstract class FixupRecordBase
    {
        private int _sectorSize;
        private ushort[] _updateSequenceArray;

        public FixupRecordBase(string magic, int sectorSize)
        {
            Magic = magic;
            _sectorSize = sectorSize;
        }

        public FixupRecordBase(string magic, int sectorSize, int recordLength)
        {
            Initialize(magic, sectorSize, recordLength);
        }

        public string Magic { get; private set; }

        public int Size
        {
            get { return CalcSize(); }
        }

        public ushort UpdateSequenceCount { get; private set; }

        public ushort UpdateSequenceNumber { get; private set; }

        public ushort UpdateSequenceOffset { get; private set; }

        public int UpdateSequenceSize
        {
            get { return UpdateSequenceCount * 2; }
        }

        public void FromBytes(byte[] buffer, int offset)
        {
            FromBytes(buffer, offset, false);
        }

        public void FromBytes(byte[] buffer, int offset, bool ignoreMagic)
        {
            string diskMagic = EndianUtilities.BytesToString(buffer, offset + 0x00, 4);
            if (Magic == null)
            {
                Magic = diskMagic;
            }
            else
            {
                if (diskMagic != Magic && ignoreMagic)
                {
                    return;
                }

                if (diskMagic != Magic)
                {
                    throw new IOException("Corrupt record");
                }
            }

            UpdateSequenceOffset = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x04);
            UpdateSequenceCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x06);

            UpdateSequenceNumber = EndianUtilities.ToUInt16LittleEndian(buffer, offset + UpdateSequenceOffset);
            _updateSequenceArray = new ushort[UpdateSequenceCount - 1];
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                _updateSequenceArray[i] = EndianUtilities.ToUInt16LittleEndian(buffer,
                    offset + UpdateSequenceOffset + 2 * (i + 1));
            }

            UnprotectBuffer(buffer, offset);

            Read(buffer, offset);
        }

        public void ToBytes(byte[] buffer, int offset)
        {
            UpdateSequenceOffset = Write(buffer, offset);

            ProtectBuffer(buffer, offset);

            EndianUtilities.StringToBytes(Magic, buffer, offset + 0x00, 4);
            EndianUtilities.WriteBytesLittleEndian(UpdateSequenceOffset, buffer, offset + 0x04);
            EndianUtilities.WriteBytesLittleEndian(UpdateSequenceCount, buffer, offset + 0x06);

            EndianUtilities.WriteBytesLittleEndian(UpdateSequenceNumber, buffer, offset + UpdateSequenceOffset);
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian(_updateSequenceArray[i], buffer,
                    offset + UpdateSequenceOffset + 2 * (i + 1));
            }
        }

        protected void Initialize(string magic, int sectorSize, int recordLength)
        {
            Magic = magic;
            _sectorSize = sectorSize;
            UpdateSequenceCount = (ushort)(1 + MathUtilities.Ceil(recordLength, Sizes.Sector));
            UpdateSequenceNumber = 1;
            _updateSequenceArray = new ushort[UpdateSequenceCount - 1];
        }

        protected abstract void Read(byte[] buffer, int offset);

        protected abstract ushort Write(byte[] buffer, int offset);

        protected abstract int CalcSize();

        private void UnprotectBuffer(byte[] buffer, int offset)
        {
            // First do validation check - make sure the USN matches on all sectors)
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                if (UpdateSequenceNumber != EndianUtilities.ToUInt16LittleEndian(buffer, offset + Sizes.Sector * (i + 1) - 2))
                {
                    throw new IOException("Corrupt file system record found");
                }
            }

            // Now replace the USNs with the actual data from the sequence array
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian(_updateSequenceArray[i], buffer, offset + Sizes.Sector * (i + 1) - 2);
            }
        }

        private void ProtectBuffer(byte[] buffer, int offset)
        {
            UpdateSequenceNumber++;

            // Read in the bytes that are replaced by the USN
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                _updateSequenceArray[i] = EndianUtilities.ToUInt16LittleEndian(buffer, offset + Sizes.Sector * (i + 1) - 2);
            }

            // Overwrite the bytes that are replaced with the USN
            for (int i = 0; i < _updateSequenceArray.Length; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian(UpdateSequenceNumber, buffer, offset + Sizes.Sector * (i + 1) - 2);
            }
        }
    }
}
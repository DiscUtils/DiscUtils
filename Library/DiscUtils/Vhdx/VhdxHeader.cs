//
// Copyright (c) 2008-2012, Kenneth Bell
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

namespace DiscUtils.Vhdx
{
    using System;

    internal sealed class VhdxHeader : IByteArraySerializable
    {
        public const uint VhdxHeaderSignature = 0x64616568;

        public uint Signature = VhdxHeaderSignature;
        public uint Checksum;
        public ulong SequenceNumber;
        public Guid FileWriteGuid;
        public Guid DataWriteGuid;
        public Guid LogGuid;
        public ushort LogVersion;
        public ushort Version;
        public uint LogLength;
        public ulong LogOffset;
        private byte[] _data = new byte[4096];

        public VhdxHeader()
        {
        }

        public VhdxHeader(VhdxHeader header)
        {
            Array.Copy(header._data, _data, 4096);

            Signature = header.Signature;
            Checksum = header.Checksum;
            SequenceNumber = header.SequenceNumber;
            FileWriteGuid = header.FileWriteGuid;
            DataWriteGuid = header.DataWriteGuid;
            LogGuid = header.LogGuid;
            LogVersion = header.LogVersion;
            Version = header.Version;
            LogLength = header.LogLength;
            LogOffset = header.LogOffset;
        }

        public int Size
        {
            get { return (int)(4 * Sizes.OneKiB); }
        }

        public bool IsValid
        {
            get
            {
                if (Signature != VhdxHeaderSignature)
                {
                    return false;
                }

                byte[] checkData = new byte[4096];
                Array.Copy(_data, checkData, 4096);
                Utilities.WriteBytesLittleEndian((uint)0, checkData, 4);
                return Checksum == Crc32LittleEndian.Compute(Crc32Algorithm.Castagnoli, checkData, 0, 4096);
            }
        }

        public void CalcChecksum()
        {
            Checksum = 0;
            RefreshData();
            Checksum = Crc32LittleEndian.Compute(Crc32Algorithm.Castagnoli, _data, 0, 4096);
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Array.Copy(buffer, offset, _data, 0, 4096);

            Signature = Utilities.ToUInt32LittleEndian(_data, 0);
            Checksum = Utilities.ToUInt32LittleEndian(_data, 4);

            SequenceNumber = Utilities.ToUInt64LittleEndian(_data, 8);
            FileWriteGuid = Utilities.ToGuidLittleEndian(_data, 16);
            DataWriteGuid = Utilities.ToGuidLittleEndian(_data, 32);
            LogGuid = Utilities.ToGuidLittleEndian(_data, 48);
            LogVersion = Utilities.ToUInt16LittleEndian(_data, 64);
            Version = Utilities.ToUInt16LittleEndian(_data, 66);
            LogLength = Utilities.ToUInt32LittleEndian(_data, 68);
            LogOffset = Utilities.ToUInt64LittleEndian(_data, 72);

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            RefreshData();
            Array.Copy(_data, 0, buffer, offset, (int)(4 * Sizes.OneKiB));
        }

        private void RefreshData()
        {
            Utilities.WriteBytesLittleEndian(Signature, _data, 0);
            Utilities.WriteBytesLittleEndian(Checksum, _data, 4);
            Utilities.WriteBytesLittleEndian(SequenceNumber, _data, 8);
            Utilities.WriteBytesLittleEndian(FileWriteGuid, _data, 16);
            Utilities.WriteBytesLittleEndian(DataWriteGuid, _data, 32);
            Utilities.WriteBytesLittleEndian(LogGuid, _data, 48);
            Utilities.WriteBytesLittleEndian(LogVersion, _data, 64);
            Utilities.WriteBytesLittleEndian(Version, _data, 66);
            Utilities.WriteBytesLittleEndian(LogLength, _data, 68);
            Utilities.WriteBytesLittleEndian(LogOffset, _data, 72);
        }
    }
}

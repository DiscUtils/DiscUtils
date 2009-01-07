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
    internal class GptHeader
    {
        public const string GptSignature = "EFI PART";

        public string Signature;
        public uint Version;
        public int HeaderSize;
        public uint Crc;
        public long HeaderLba;
        public long AlternateHeaderLba;
        public long FirstUsable;
        public long LastUsable;
        public Guid DiskGuid;
        public long PartitionEntriesLba;
        public uint PartitionEntryCount;
        public int PartitionEntrySize;
        public uint EntriesCrc;

        public bool ReadFrom(byte[] buffer, int offset, int count)
        {
            Signature = Utilities.BytesToString(buffer, offset + 0, 8);
            Version = Utilities.ToUInt32LittleEndian(buffer, offset + 8);
            HeaderSize = Utilities.ToInt32LittleEndian(buffer, offset + 12);
            Crc = Utilities.ToUInt32LittleEndian(buffer, offset + 16);
            HeaderLba = Utilities.ToInt64LittleEndian(buffer, offset + 24);
            AlternateHeaderLba = Utilities.ToInt64LittleEndian(buffer, offset + 32);
            FirstUsable = Utilities.ToInt64LittleEndian(buffer, offset + 40);
            LastUsable = Utilities.ToInt64LittleEndian(buffer, offset + 48);
            DiskGuid = Utilities.ToGuidLittleEndian(buffer, offset + 56);
            PartitionEntriesLba = Utilities.ToInt64LittleEndian(buffer, offset + 72);
            PartitionEntryCount = Utilities.ToUInt32LittleEndian(buffer, offset + 80);
            PartitionEntrySize = Utilities.ToInt32LittleEndian(buffer, offset + 84);
            EntriesCrc = Utilities.ToUInt32LittleEndian(buffer, offset + 88);

            return (Crc == CalcCrc(buffer, offset, HeaderSize));
        }


        private uint CalcCrc(byte[] buffer, int offset, int count)
        {
            byte[] temp = new byte[count];
            Array.Copy(buffer, offset, temp, 0, count);

            // Reset CRC field
            Utilities.WriteBytesLittleEndian((uint)0, temp, 16);

            return Crc32.Compute(0xFFFFFFFF, temp, 0, count) ^ 0xFFFFFFFF;
        }
    }
}

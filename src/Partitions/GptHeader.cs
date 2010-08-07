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

        public byte[] Buffer;

        public GptHeader(int sectorSize)
        {
            Signature = GptSignature;
            Version = 0x00010000;
            HeaderSize = 92;
            Buffer = new byte[sectorSize];
        }

        public GptHeader(GptHeader toCopy)
        {
            Signature = toCopy.Signature;
            Version = toCopy.Version;
            HeaderSize = toCopy.HeaderSize;
            Crc = toCopy.Crc;
            HeaderLba = toCopy.HeaderLba;
            AlternateHeaderLba = toCopy.AlternateHeaderLba;
            FirstUsable = toCopy.FirstUsable;
            LastUsable = toCopy.LastUsable;
            DiskGuid = toCopy.DiskGuid;
            PartitionEntriesLba = toCopy.PartitionEntriesLba;
            PartitionEntryCount = toCopy.PartitionEntryCount;
            PartitionEntrySize = toCopy.PartitionEntrySize;
            EntriesCrc = toCopy.EntriesCrc;

            Buffer = new byte[toCopy.Buffer.Length];
            Array.Copy(toCopy.Buffer, Buffer, Buffer.Length);
        }


        public bool ReadFrom(byte[] buffer, int offset)
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

            // In case the header has new fields unknown to us, store the entire header
            // as a byte array
            Buffer = new byte[HeaderSize];
            Array.Copy(buffer, offset, Buffer, 0, HeaderSize);

            // Reject obviously invalid data
            if (Signature != GptSignature || HeaderSize == 0)
            {
                return false;
            }

            return (Crc == CalcCrc(buffer, offset, HeaderSize));
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            // First, copy the cached header to allow for unknown fields
            Array.Copy(Buffer, 0, buffer, offset, Buffer.Length);

            // Next, write the fields
            Utilities.StringToBytes(Signature, buffer, offset + 0, 8);
            Utilities.WriteBytesLittleEndian(Version, buffer, offset + 8);
            Utilities.WriteBytesLittleEndian(HeaderSize, buffer, offset + 12);
            Utilities.WriteBytesLittleEndian((uint)0, buffer, offset + 16);
            Utilities.WriteBytesLittleEndian(HeaderLba, buffer, offset + 24);
            Utilities.WriteBytesLittleEndian(AlternateHeaderLba, buffer, offset + 32);
            Utilities.WriteBytesLittleEndian(FirstUsable, buffer, offset + 40);
            Utilities.WriteBytesLittleEndian(LastUsable, buffer, offset + 48);
            Utilities.WriteBytesLittleEndian(DiskGuid, buffer, offset + 56);
            Utilities.WriteBytesLittleEndian(PartitionEntriesLba, buffer, offset + 72);
            Utilities.WriteBytesLittleEndian(PartitionEntryCount, buffer, offset + 80);
            Utilities.WriteBytesLittleEndian(PartitionEntrySize, buffer, offset + 84);
            Utilities.WriteBytesLittleEndian(EntriesCrc, buffer, offset + 88);

            // Calculate & write the CRC
            Utilities.WriteBytesLittleEndian(CalcCrc(buffer, offset, HeaderSize), buffer, offset + 16);

            // Update the cached copy - re-allocate the buffer to allow for HeaderSize potentially having changed
            Buffer = new byte[HeaderSize];
            Array.Copy(buffer, offset, Buffer, 0, HeaderSize);
        }

        internal static uint CalcCrc(byte[] buffer, int offset, int count)
        {
            byte[] temp = new byte[count];
            Array.Copy(buffer, offset, temp, 0, count);

            // Reset CRC field
            Utilities.WriteBytesLittleEndian((uint)0, temp, 16);

            return Crc32.Compute(0xFFFFFFFF, temp, 0, count) ^ 0xFFFFFFFF;
        }
    }
}

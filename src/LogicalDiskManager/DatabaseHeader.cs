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

namespace DiscUtils.LogicalDiskManager
{
    internal class DatabaseHeader
    {
        public string Signature; // VMDB
        public uint NumVBlks; // 00 00 17 24
        public uint BlockSize; // 00 00 00 80
        public uint HeaderSize; // 00 00 02 00
        public ushort Unknown1; // 00 01
        public ushort VersionNum; // 00 04
        public ushort VersionDenom; // 00 0a
        public string GroupName;
        public string DiskGroupId;
        public long CommittedSequence; // 0xA
        public long PendingSequence; //0xA
        public uint Unknown2; // 1
        public uint Unknown3; // 1
        public uint Unknown4; // 3
        public uint Unknown5; // 3
        public long Unknown6; // 0
        public long Unknown7; // 1
        public uint Unknown8; // 1
        public uint Unknown9; // 3
        public uint UnknownA; // 3
        public long UnknownB; // 0
        public uint UnknownC; // 0
        public DateTime Timestamp;

        public void ReadFrom(byte[] buffer, int offset)
        {
            Signature = Utilities.BytesToString(buffer, offset + 0x00, 4);
            NumVBlks = Utilities.ToUInt32BigEndian(buffer, offset + 0x04);
            BlockSize = Utilities.ToUInt32BigEndian(buffer, offset + 0x08);
            HeaderSize = Utilities.ToUInt32BigEndian(buffer, offset + 0x0C);
            Unknown1 = Utilities.ToUInt16BigEndian(buffer, offset + 0x10);
            VersionNum = Utilities.ToUInt16BigEndian(buffer, offset + 0x12);
            VersionDenom = Utilities.ToUInt16BigEndian(buffer, offset + 0x14);
            GroupName = Utilities.BytesToString(buffer, offset + 0x16, 31).Trim('\0');
            DiskGroupId = Utilities.BytesToString(buffer, offset + 0x35, 0x40).Trim('\0');

            // May be wrong way round...
            CommittedSequence = Utilities.ToInt64BigEndian(buffer, offset + 0x75);
            PendingSequence = Utilities.ToInt64BigEndian(buffer, offset + 0x7D);

            Unknown2 = Utilities.ToUInt32BigEndian(buffer, offset + 0x85);
            Unknown3 = Utilities.ToUInt32BigEndian(buffer, offset + 0x89);
            Unknown4 = Utilities.ToUInt32BigEndian(buffer, offset + 0x8D);
            Unknown5 = Utilities.ToUInt32BigEndian(buffer, offset + 0x91);
            Unknown6 = Utilities.ToInt64BigEndian(buffer, offset + 0x95);
            Unknown7 = Utilities.ToInt64BigEndian(buffer, offset + 0x9D);
            Unknown8 = Utilities.ToUInt32BigEndian(buffer, offset + 0xA5);
            Unknown9 = Utilities.ToUInt32BigEndian(buffer, offset + 0xA9);
            UnknownA = Utilities.ToUInt32BigEndian(buffer, offset + 0xAD);

            UnknownB = Utilities.ToInt64BigEndian(buffer, offset + 0xB1);
            UnknownC = Utilities.ToUInt32BigEndian(buffer, offset + 0xB9);

            Timestamp = DateTime.FromFileTimeUtc(Utilities.ToInt64BigEndian(buffer, offset + 0xBD));
        }

        //private static int CalcChecksum()
        //{
        //    // Zero checksum bytes (0x08, 4)
        //    // Add all byte values for ?? bytes
        //    throw new NotImplementedException();
        //}
    }
}

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

namespace DiscUtils.LogicalDiskManager
{
    internal class DatabaseHeader
    {
        public uint BlockSize; // 00 00 00 80
        public long CommittedSequence; // 0xA
        public string DiskGroupId;
        public string GroupName;
        public uint HeaderSize; // 00 00 02 00
        public uint NumVBlks; // 00 00 17 24
        public long PendingSequence; // 0xA
        public string Signature; // VMDB
        public DateTime Timestamp;
        public ushort Unknown1; // 00 01
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
        public ushort VersionDenom; // 00 0a
        public ushort VersionNum; // 00 04

        public void ReadFrom(byte[] buffer, int offset)
        {
            Signature = EndianUtilities.BytesToString(buffer, offset + 0x00, 4);
            NumVBlks = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x04);
            BlockSize = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x08);
            HeaderSize = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x0C);
            Unknown1 = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x10);
            VersionNum = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x12);
            VersionDenom = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x14);
            GroupName = EndianUtilities.BytesToString(buffer, offset + 0x16, 31).Trim('\0');
            DiskGroupId = EndianUtilities.BytesToString(buffer, offset + 0x35, 0x40).Trim('\0');

            // May be wrong way round...
            CommittedSequence = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x75);
            PendingSequence = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x7D);

            Unknown2 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x85);
            Unknown3 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x89);
            Unknown4 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x8D);
            Unknown5 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x91);
            Unknown6 = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x95);
            Unknown7 = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x9D);
            Unknown8 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0xA5);
            Unknown9 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0xA9);
            UnknownA = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0xAD);

            UnknownB = EndianUtilities.ToInt64BigEndian(buffer, offset + 0xB1);
            UnknownC = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0xB9);

            Timestamp = DateTime.FromFileTimeUtc(EndianUtilities.ToInt64BigEndian(buffer, offset + 0xBD));
        }

        ////}
        ////    throw new NotImplementedException();
        ////    // Add all byte values for ?? bytes
        ////    // Zero checksum bytes (0x08, 4)
        ////{

        ////private static int CalcChecksum()
    }
}
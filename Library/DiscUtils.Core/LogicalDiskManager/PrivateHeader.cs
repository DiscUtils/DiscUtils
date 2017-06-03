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
    internal class PrivateHeader
    {
        public uint Checksum; // 00 00 2f 96
        public long ConfigSizeLba;
        public long ConfigurationSizeLba; // 08 00
        public long ConfigurationStartLba; // 03 FF F8 00
        public long DataSizeLba; // 03 FF F7 C1
        public long DataStartLba; // 3F
        public string DiskGroupId; // GUID string
        public string DiskGroupName; // MAX_COMPUTER_NAME_LENGTH?
        public string DiskId; // GUID string
        public string HostId; // GUID string
        public long LogSizeLba;
        public long NextTocLba;
        public long NumberOfConfigs;
        public long NumberOfLogs;
        public string Signature; // PRIVHEAD
        public DateTime Timestamp;
        public long TocSizeLba;
        public long Unknown2; // Active TOC? 00 .. 00 01
        public long Unknown3; // 00 .. 07 ff  // 1 sector less than 2MB
        public long Unknown4; // 00 .. 07 40
        public uint Unknown5; // Sector Size?
        public uint Version; // 2.12

        public void ReadFrom(byte[] buffer, int offset)
        {
            Signature = EndianUtilities.BytesToString(buffer, offset + 0x00, 8);
            Checksum = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x08);
            Version = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x0C);
            Timestamp = DateTime.FromFileTimeUtc(EndianUtilities.ToInt64BigEndian(buffer, offset + 0x10));
            Unknown2 = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x18);
            Unknown3 = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x20);
            Unknown4 = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x28);
            DiskId = EndianUtilities.BytesToString(buffer, offset + 0x30, 0x40).Trim('\0');
            HostId = EndianUtilities.BytesToString(buffer, offset + 0x70, 0x40).Trim('\0');
            DiskGroupId = EndianUtilities.BytesToString(buffer, offset + 0xB0, 0x40).Trim('\0');
            DiskGroupName = EndianUtilities.BytesToString(buffer, offset + 0xF0, 31).Trim('\0');
            Unknown5 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x10F);
            DataStartLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x11B);
            DataSizeLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x123);
            ConfigurationStartLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x12B);
            ConfigurationSizeLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x133);
            TocSizeLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x13B);
            NextTocLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x143);

            // These two may be reversed
            NumberOfConfigs = EndianUtilities.ToInt32BigEndian(buffer, offset + 0x14B);
            NumberOfLogs = EndianUtilities.ToInt32BigEndian(buffer, offset + 0x14F);

            ConfigSizeLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x153);
            LogSizeLba = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x15B);
        }

        ////}
        ////    throw new NotImplementedException();
        ////    // Add all byte values for 512 bytes
        ////    // Zero checksum bytes (0x08, 4)
        ////{

        ////private static int CalcChecksum()
    }
}
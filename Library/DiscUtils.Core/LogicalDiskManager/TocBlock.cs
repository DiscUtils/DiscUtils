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

using DiscUtils.Streams;

namespace DiscUtils.LogicalDiskManager
{
    internal class TocBlock
    {
        public uint Checksum; // 00 00 08 B6
        public long Item1Size; // Unit?
        public long Item1Start; // Sector Offset from ConfigurationStart
        public string Item1Str; // 'config', length 10
        public long Item2Size; // Unit?
        public long Item2Start; // Sector Offset from ConfigurationStart
        public string Item2Str; // 'log', length 10
        public long SequenceNumber; // 00 .. 01
        public string Signature; // TOCBLOCK
        public long Unknown1; // 0
        public long Unknown2; // 00
        public uint Unknown3; // 00 06 00 01  (may be two values?)
        public uint Unknown4; // 00 00 00 00
        public uint Unknown5; // 00 06 00 01  (may be two values?)
        public uint Unknown6; // 00 00 00 00

        public void ReadFrom(byte[] buffer, int offset)
        {
            Signature = EndianUtilities.BytesToString(buffer, offset + 0x00, 8);
            Checksum = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x08);
            SequenceNumber = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x0C);
            Unknown1 = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x14);
            Unknown2 = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x1C);
            Item1Str = EndianUtilities.BytesToString(buffer, offset + 0x24, 10).Trim('\0');
            Item1Start = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x2E);
            Item1Size = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x36);
            Unknown3 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x3E);
            Unknown4 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x42);
            Item2Str = EndianUtilities.BytesToString(buffer, offset + 0x46, 10).Trim('\0');
            Item2Start = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x50);
            Item2Size = EndianUtilities.ToInt64BigEndian(buffer, offset + 0x58);
            Unknown5 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x60);
            Unknown6 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x64);
        }

        ////}
        ////    throw new NotImplementedException();
        ////    // Add all byte values for ?? bytes
        ////    // Zero checksum bytes (0x08, 4)
        ////{

        ////private static int CalcChecksum()
    }
}
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

namespace DiscUtils.Dmg
{
    using System;

    internal class UdifResourceFile : IByteArraySerializable
    {
        public uint Signature;
        public uint Version;
        public uint HeaderSize;
        public uint Flags;

        public ulong RunningDataForkOffset;
        public ulong DataForkOffset;
        public ulong DataForkLength;
        public ulong RsrcForkOffset;
        public ulong RsrcForkLength;

        public uint SegmentNumber;
        public uint SegmentCount;
        public Guid SegmentGuid;

        public UdifChecksum DataForkChecksum;
        public ulong XmlOffset;
        public ulong XmlLength;

        public UdifChecksum MasterChecksum;
        public uint ImageVariant;
        public long SectorCount;

        public int Size
        {
            get { return 512; }
        }

        public bool SignatureValid
        {
            get { return Signature == 0x6B6F6C79; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Signature = Utilities.ToUInt32BigEndian(buffer, offset + 0);
            Version = Utilities.ToUInt32BigEndian(buffer, offset + 4);
            HeaderSize = Utilities.ToUInt32BigEndian(buffer, offset + 8);
            Flags = Utilities.ToUInt32BigEndian(buffer, offset + 12);
            RunningDataForkOffset = Utilities.ToUInt64BigEndian(buffer, offset + 16);
            DataForkOffset = Utilities.ToUInt64BigEndian(buffer, offset + 24);
            DataForkLength = Utilities.ToUInt64BigEndian(buffer, offset + 32);
            RsrcForkOffset = Utilities.ToUInt64BigEndian(buffer, offset + 40);
            RsrcForkLength = Utilities.ToUInt64BigEndian(buffer, offset + 48);
            SegmentNumber = Utilities.ToUInt32BigEndian(buffer, offset + 56);
            SegmentCount = Utilities.ToUInt32BigEndian(buffer, offset + 60);
            SegmentGuid = Utilities.ToGuidBigEndian(buffer, offset + 64);

            DataForkChecksum = Utilities.ToStruct<UdifChecksum>(buffer, offset + 80);
            XmlOffset = Utilities.ToUInt64BigEndian(buffer, offset + 216);
            XmlLength = Utilities.ToUInt64BigEndian(buffer, offset + 224);

            MasterChecksum = Utilities.ToStruct<UdifChecksum>(buffer, offset + 352);
            ImageVariant = Utilities.ToUInt32BigEndian(buffer, offset + 488);
            SectorCount = Utilities.ToInt64BigEndian(buffer, offset + 492);

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

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

namespace DiscUtils.Dmg
{
    internal class UdifResourceFile : IByteArraySerializable
    {
        public UdifChecksum DataForkChecksum;
        public ulong DataForkLength;
        public ulong DataForkOffset;
        public uint Flags;
        public uint HeaderSize;
        public uint ImageVariant;

        public UdifChecksum MasterChecksum;
        public ulong RsrcForkLength;
        public ulong RsrcForkOffset;

        public ulong RunningDataForkOffset;
        public long SectorCount;
        public uint SegmentCount;
        public Guid SegmentGuid;

        public uint SegmentNumber;
        public uint Signature;
        public uint Version;
        public ulong XmlLength;
        public ulong XmlOffset;

        public bool SignatureValid
        {
            get { return Signature == 0x6B6F6C79; }
        }

        public int Size
        {
            get { return 512; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Signature = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0);
            Version = EndianUtilities.ToUInt32BigEndian(buffer, offset + 4);
            HeaderSize = EndianUtilities.ToUInt32BigEndian(buffer, offset + 8);
            Flags = EndianUtilities.ToUInt32BigEndian(buffer, offset + 12);
            RunningDataForkOffset = EndianUtilities.ToUInt64BigEndian(buffer, offset + 16);
            DataForkOffset = EndianUtilities.ToUInt64BigEndian(buffer, offset + 24);
            DataForkLength = EndianUtilities.ToUInt64BigEndian(buffer, offset + 32);
            RsrcForkOffset = EndianUtilities.ToUInt64BigEndian(buffer, offset + 40);
            RsrcForkLength = EndianUtilities.ToUInt64BigEndian(buffer, offset + 48);
            SegmentNumber = EndianUtilities.ToUInt32BigEndian(buffer, offset + 56);
            SegmentCount = EndianUtilities.ToUInt32BigEndian(buffer, offset + 60);
            SegmentGuid = EndianUtilities.ToGuidBigEndian(buffer, offset + 64);

            DataForkChecksum = EndianUtilities.ToStruct<UdifChecksum>(buffer, offset + 80);
            XmlOffset = EndianUtilities.ToUInt64BigEndian(buffer, offset + 216);
            XmlLength = EndianUtilities.ToUInt64BigEndian(buffer, offset + 224);

            MasterChecksum = EndianUtilities.ToStruct<UdifChecksum>(buffer, offset + 352);
            ImageVariant = EndianUtilities.ToUInt32BigEndian(buffer, offset + 488);
            SectorCount = EndianUtilities.ToInt64BigEndian(buffer, offset + 492);

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
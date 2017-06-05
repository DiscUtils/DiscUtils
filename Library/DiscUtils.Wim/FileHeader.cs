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

namespace DiscUtils.Wim
{
    internal class FileHeader
    {
        public uint BootIndex;
        public ShortResourceHeader BootMetaData;
        public int CompressionSize;
        public FileFlags Flags;
        public uint HeaderSize;
        public uint ImageCount;
        public ShortResourceHeader IntegrityHeader;
        public ShortResourceHeader OffsetTableHeader;
        public ushort PartNumber;
        public string Tag;
        public ushort TotalParts;
        public uint Version;
        public Guid WimGuid;
        public ShortResourceHeader XmlDataHeader;

        public void Read(byte[] buffer, int offset)
        {
            Tag = EndianUtilities.BytesToString(buffer, offset, 8);
            HeaderSize = EndianUtilities.ToUInt32LittleEndian(buffer, 8);
            Version = EndianUtilities.ToUInt32LittleEndian(buffer, 12);
            Flags = (FileFlags)EndianUtilities.ToUInt32LittleEndian(buffer, 16);
            CompressionSize = EndianUtilities.ToInt32LittleEndian(buffer, 20);
            WimGuid = EndianUtilities.ToGuidLittleEndian(buffer, 24);
            PartNumber = EndianUtilities.ToUInt16LittleEndian(buffer, 40);
            TotalParts = EndianUtilities.ToUInt16LittleEndian(buffer, 42);
            ImageCount = EndianUtilities.ToUInt32LittleEndian(buffer, 44);

            OffsetTableHeader = new ShortResourceHeader();
            OffsetTableHeader.Read(buffer, 48);

            XmlDataHeader = new ShortResourceHeader();
            XmlDataHeader.Read(buffer, 72);

            BootMetaData = new ShortResourceHeader();
            BootMetaData.Read(buffer, 96);

            BootIndex = EndianUtilities.ToUInt32LittleEndian(buffer, 120);

            IntegrityHeader = new ShortResourceHeader();
            IntegrityHeader.Read(buffer, 124);
        }

        public bool IsValid()
        {
            return Tag == "MSWIM\0\0\0" && HeaderSize >= 148;
        }
    }
}
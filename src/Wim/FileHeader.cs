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

namespace DiscUtils.Wim
{
    [Flags]
    internal enum FileFlags
    {
        Compression = 0x00000002,
        ReadOnly    = 0x00000004,
        Spanned     = 0x00000008,
        ResourceOnly = 0x00000010,
        MetaDataOnly    = 0x00000020,
        WriteInProgress = 0x00000040,
        ReparsePointFix   = 0x00000080,
        XpressCompression = 0x00020000,
        LzxCompression = 0x00040000
    }

    internal class FileHeader
    {
        public string Tag;
        public uint HeaderSize;
        public uint Version;
        public FileFlags Flags;
        public int CompressionSize;
        public Guid WimGuid;
        public ushort PartNumber;
        public ushort TotalParts;
        public uint ImageCount;
        public ShortResourceHeader OffsetTableHeader;
        public ShortResourceHeader XmlDataHeader;
        public ShortResourceHeader BootMetaData;
        public uint BootIndex;
        public ShortResourceHeader IntegrityHeader;

        public void Read(byte[] buffer, int offset)
        {
            Tag = Utilities.BytesToString(buffer, offset, 8);
            HeaderSize = Utilities.ToUInt32LittleEndian(buffer, 8);
            Version = Utilities.ToUInt32LittleEndian(buffer, 12);
            Flags = (FileFlags)Utilities.ToUInt32LittleEndian(buffer, 16);
            CompressionSize = Utilities.ToInt32LittleEndian(buffer, 20);
            WimGuid = Utilities.ToGuidLittleEndian(buffer, 24);
            PartNumber = Utilities.ToUInt16LittleEndian(buffer, 40);
            TotalParts = Utilities.ToUInt16LittleEndian(buffer, 42);
            ImageCount = Utilities.ToUInt32LittleEndian(buffer, 44);

            OffsetTableHeader = new ShortResourceHeader();
            OffsetTableHeader.Read(buffer, 48);

            XmlDataHeader = new ShortResourceHeader();
            XmlDataHeader.Read(buffer, 72);

            BootMetaData = new ShortResourceHeader();
            BootMetaData.Read(buffer, 96);

            BootIndex = Utilities.ToUInt32LittleEndian(buffer, 120);

            IntegrityHeader = new ShortResourceHeader();
            IntegrityHeader.Read(buffer, 124);
        }

        public bool IsValid()
        {
            return Tag == "MSWIM\0\0\0" && HeaderSize >= 148;
        }
    }
}

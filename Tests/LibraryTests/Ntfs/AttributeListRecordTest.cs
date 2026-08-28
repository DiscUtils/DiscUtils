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

using DiscUtils.Ntfs;
using Xunit;

namespace LibraryTests.Ntfs
{
    public class AttributeListRecordTest
    {
        // The $ATTRIBUTE_LIST entry fixed header is 26 (0x1A) bytes:
        //   Type(4) + RecordLength(2) + NameLength(1) + NameOffset(1) + StartVcn(8)
        //   + BaseFileReference(8) + AttributeId(2) = 26.
        // Per the NTFS on-disk layout the attribute name packs immediately after the
        // header, so the NameOffset field must equal 26. DiscUtils historically wrote
        // 0x20 (32) instead. Windows ntfs.sys validates this field and rejects any file
        // record whose $ATTRIBUTE_LIST entries use 32 with STATUS_FILE_CORRUPT /
        // ERROR_FILE_CORRUPT (1392) -- so images written by DiscUtils that contain a
        // file needing an $ATTRIBUTE_LIST (enough hard links to overflow the base MFT
        // record) fail to open/load on Windows even though offline chkdsk tolerates them.
        private const byte AttributeListEntryHeaderSize = 26;

        // NameOffset field is the byte at entry offset 0x07.
        private const int NameOffsetFieldOffset = 0x07;

        [Fact]
        public void WriteTo_UnnamedEntry_NameOffsetEqualsHeaderSize()
        {
            var record = new AttributeListRecord
            {
                Type = AttributeType.Data,
                Name = null,
                StartVcn = 0,
                BaseFileReference = new FileRecordReference(5, 5),
                AttributeId = 0,
            };

            byte[] buffer = new byte[64];
            record.WriteTo(buffer, 0);

            Assert.Equal(AttributeListEntryHeaderSize, buffer[NameOffsetFieldOffset]);
            Assert.Equal(AttributeListEntryHeaderSize, record.NameOffset);
        }

        [Fact]
        public void WriteTo_NamedEntry_NamePackedImmediatelyAfterHeader()
        {
            var record = new AttributeListRecord
            {
                Type = AttributeType.Data,
                Name = "AB",
                StartVcn = 0,
                BaseFileReference = new FileRecordReference(5, 5),
                AttributeId = 0,
            };

            byte[] buffer = new byte[64];
            record.WriteTo(buffer, 0);

            Assert.Equal(AttributeListEntryHeaderSize, buffer[NameOffsetFieldOffset]);

            // The name must be stored starting at offset 26 (UTF-16LE 'A','B').
            Assert.Equal((byte)'A', buffer[AttributeListEntryHeaderSize + 0]);
            Assert.Equal((byte)0, buffer[AttributeListEntryHeaderSize + 1]);
            Assert.Equal((byte)'B', buffer[AttributeListEntryHeaderSize + 2]);
            Assert.Equal((byte)0, buffer[AttributeListEntryHeaderSize + 3]);
        }

        [Fact]
        public void WriteTo_RoundTrips_ThroughReadFrom()
        {
            var record = new AttributeListRecord
            {
                Type = AttributeType.Data,
                Name = "stream",
                StartVcn = 3,
                BaseFileReference = new FileRecordReference(42, 7),
                AttributeId = 9,
            };

            byte[] buffer = new byte[128];
            record.WriteTo(buffer, 0);

            var readBack = new AttributeListRecord();
            readBack.ReadFrom(buffer, 0);

            Assert.Equal(AttributeListEntryHeaderSize, readBack.NameOffset);
            Assert.Equal(record.Type, readBack.Type);
            Assert.Equal(record.Name, readBack.Name);
            Assert.Equal(record.StartVcn, readBack.StartVcn);
            Assert.Equal(record.BaseFileReference, readBack.BaseFileReference);
            Assert.Equal(record.AttributeId, readBack.AttributeId);
            Assert.Equal(record.RecordLength, readBack.RecordLength);
        }
    }
}

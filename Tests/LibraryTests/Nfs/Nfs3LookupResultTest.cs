//
// Copyright (c) 2017, Quamotion
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

using DiscUtils;
using DiscUtils.Nfs;
using System;
using System.IO;
using Xunit;

namespace LibraryTests.Nfs
{
    public class Nfs3LookupResultTest
    {
        [Fact]
        public void RoundTripTest()
        {
            Nfs3LookupResult result = new Nfs3LookupResult()
            {
                DirAttributes = new Nfs3FileAttributes()
                {
                    AccessTime = new Nfs3FileTime(new DateTime(2017, 1, 1)),
                    BytesUsed = 1,
                    ChangeTime = new Nfs3FileTime(new DateTime(2017, 1, 2)),
                    FileId = 2,
                    FileSystemId = 3,
                    Gid = 4,
                    LinkCount = 5,
                    Mode = UnixFilePermissions.GroupAll,
                    ModifyTime = new Nfs3FileTime(new DateTime(2017, 1, 3)),
                    RdevMajor = 6,
                    RdevMinor = 7,
                    Size = 8,
                    Type = Nfs3FileType.BlockDevice,
                    Uid = 9
                },
                ObjectAttributes = new Nfs3FileAttributes()
                {
                    AccessTime = new Nfs3FileTime(new DateTime(2017, 1, 10)),
                    BytesUsed = 11,
                    ChangeTime = new Nfs3FileTime(new DateTime(2017, 1, 12)),
                    FileId = 12,
                    FileSystemId = 13,
                    Gid = 14,
                    LinkCount = 15,
                    Mode = UnixFilePermissions.GroupWrite,
                    ModifyTime = new Nfs3FileTime(new DateTime(2017, 1, 13)),
                    RdevMajor = 16,
                    RdevMinor = 17,
                    Size = 18,
                    Type = Nfs3FileType.Socket,
                    Uid = 19
                },
                ObjectHandle = new Nfs3FileHandle()
                {
                    Value = new byte[] { 0x20 }
                },
                Status = Nfs3Status.Ok
            };

            Nfs3LookupResult clone = null;

            using (MemoryStream stream = new MemoryStream())
            {
                XdrDataWriter writer = new XdrDataWriter(stream);
                result.Write(writer);

                stream.Position = 0;
                XdrDataReader reader = new XdrDataReader(stream);
                clone = new Nfs3LookupResult(reader);
            }

            Assert.Equal(result, clone);
        }
    }
}

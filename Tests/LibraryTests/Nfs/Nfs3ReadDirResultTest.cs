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

using DiscUtils.Nfs;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace LibraryTests.Nfs
{
    public class Nfs3ReadDirResultTest
    {
        [Fact]
        public void RoundTripTest()
        {
            Nfs3ReadDirResult result = new Nfs3ReadDirResult()
            {
                Status = Nfs3Status.Ok,
                Eof = false,
                CookieVerifier = 1u,
                DirAttributes = new Nfs3FileAttributes()
                {
                    AccessTime = new Nfs3FileTime(new DateTime(2018, 1, 1)),
                    ChangeTime = new Nfs3FileTime(new DateTime(2018, 1, 2)),
                    ModifyTime = new Nfs3FileTime(new DateTime(2018, 1, 3)),
                },
                DirEntries = new List<Nfs3DirectoryEntry>()
                {
                    new Nfs3DirectoryEntry()
                    {
                         Cookie = 2u,
                         FileAttributes = new Nfs3FileAttributes()
                         {
                             AccessTime = new Nfs3FileTime(new DateTime(2018, 2, 1)),
                             ChangeTime = new Nfs3FileTime(new DateTime(2018, 2, 2)),
                             ModifyTime = new Nfs3FileTime(new DateTime(2018, 2, 3)),
                         },
                         FileHandle = new Nfs3FileHandle()
                         {
                             Value = new byte[]{0x20, 0x18 }
                         },
                         FileId = 2018,
                         Name = "test.bin"
                    }
                }
            };

            Nfs3ReadDirResult clone = null;

            using (MemoryStream stream = new MemoryStream())
            {
                XdrDataWriter writer = new XdrDataWriter(stream);
                result.Write(writer);

                stream.Position = 0;
                XdrDataReader reader = new XdrDataReader(stream);
                clone = new Nfs3ReadDirResult(reader);
            }

            Assert.Equal(result, clone);
        }
    }
}

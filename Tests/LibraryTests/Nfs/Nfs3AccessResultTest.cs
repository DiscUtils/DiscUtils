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
    public class Nfs3AccessResultTest
    {
        [Fact]
        public void RoundTripTest()
        {
            Nfs3AccessResult result = new Nfs3AccessResult()
            {
                Access = Nfs3AccessPermissions.Execute,
                ObjectAttributes = new Nfs3FileAttributes()
                {
                    AccessTime = new Nfs3FileTime(new DateTime(2017, 1, 1)),
                    ChangeTime = new Nfs3FileTime(new DateTime(2017, 1, 2)),
                    ModifyTime = new Nfs3FileTime(new DateTime(2017, 1, 3))
                },
                Status = Nfs3Status.AccessDenied
            };

            Nfs3AccessResult clone = null;

            using (MemoryStream stream = new MemoryStream())
            {
                XdrDataWriter writer = new XdrDataWriter(stream);
                result.Write(writer);

                stream.Position = 0;
                XdrDataReader reader = new XdrDataReader(stream);
                clone = new Nfs3AccessResult(reader);
            }

            Assert.Equal(result, clone);
        }
    }
}

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

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils
{
    [TestFixture]
    public class VirtualDiskTest
    {
        [Test]
        public void TestSignature()
        {
            MemoryStream ms = new MemoryStream();
            ms.SetLength(1024 * 1024);

            Raw.Disk rawDisk = new DiscUtils.Raw.Disk(ms, Ownership.Dispose);
            Assert.AreEqual(0, rawDisk.Signature);
            rawDisk.Signature = unchecked((int)0xDEADBEEF);
            Assert.AreEqual(unchecked((int)0xDEADBEEF), rawDisk.Signature);
        }

        [Test]
        public void TestMbr()
        {
            MemoryStream ms = new MemoryStream();
            ms.SetLength(1024 * 1024);

            byte[] newMbr = new byte[512];
            for (int i = 0; i < 512; i++)
            {
                newMbr[i] = (byte)i;
            }

            Raw.Disk rawDisk = new DiscUtils.Raw.Disk(ms, Ownership.Dispose);
            rawDisk.SetMasterBootRecord(newMbr);

            byte[] readMbr = rawDisk.GetMasterBootRecord();
            Assert.AreEqual(512, readMbr.Length);

            for (int i = 0; i < 512; i++)
            {
                if (readMbr[i] != (byte)i)
                {
                    Assert.Fail("Mismatch on byte {0}, expected {1} was {2}", i, (byte)i, readMbr[i]);
                }
            }
        }


        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMbr_Null()
        {
            MemoryStream ms = new MemoryStream();
            ms.SetLength(1024 * 1024);

            Raw.Disk rawDisk = new DiscUtils.Raw.Disk(ms, Ownership.Dispose);
            rawDisk.SetMasterBootRecord(null);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void TestMbr_WrongSize()
        {
            MemoryStream ms = new MemoryStream();
            ms.SetLength(1024 * 1024);

            Raw.Disk rawDisk = new DiscUtils.Raw.Disk(ms, Ownership.Dispose);
            rawDisk.SetMasterBootRecord(new byte[511]);
        }
    }
}

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

using System.IO;
using NUnit.Framework;

namespace DiscUtils.Iso9660
{
    [TestFixture]
    public class BuilderTest
    {
        [Test]
        public void AddFileStream()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"ADIR\AFILE.TXT", new MemoryStream());
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.IsTrue(fs.Exists(@"ADIR\AFILE.TXT"));
        }

        [Test]
        public void AddFileBytes()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"ADIR\AFILE.TXT", new byte[] {});
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.IsTrue(fs.Exists(@"ADIR\AFILE.TXT"));
        }

        [Test]
        public void BootImage()
        {
            byte[] memoryStream = new byte[33 * 512];
            for(int i = 0; i < memoryStream.Length; ++i)
            {
                memoryStream[i] = (byte)i;
            }

            CDBuilder builder = new CDBuilder();
            builder.SetBootImage(new MemoryStream(memoryStream), BootDeviceEmulation.HardDisk, 0x543);

            CDReader fs = new CDReader(builder.Build(), false);
            Assert.IsTrue(fs.HasBootImage);

            using (Stream bootImg = fs.OpenBootImage())
            {
                Assert.AreEqual(memoryStream.Length, bootImg.Length);
                for (int i = 0; i < bootImg.Length; ++i)
                {
                    if (memoryStream[i] != bootImg.ReadByte())
                    {
                        Assert.Fail("Boot image corrupted");
                    }
                }
            }

            Assert.AreEqual(BootDeviceEmulation.HardDisk, fs.BootEmulation);
            Assert.AreEqual(0x543, fs.BootLoadSegment);
        }
    }
}

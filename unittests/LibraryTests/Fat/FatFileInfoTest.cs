//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils.Fat
{
    [TestFixture]
    public class FatFileInfoTest
    {
        [Test]
        public void CreateFile()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }

            fs = new FatFileSystem(ms);
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            Assert.IsTrue(fi.Exists);
            Assert.AreEqual(FileAttributes.Archive, fi.Attributes);
            Assert.AreEqual(1, fi.Length);

            using (Stream s = fs.OpenFile("Foo.txt", FileMode.Open, FileAccess.Read))
            {
                Assert.AreEqual(1, s.ReadByte());
            }
        }

        [Test]
        public void DeleteFile()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite)) { }

            Assert.AreEqual(1, fs.Root.GetFiles().Length);

            fs = new FatFileSystem(ms);
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            fi.Delete();

            Assert.AreEqual(0, fs.Root.GetFiles().Length);
        }

        [Test]
        public void Length()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite)) {
                s.SetLength(3128);
            }

            Assert.AreEqual(3128, fs.GetFileInfo("foo.txt").Length);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.ReadWrite))
            {
                s.SetLength(3);
            }

            Assert.AreEqual(3, fs.GetFileInfo("foo.txt").Length);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.ReadWrite))
            {
                s.SetLength(3333);

                byte[] buffer = new byte[512];
                for(int i = 0; i < buffer.Length; ++i)
                {
                    buffer[i] = (byte)i;
                }

                s.Write(buffer, 0, buffer.Length);
                s.Write(buffer, 0, buffer.Length);

                Assert.AreEqual(1024, s.Position);

                s.SetLength(512);

                Assert.AreEqual(512, s.Position);
            }

            fs = new FatFileSystem(ms);
            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.ReadWrite))
            {
                byte[] buffer = new byte[512];
                int numRead = s.Read(buffer, 0, buffer.Length);
                int totalRead = 0;
                while (numRead != 0)
                {
                    totalRead += numRead;
                    numRead = s.Read(buffer, totalRead, buffer.Length - totalRead);
                }

                for (int i = 0; i < buffer.Length; ++i)
                {
                    Assert.AreEqual((byte)i, buffer[i]);
                }
            }
        }
    }
}

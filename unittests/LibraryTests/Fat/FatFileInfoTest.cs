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

            using (Stream s = fs.Open("Foo.txt", FileMode.Open, FileAccess.Read))
            {
                Assert.AreEqual(1, s.ReadByte());
            }

        }
    }
}

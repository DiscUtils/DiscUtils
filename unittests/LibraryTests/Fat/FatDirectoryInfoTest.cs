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
    public class FatDirectoryInfoTest
    {
        [Test]
        public void Create()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();
        }

        [Test]
        public void CreateExisting()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
        }

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Long()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIRWITHANAMETHATISTOOLONG");
            dirInfo.Create();
        }

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Characters()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOME*DIR");
            dirInfo.Create();
        }

        [Test]
        public void Exists()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR\").Exists);
            Assert.IsFalse(fs.GetDirectoryInfo(@"NONDIR").Exists);
            Assert.IsFalse(fs.GetDirectoryInfo(@"SOMEDIR\NONDIR").Exists);
        }

        [Test]
        public void FullName()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            Assert.AreEqual(@"\", fs.Root.FullName);
            Assert.AreEqual(@"SOMEDIR\", fs.GetDirectoryInfo(@"SOMEDIR").FullName);
            Assert.AreEqual(@"SOMEDIR\CHILDDIR\", fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").FullName);
        }
    }
}

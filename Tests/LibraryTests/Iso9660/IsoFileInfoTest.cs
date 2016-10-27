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

using System;
using System.IO;
using NUnit.Framework;

namespace DiscUtils.Iso9660
{
    [TestFixture]
    public class IsoFileInfoTest
    {
        [Test]
        public void Length()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"FILE.TXT", new byte[0]);
            builder.AddFile(@"FILE2.TXT", new byte[1]);
            builder.AddFile(@"FILE3.TXT", new byte[10032]);
            builder.AddFile(@"FILE3.TXT;2", new byte[132]);
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.AreEqual(0, fs.GetFileInfo("FILE.txt").Length);
            Assert.AreEqual(1, fs.GetFileInfo("FILE2.txt").Length);
            Assert.AreEqual(10032, fs.GetFileInfo("FILE3.txt;1").Length);
            Assert.AreEqual(132, fs.GetFileInfo("FILE3.txt;2").Length);
            Assert.AreEqual(132, fs.GetFileInfo("FILE3.txt").Length);
        }

        [Test]
        [ExpectedException(typeof(FileNotFoundException))]
        [Category("ThrowsException")]
        public void Open_FileNotFound()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Open)) { }
        }

        [Test]
        public void Open_Read()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile("foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Open, FileAccess.Read))
            {
                Assert.IsFalse(s.CanWrite);
                Assert.IsTrue(s.CanRead);

                Assert.AreEqual(1, s.ReadByte());
            }
        }

        [Test]
        public void Name()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.AreEqual("foo.txt", fs.GetFileInfo("foo.txt").Name);
            Assert.AreEqual("foo.txt", fs.GetFileInfo(@"path\foo.txt").Name);
            Assert.AreEqual("foo.txt", fs.GetFileInfo(@"\foo.txt").Name);
        }

        [Test]
        public void Attributes()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile("foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            // Check default attributes
            Assert.AreEqual(FileAttributes.ReadOnly, fi.Attributes);
        }

        [Test]
        public void Exists()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"dir\foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.IsFalse(fs.GetFileInfo("unknown.txt").Exists);
            Assert.IsTrue(fs.GetFileInfo(@"dir\foo.txt").Exists);
            Assert.IsFalse(fs.GetFileInfo(@"dir").Exists);
        }

        [Test]
        public void CreationTimeUtc()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.GreaterOrEqual(DateTime.UtcNow, fs.GetFileInfo("foo.txt").CreationTimeUtc);
            Assert.LessOrEqual(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)), fs.GetFileInfo("foo.txt").CreationTimeUtc);
        }

        [Test]
        public void Equals()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.AreEqual(fs.GetFileInfo("foo.txt"), fs.GetFileInfo("foo.txt"));
        }

        [Test]
        public void Parent()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"SOMEDIR\ADIR\FILE.TXT", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\ADIR\FILE.TXT");
            Assert.AreEqual(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Parent);
            Assert.AreEqual(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Directory);
        }
    }
}

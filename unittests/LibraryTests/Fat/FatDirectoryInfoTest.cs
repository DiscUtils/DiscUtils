//
// Copyright (c) 2008-2009, Kenneth Bell
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

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
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

            Assert.IsTrue(fs.GetDirectoryInfo(@"\").Exists);
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

        [Test]
        public void Delete()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            fs.CreateDirectory(@"Fred");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete();
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);

            fs = new FatFileSystem(ms);
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [Test]
        public void DeleteRecursive()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            fs.CreateDirectory(@"Fred\child");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete(true);
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteRoot()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            fs.Root.Delete();
        }

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteNonEmpty_NonRecursive()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            fs.CreateDirectory(@"Fred\child");
            fs.Root.GetDirectories(@"Fred")[0].Delete();
        }

        [Test]
        [Category("SlowTest")]
        public void CreateDeleteLeakTest()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            for (int i = 0; i < 2000; ++i)
            {
                fs.CreateDirectory(@"Fred");
                fs.Root.GetDirectories(@"Fred")[0].Delete();
            }

            fs.CreateDirectory(@"SOMEDIR");
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR");
            Assert.IsNotNull(dirInfo);

            for (int i = 0; i < 2000; ++i)
            {
                fs.CreateDirectory(@"SOMEDIR\Fred");
                dirInfo.GetDirectories(@"Fred")[0].Delete();
            }
        }

        [Test]
        public void Move()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.GetDirectoryInfo(@"SOMEDIR\CHILD").MoveTo("NEWDIR");

            fs = new FatFileSystem(ms);

            Assert.AreEqual(2, fs.Root.GetDirectories().Length);
            Assert.AreEqual(0, fs.Root.GetDirectories("SOMEDIR")[0].GetDirectories().Length);
        }

        [Test]
        public void Extension()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            Assert.AreEqual("dir", fs.GetDirectoryInfo("fred.dir").Extension);
            Assert.AreEqual("", fs.GetDirectoryInfo("fred").Extension);
        }

        [Test]
        public void GetDirectories()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"A.DIR");

            fs = new FatFileSystem(ms);
            Assert.AreEqual(2, fs.Root.GetDirectories().Length);

            DiscDirectoryInfo someDir = fs.Root.GetDirectories(@"SoMeDir")[0];
            Assert.AreEqual(1, fs.Root.GetDirectories("SOMEDIR").Length);
            Assert.AreEqual("SOMEDIR", someDir.Name);

            Assert.AreEqual(1, someDir.GetDirectories("*.*").Length);
            Assert.AreEqual("CHILD", someDir.GetDirectories("*.*")[0].Name);
            Assert.AreEqual(2, someDir.GetDirectories("*.*", SearchOption.AllDirectories).Length);

            Assert.AreEqual(4, fs.Root.GetDirectories("*.*", SearchOption.AllDirectories).Length);
            Assert.AreEqual(2, fs.Root.GetDirectories("*.*", SearchOption.TopDirectoryOnly).Length);

            Assert.AreEqual(1, fs.Root.GetDirectories("*.DIR", SearchOption.AllDirectories).Length);
            Assert.AreEqual(@"A.DIR\", fs.Root.GetDirectories("*.DIR", SearchOption.AllDirectories)[0].FullName);

            Assert.AreEqual(1, fs.Root.GetDirectories("GCHILD", SearchOption.AllDirectories).Length);
            Assert.AreEqual(@"SOMEDIR\CHILD\GCHILD\", fs.Root.GetDirectories("GCHILD", SearchOption.AllDirectories)[0].FullName);
        }

        [Test]
        public void GetFiles()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.DIR");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            fs = new FatFileSystem(ms);
            Assert.AreEqual(1, fs.Root.GetFiles().Length);
            Assert.AreEqual("FOO.TXT", fs.Root.GetFiles()[0].FullName);

            Assert.AreEqual(2, fs.Root.GetDirectories("SOMEDIR")[0].GetFiles("*.TXT").Length);
            Assert.AreEqual(4, fs.Root.GetFiles("*.TXT", SearchOption.AllDirectories).Length);

            Assert.AreEqual(0, fs.Root.GetFiles("*.DIR", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void GetFileSystemInfos()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.EXT");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.EXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            fs = new FatFileSystem(ms);
            Assert.AreEqual(3, fs.Root.GetFileSystemInfos().Length);

            Assert.AreEqual(1, fs.Root.GetFileSystemInfos("*.EXT").Length);
            Assert.AreEqual(2, fs.Root.GetFileSystemInfos("*.?XT").Length);
        }

        [Test]
        public void Parent()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);
            fs.CreateDirectory("SOMEDIR");

            Assert.AreEqual(fs.Root, fs.Root.GetDirectories("SOMEDIR")[0].Parent);
        }

        [Test]
        public void Parent_Root()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            Assert.IsNull(fs.Root.Parent);
        }

        [Test]
        public void CreationTimeUtc()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.UtcNow, fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
            Assert.LessOrEqual(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
        }

        [Test]
        public void CreationTime()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.Now, fs.Root.GetDirectories("DIR")[0].CreationTime);
            Assert.LessOrEqual(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTime);
        }

        [Test]
        public void LastAccessTime()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            di.LastAccessTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastAccessTime);
        }

        [Test]
        public void LastWriteTime()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            di.LastWriteTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastWriteTime);
        }

        [Test]
        public void Equals()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            Assert.AreEqual(fs.GetDirectoryInfo("foo"), fs.GetDirectoryInfo("foo"));
        }
    }
}

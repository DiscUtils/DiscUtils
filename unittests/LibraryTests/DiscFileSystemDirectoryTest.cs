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
using System.Collections.Generic;
using System.IO;
using DiscUtils.Vhd;
using NUnit.Framework;

namespace DiscUtils
{
    [TestFixture]
    public class DiscFileSystemDirectoryTest
    {
        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void TestCreateInRoot(DiscFileSystem testFs)
        {
            using (Stream s = testFs.OpenFile(@"NEWROOT.TXT", FileMode.Create, FileAccess.ReadWrite))
            {
                Assert.AreEqual(0, s.Length);
            }
            Assert.IsTrue(testFs.FileExists("NEWROOT.TXT"));
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException), UserMessage = "File already exists")]
        [Category("ThrowsException")]
        public void TestCreateFailInRoot(DiscFileSystem testFs)
        {
            using (Stream s = testFs.OpenFile(@"NEWROOT.TXT", FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }

            using (Stream s = testFs.OpenFile(@"NEWROOT.TXT", FileMode.CreateNew, FileAccess.ReadWrite))
            {
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Create(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreateRecursive(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
            Assert.AreEqual(1, fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories().Length);
            Assert.AreEqual("CHILDDIR", fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories()[0].Name);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreateExisting(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Long(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIRWITHANAMETHATISTOOLONG");
            dirInfo.Create();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Characters(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOME*DIR");
            dirInfo.Create();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Exists(DiscFileSystem fs)
        {
            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.IsTrue(fs.GetDirectoryInfo(@"\").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").Exists);
            Assert.IsTrue(fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR\").Exists);
            Assert.IsFalse(fs.GetDirectoryInfo(@"NONDIR").Exists);
            Assert.IsFalse(fs.GetDirectoryInfo(@"SOMEDIR\NONDIR").Exists);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void FullName(DiscFileSystem fs)
        {
            Assert.AreEqual(@"\", fs.Root.FullName);
            Assert.AreEqual(@"SOMEDIR\", fs.GetDirectoryInfo(@"SOMEDIR").FullName);
            Assert.AreEqual(@"SOMEDIR\CHILDDIR\", fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").FullName);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Delete(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"Fred");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete();
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void DeleteRecursive(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"Fred\child");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete(true);
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteRoot(DiscFileSystem fs)
        {
            fs.Root.Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteNonEmpty_NonRecursive(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"Fred\child");
            fs.Root.GetDirectories(@"Fred")[0].Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [Category("SlowTest")]
        public void CreateDeleteLeakTest(DiscFileSystem fs)
        {
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

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Move(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.GetDirectoryInfo(@"SOMEDIR\CHILD").MoveTo("NEWDIR");

            Assert.AreEqual(2, fs.Root.GetDirectories().Length);
            Assert.AreEqual(0, fs.Root.GetDirectories("SOMEDIR")[0].GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Extension(DiscFileSystem fs)
        {
            Assert.AreEqual("dir", fs.GetDirectoryInfo("fred.dir").Extension);
            Assert.AreEqual("", fs.GetDirectoryInfo("fred").Extension);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetDirectories(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"A.DIR");

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

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetFiles(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.DIR");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            Assert.AreEqual(1, fs.Root.GetFiles().Length);
            Assert.AreEqual("FOO.TXT", fs.Root.GetFiles()[0].FullName);

            Assert.AreEqual(2, fs.Root.GetDirectories("SOMEDIR")[0].GetFiles("*.TXT").Length);
            Assert.AreEqual(4, fs.Root.GetFiles("*.TXT", SearchOption.AllDirectories).Length);

            Assert.AreEqual(0, fs.Root.GetFiles("*.DIR", SearchOption.AllDirectories).Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetFileSystemInfos(DiscFileSystem fs)
        {
            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.CreateDirectory(@"AAA.EXT");
            using (Stream s = fs.OpenFile(@"FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD.EXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\FOO.TXT", FileMode.Create)) { }
            using (Stream s = fs.OpenFile(@"SOMEDIR\CHILD\GCHILD\BAR.TXT", FileMode.Create)) { }

            Assert.AreEqual(3, fs.Root.GetFileSystemInfos().Length);

            Assert.AreEqual(1, fs.Root.GetFileSystemInfos("*.EXT").Length);
            Assert.AreEqual(2, fs.Root.GetFileSystemInfos("*.?XT").Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Parent(DiscFileSystem fs)
        {
            fs.CreateDirectory("SOMEDIR");

            Assert.AreEqual(fs.Root, fs.Root.GetDirectories("SOMEDIR")[0].Parent);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Parent_Root(DiscFileSystem fs)
        {
            Assert.IsNull(fs.Root.Parent);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTimeUtc(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.UtcNow, fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
            Assert.LessOrEqual(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTime(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.Now, fs.Root.GetDirectories("DIR")[0].CreationTime);
            Assert.LessOrEqual(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastAccessTime(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            di.LastAccessTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastAccessTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastWriteTime(DiscFileSystem fs)
        {
            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            di.LastWriteTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastWriteTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Equals(DiscFileSystem fs)
        {
            Assert.AreEqual(fs.GetDirectoryInfo("foo"), fs.GetDirectoryInfo("foo"));
        }
    }

    public class FileSystemSource
    {
        public static IEnumerable<TestCaseData> ReadWriteFileSystems
        {
            get
            {
                SparseMemoryBuffer buffer = new SparseMemoryBuffer(4096);
                SparseMemoryStream ms = new SparseMemoryStream();
                Geometry diskGeometry = Geometry.FromCapacity(30 * 1024 * 1024);
                yield return new TestCaseData(Fat.FatFileSystem.FormatFloppy(ms, Fat.FloppyDiskType.Extended, null)).SetName("FAT");

                // TODO: When format code complete, format a vanilla partition rather than relying on file on disk
                string baseFile = "ntfsblank.vhd";
                DiskImageFile parent = new DiskImageFile(
                    new FileStream(@"C:\temp\" + baseFile, FileMode.Open, FileAccess.Read),
                    Ownership.Dispose);
                Stream diffStream = new SparseMemoryStream();
                Disk disk = Disk.InitializeDifferencing(
                    diffStream,
                    Ownership.Dispose,
                    parent,
                    Ownership.Dispose,
                    @"C:\temp\" + baseFile,
                    @".\" + baseFile,
                    File.GetLastWriteTimeUtc(@"C:\temp\" + baseFile));
                yield return new TestCaseData(new Ntfs.NtfsFileSystem(disk.Partitions[0].Open())).SetName("NTFS");
            }
        }
    }
}

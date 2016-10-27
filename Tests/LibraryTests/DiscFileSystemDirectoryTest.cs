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
        public void Create(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreateRecursive(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR");
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
            Assert.AreEqual(1, fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories().Length);
            Assert.AreEqual("CHILDDIR", fs.GetDirectoryInfo(@"SOMEDIR").GetDirectories()[0].Name);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreateExisting(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOMEDIR");
            dirInfo.Create();
            dirInfo.Create();

            Assert.AreEqual(1, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Long(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo(new String('X', 256));
            dirInfo.Create();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateInvalid_Characters(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscDirectoryInfo dirInfo = fs.GetDirectoryInfo("SOME\0DIR");
            dirInfo.Create();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Exists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

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
        public void FullName(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.AreEqual(@"\", fs.Root.FullName);
            Assert.AreEqual(@"SOMEDIR\", fs.GetDirectoryInfo(@"SOMEDIR").FullName);
            Assert.AreEqual(@"SOMEDIR\CHILDDIR\", fs.GetDirectoryInfo(@"SOMEDIR\CHILDDIR").FullName);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Delete(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"Fred");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete();
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void DeleteRecursive(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"Fred\child");
            Assert.AreEqual(1, fs.Root.GetDirectories().Length);

            fs.Root.GetDirectories(@"Fred")[0].Delete(true);
            Assert.AreEqual(0, fs.Root.GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteRoot(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.Root.Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void DeleteNonEmpty_NonRecursive(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"Fred\child");
            fs.Root.GetDirectories(@"Fred")[0].Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "QuickReadWriteFileSystems")]
        [Category("SlowTest")]
        public void CreateDeleteLeakTest(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

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
        public void Move(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"SOMEDIR\CHILD\GCHILD");
            fs.GetDirectoryInfo(@"SOMEDIR\CHILD").MoveTo("NEWDIR");

            Assert.AreEqual(2, fs.Root.GetDirectories().Length);
            Assert.AreEqual(0, fs.Root.GetDirectories("SOMEDIR")[0].GetDirectories().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Extension(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.AreEqual("dir", fs.GetDirectoryInfo("fred.dir").Extension);
            Assert.AreEqual("", fs.GetDirectoryInfo("fred").Extension);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetDirectories(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

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

        [ExpectedException(typeof(DirectoryNotFoundException))]
        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetDirectories_BadPath(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.GetDirectories(@"\baddir");
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void GetFiles(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

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
        public void GetFileSystemInfos(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

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
        public void Parent(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("SOMEDIR");

            Assert.AreEqual(fs.Root, fs.Root.GetDirectories("SOMEDIR")[0].Parent);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Parent_Root(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.IsNull(fs.Root.Parent);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTimeUtc(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.UtcNow, fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
            Assert.LessOrEqual(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTimeUtc);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");

            Assert.GreaterOrEqual(DateTime.Now, fs.Root.GetDirectories("DIR")[0].CreationTime);
            Assert.LessOrEqual(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)), fs.Root.GetDirectories("DIR")[0].CreationTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastAccessTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            di.LastAccessTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastAccessTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastWriteTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("DIR");
            DiscDirectoryInfo di = fs.GetDirectoryInfo("DIR");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            di.LastWriteTime = baseTime;

            fs.CreateDirectory(@"DIR\CHILD");

            Assert.Less(baseTime, di.LastWriteTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Equals(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.AreEqual(fs.GetDirectoryInfo("foo"), fs.GetDirectoryInfo("foo"));
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void RootBehaviour(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            // Not all file systems can modify the root directory, so we just make sure 'get' and 'no-op' change work.
            fs.Root.Attributes = fs.Root.Attributes;
            fs.Root.CreationTimeUtc = fs.Root.CreationTimeUtc;
            fs.Root.LastAccessTimeUtc = fs.Root.LastAccessTimeUtc;
            fs.Root.LastWriteTimeUtc = fs.Root.LastWriteTimeUtc;
        }
    }

}

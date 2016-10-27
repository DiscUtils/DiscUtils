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

namespace DiscUtils
{
    [TestFixture]
    public class DiscFileSystemFileTest
    {
        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreateFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            Assert.IsTrue(fi.Exists);
            Assert.AreEqual(FileAttributes.Archive, fi.Attributes);
            Assert.AreEqual(1, fi.Length);

            using (Stream s = fs.OpenFile("Foo.txt", FileMode.Open, FileAccess.Read))
            {
                Assert.AreEqual(1, s.ReadByte());
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateFileInvalid_Long(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo(new string('X', 256)).Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void CreateFileInvalid_Characters(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo("A\0File").Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void DeleteFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite)) { }

            Assert.AreEqual(1, fs.Root.GetFiles().Length);

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            fi.Delete();

            Assert.AreEqual(0, fs.Root.GetFiles().Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Length(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.SetLength(3128);
            }

            Assert.AreEqual(3128, fs.GetFileInfo("foo.txt").Length);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.ReadWrite))
            {
                s.SetLength(3);
                Assert.AreEqual(3, s.Length);
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
                Assert.AreEqual(3333, s.Length);

                s.SetLength(512);

                Assert.AreEqual(512, s.Length);
            }

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

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(FileNotFoundException))]
        [Category("ThrowsException")]
        public void Open_FileNotFound(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Open)) { }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_FileExists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create)) { s.WriteByte(1); }

            using (Stream s = di.Open(FileMode.CreateNew)) { }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_DirExists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("FOO.TXT");

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create)) { s.WriteByte(1); }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Open_Read(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");

            using (Stream s = di.Open(FileMode.Create))
            {
                s.WriteByte(1);
            }

            using (Stream s = di.Open(FileMode.Open, FileAccess.Read))
            {
                Assert.IsFalse(s.CanWrite);
                Assert.IsTrue(s.CanRead);

                Assert.AreEqual(1, s.ReadByte());
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_Read_Fail(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create, FileAccess.Read))
            {
                s.WriteByte(1);
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Open_Write(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create, FileAccess.Write))
            {
                Assert.IsTrue(s.CanWrite);
                Assert.IsFalse(s.CanRead);
                s.WriteByte(1);
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_Write_Fail(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }

            using (Stream s = di.Open(FileMode.Open, FileAccess.Write))
            {
                Assert.IsTrue(s.CanWrite);
                Assert.IsFalse(s.CanRead);
                s.ReadByte();
            }
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Name(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.AreEqual("foo.txt", fs.GetFileInfo("foo.txt").Name);
            Assert.AreEqual("foo.txt", fs.GetFileInfo(@"path\foo.txt").Name);
            Assert.AreEqual("foo.txt", fs.GetFileInfo(@"\foo.txt").Name);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Attributes(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");
            using (Stream s = fi.Open(FileMode.Create)) { }

            // Check default attributes
            Assert.AreEqual(FileAttributes.Archive, fi.Attributes);

            // Check round-trip
            FileAttributes newAttrs = FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System;
            fi.Attributes = newAttrs;
            Assert.AreEqual(newAttrs, fi.Attributes);

            // And check persistence to disk
            Assert.AreEqual(newAttrs, fs.GetFileInfo("foo.txt").Attributes);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(ArgumentException))]
        [Category("ThrowsException")]
        public void Attributes_ChangeType(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");
            using (Stream s = fi.Open(FileMode.Create)) { }

            fi.Attributes = fi.Attributes | FileAttributes.Directory;
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Exists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            Assert.IsFalse(fi.Exists);

            using (Stream s = fi.Open(FileMode.Create)) { }
            Assert.IsTrue(fi.Exists);

            fs.CreateDirectory("dir.txt");
            Assert.IsFalse(fs.GetFileInfo("dir.txt").Exists);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTimeUtc(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }

            Assert.GreaterOrEqual(DateTime.UtcNow, fs.GetFileInfo("foo.txt").CreationTimeUtc);
            Assert.LessOrEqual(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)), fs.GetFileInfo("foo.txt").CreationTimeUtc);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CreationTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }

            Assert.GreaterOrEqual(DateTime.Now, fs.GetFileInfo("foo.txt").CreationTime);
            Assert.LessOrEqual(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)), fs.GetFileInfo("foo.txt").CreationTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastAccessTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            fi.LastAccessTime = baseTime;

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.Read)) { }

            Assert.Less(baseTime, fi.LastAccessTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void LastWriteTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            fi.LastWriteTime = baseTime;

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open)) { s.WriteByte(1); }

            Assert.Less(baseTime, fi.LastWriteTime);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Delete(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            fs.GetFileInfo("foo.txt").Delete();

            Assert.IsFalse(fs.FileExists("foo.txt"));
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(FileNotFoundException))]
        [Category("ThrowsException")]
        public void Delete_Dir(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("foo.txt");
            fs.GetFileInfo("foo.txt").Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        [ExpectedException(typeof(FileNotFoundException))]
        [Category("ThrowsException")]
        public void Delete_NoFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.GetFileInfo("foo.txt").Delete();
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void CopyFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            using (Stream s = fi.Create())
            {
                for (int i = 0; i < 10; ++i)
                {
                    s.Write(new byte[111], 0, 111);
                }
            }
            fi.Attributes = FileAttributes.Hidden | FileAttributes.System;

            fi.CopyTo("foo2.txt");

            fi = fs.GetFileInfo("foo2.txt");
            Assert.IsTrue(fi.Exists);
            Assert.AreEqual(1110, fi.Length);
            Assert.AreEqual(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.IsTrue(fi.Exists);

            fi = fs.GetFileInfo("foo2.txt");
            Assert.IsTrue(fi.Exists);
            Assert.AreEqual(1110, fi.Length);
            Assert.AreEqual(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.IsTrue(fi.Exists);
        }


        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void MoveFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            using (Stream s = fi.Create())
            {
                for (int i = 0; i < 10; ++i)
                {
                    s.Write(new byte[111], 0, 111);
                }
            }
            fi.Attributes = FileAttributes.Hidden | FileAttributes.System;

            fi.MoveTo("foo2.txt");

            fi = fs.GetFileInfo("foo2.txt");
            Assert.IsTrue(fi.Exists);
            Assert.AreEqual(1110, fi.Length);
            Assert.AreEqual(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.IsFalse(fi.Exists);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void MoveFile_Overwrite(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");
            using (Stream s = fi.Create())
            {
                s.WriteByte(1);
            }

            DiscFileInfo fi2 = fs.GetFileInfo("foo2.txt");
            using (Stream s = fi2.Create())
            {
            }

            fs.MoveFile("foo.txt", "foo2.txt", true);

            Assert.IsFalse(fi.Exists);
            Assert.IsTrue(fi2.Exists);
            Assert.AreEqual(1, fi2.Length);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Equals(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.AreEqual(fs.GetFileInfo("foo.txt"), fs.GetFileInfo("foo.txt"));
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void Parent(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"SOMEDIR\ADIR");
            using (Stream s = fs.OpenFile(@"SOMEDIR\ADIR\FILE.TXT", FileMode.Create)) { }

            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\ADIR\FILE.TXT");
            Assert.AreEqual(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Parent);
            Assert.AreEqual(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Directory);
        }

        [TestCaseSource(typeof(FileSystemSource), "ReadWriteFileSystems")]
        public void VolumeLabel(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            string volLabel = fs.VolumeLabel;
            Assert.NotNull(volLabel);
        }
    }
}

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

        [Test]
        [ExpectedException(typeof(FileNotFoundException))]
        [Category("ThrowsException")]
        public void Open_FileNotFound()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Open)) { }
        }

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_FileExists()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create)) { s.WriteByte(1); }

            using (Stream s = di.Open(FileMode.CreateNew)) { }
        }

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_DirExists()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            fs.CreateDirectory("FOO.TXT");

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create)) { s.WriteByte(1); }
        }

        [Test]
        public void Open_Read()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

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

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_Read_Fail()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create, FileAccess.Read))
            {
                s.WriteByte(1);
            }
        }

        [Test]
        public void Open_Write()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create, FileAccess.Write))
            {
                Assert.IsTrue(s.CanWrite);
                Assert.IsFalse(s.CanRead);
                s.WriteByte(1);
            }
        }

        [Test]
        [ExpectedException(typeof(IOException))]
        [Category("ThrowsException")]
        public void Open_Write_Fail()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

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

        [Test]
        public void Name()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            Assert.AreEqual("foo.txt", fs.GetFileInfo("foo.txt").Name);
            Assert.AreEqual("foo.txt", fs.GetFileInfo(@"path\foo.txt").Name);
            Assert.AreEqual("foo.txt", fs.GetFileInfo(@"\foo.txt").Name);
        }

        [Test]
        public void Attributes()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");
            using (Stream s = fi.Open(FileMode.Create)) { }

            // Check default attributes
            Assert.AreEqual(FileAttributes.Archive, fi.Attributes);

            // Check round-trip
            FileAttributes newAttrs = FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System;
            fi.Attributes = newAttrs;
            Assert.AreEqual(newAttrs, fi.Attributes);

            // And check persistence to disk
            fs = new FatFileSystem(ms);
            Assert.AreEqual(newAttrs, fs.GetFileInfo("foo.txt").Attributes);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        [Category("ThrowsException")]
        public void Attributes_ChangeType()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");
            using (Stream s = fi.Open(FileMode.Create)) { }

            fi.Attributes = fi.Attributes | FileAttributes.Directory;
        }

        [Test]
        public void Exists()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, null);

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            Assert.IsFalse(fi.Exists);

            using (Stream s = fi.Open(FileMode.Create)) { }
            Assert.IsTrue(fi.Exists);

            fs.CreateDirectory("dir.txt");
            Assert.IsFalse(fs.GetFileInfo("dir.txt").Exists);
        }

        [Test]
        public void CreationTimeUtc()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }

            Assert.GreaterOrEqual(DateTime.UtcNow, fs.GetFileInfo("foo.txt").CreationTimeUtc);
            Assert.LessOrEqual(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)), fs.GetFileInfo("foo.txt").CreationTimeUtc);
        }

        [Test]
        public void CreationTime()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }

            Assert.GreaterOrEqual(DateTime.Now, fs.GetFileInfo("foo.txt").CreationTime);
            Assert.LessOrEqual(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)), fs.GetFileInfo("foo.txt").CreationTime);
        }

        [Test]
        public void LastAccessTime()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            fi.LastAccessTime = baseTime;

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.Read)) { }

            Assert.Less(baseTime, fi.LastAccessTime);
        }

        [Test]
        public void LastWriteTime()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            fi.LastWriteTime = baseTime;

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open)) { s.WriteByte(1); }

            Assert.Less(baseTime, fi.LastWriteTime);
        }

        [Test]
        public void Delete()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            fs.GetFileInfo("foo.txt").Delete();

            Assert.IsFalse(fs.FileExists("foo.txt"));
        }

        [Test]
        [ExpectedException(typeof(FileNotFoundException))]
        [Category("ThrowsException")]
        public void Delete_Dir()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.CreateDirectory("foo.txt");
            fs.GetFileInfo("foo.txt").Delete();
        }

        [Test]
        [ExpectedException(typeof(FileNotFoundException))]
        [Category("ThrowsException")]
        public void Delete_NoFile()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.GetFileInfo("foo.txt").Delete();
        }

        [Test]
        public void CopyFile()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, "FLOPPY_IMG ");
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

            fs = new FatFileSystem(ms);

            fi = fs.GetFileInfo("foo2.txt");
            Assert.IsTrue(fi.Exists);
            Assert.AreEqual(1110, fi.Length);
            Assert.AreEqual(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.IsTrue(fi.Exists);
        }


        [Test]
        public void MoveFile()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, "FLOPPY_IMG ");
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

            fs = new FatFileSystem(ms);

            fi = fs.GetFileInfo("foo2.txt");
            Assert.IsTrue(fi.Exists);
            Assert.AreEqual(1110, fi.Length);
            Assert.AreEqual(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.IsFalse(fi.Exists);
        }

        [Test]
        public void Equals()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            Assert.AreEqual(fs.GetFileInfo("foo.txt"), fs.GetFileInfo("foo.txt"));
        }

        [Test]
        public void Parent()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.DoubleDensity, null);

            fs.CreateDirectory(@"SOMEDIR\ADIR");
            using (Stream s = fs.OpenFile(@"SOMEDIR\ADIR\FILE.TXT", FileMode.Create)) { }

            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\ADIR\FILE.TXT");
            Assert.AreEqual(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Parent);
            Assert.AreEqual(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Directory);
        }

    }
}

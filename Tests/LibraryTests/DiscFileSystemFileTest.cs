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
using DiscUtils;
using Xunit;

namespace LibraryTests
{
    public class DiscFileSystemFileTest
    {
        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreateFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            Assert.True(fi.Exists);
            Assert.Equal(FileAttributes.Archive, fi.Attributes);
            Assert.Equal(1, fi.Length);

            using (Stream s = fs.OpenFile("Foo.txt", FileMode.Open, FileAccess.Read))
            {
                Assert.Equal(1, s.ReadByte());
            }
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void CreateFileInvalid_Long(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Throws<IOException>(() =>
            {
                using (Stream s = fs.GetFileInfo(new string('X', 256)).Open(FileMode.Create, FileAccess.ReadWrite))
                {
                    s.WriteByte(1);
                }
            });
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void CreateFileInvalid_Characters(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Throws<IOException>(() =>
            {
                using (Stream s = fs.GetFileInfo("A\0File").Open(FileMode.Create, FileAccess.ReadWrite))
                {
                    s.WriteByte(1);
                }
            });
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void DeleteFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite)) { }

            Assert.Equal(1, fs.Root.GetFiles().Length);

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            fi.Delete();

            Assert.Equal(0, fs.Root.GetFiles().Length);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Length(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.GetFileInfo("foo.txt").Open(FileMode.Create, FileAccess.ReadWrite))
            {
                s.SetLength(3128);
            }

            Assert.Equal(3128, fs.GetFileInfo("foo.txt").Length);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.ReadWrite))
            {
                s.SetLength(3);
                Assert.Equal(3, s.Length);
            }

            Assert.Equal(3, fs.GetFileInfo("foo.txt").Length);

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.ReadWrite))
            {
                s.SetLength(3333);

                byte[] buffer = new byte[512];
                for (int i = 0; i < buffer.Length; ++i)
                {
                    buffer[i] = (byte)i;
                }

                s.Write(buffer, 0, buffer.Length);
                s.Write(buffer, 0, buffer.Length);

                Assert.Equal(1024, s.Position);
                Assert.Equal(3333, s.Length);

                s.SetLength(512);

                Assert.Equal(512, s.Length);
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
                    Assert.Equal((byte)i, buffer[i]);
                }
            }
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void Open_FileNotFound(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");

            Assert.Throws<FileNotFoundException>(() =>
            {
                using (Stream s = di.Open(FileMode.Open))
                {

                }
            });
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void Open_FileExists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create)) { s.WriteByte(1); }

            Assert.Throws<IOException>(() =>
            {
                using (Stream s = di.Open(FileMode.CreateNew))
                {

                }
            });

        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void Open_DirExists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("FOO.TXT");

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            Assert.Throws<IOException>(() => di.Open(FileMode.Create));
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
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
                Assert.False(s.CanWrite);
                Assert.True(s.CanRead);

                Assert.Equal(1, s.ReadByte());
            }
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void Open_Read_Fail(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create, FileAccess.Read))
            {
                Assert.Throws<IOException>(() => s.WriteByte(1));
            }
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Open_Write(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Create, FileAccess.Write))
            {
                Assert.True(s.CanWrite);
                Assert.False(s.CanRead);
                s.WriteByte(1);
            }
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
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
                Assert.True(s.CanWrite);
                Assert.False(s.CanRead);

                Assert.Throws<IOException>(() => s.ReadByte());
            }
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Name(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Equal("foo.txt", fs.GetFileInfo("foo.txt").Name);
            Assert.Equal("foo.txt", fs.GetFileInfo(@"path\foo.txt").Name);
            Assert.Equal("foo.txt", fs.GetFileInfo(@"\foo.txt").Name);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Attributes(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");
            using (Stream s = fi.Open(FileMode.Create)) { }

            // Check default attributes
            Assert.Equal(FileAttributes.Archive, fi.Attributes);

            // Check round-trip
            FileAttributes newAttrs = FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System;
            fi.Attributes = newAttrs;
            Assert.Equal(newAttrs, fi.Attributes);

            // And check persistence to disk
            Assert.Equal(newAttrs, fs.GetFileInfo("foo.txt").Attributes);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void Attributes_ChangeType(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");
            using (Stream s = fi.Open(FileMode.Create)) { }

            Assert.Throws<ArgumentException>(() => fi.Attributes = fi.Attributes | FileAttributes.Directory);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Exists(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            Assert.False(fi.Exists);

            using (Stream s = fi.Open(FileMode.Create)) { }
            Assert.True(fi.Exists);

            fs.CreateDirectory("dir.txt");
            Assert.False(fs.GetFileInfo("dir.txt").Exists);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreationTimeUtc(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }

            Assert.True(DateTime.UtcNow >= fs.GetFileInfo("foo.txt").CreationTimeUtc);
            Assert.True(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)) <= fs.GetFileInfo("foo.txt").CreationTimeUtc);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void CreationTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }

            Assert.True(DateTime.Now >= fs.GetFileInfo("foo.txt").CreationTime);
            Assert.True(DateTime.Now.Subtract(TimeSpan.FromSeconds(10)) <= fs.GetFileInfo("foo.txt").CreationTime);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void LastAccessTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            DateTime baseTime = DateTime.Now - TimeSpan.FromDays(2);
            fi.LastAccessTime = baseTime;

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open, FileAccess.Read)) { }

            Assert.True(baseTime < fi.LastAccessTime);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void LastWriteTime(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            DateTime baseTime = DateTime.Now - TimeSpan.FromMinutes(10);
            fi.LastWriteTime = baseTime;

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Open)) { s.WriteByte(1); }

            Assert.True(baseTime < fi.LastWriteTime);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Delete(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            using (Stream s = fs.OpenFile("foo.txt", FileMode.Create)) { }
            fs.GetFileInfo("foo.txt").Delete();

            Assert.False(fs.FileExists("foo.txt"));
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void Delete_Dir(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory("foo.txt");

            Assert.Throws<FileNotFoundException>(() => fs.GetFileInfo("foo.txt").Delete());
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        [Trait("Category", "ThrowsException")]
        public void Delete_NoFile(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Throws<FileNotFoundException>(() => fs.GetFileInfo("foo.txt").Delete());
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
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
            Assert.True(fi.Exists);
            Assert.Equal(1110, fi.Length);
            Assert.Equal(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.True(fi.Exists);

            fi = fs.GetFileInfo("foo2.txt");
            Assert.True(fi.Exists);
            Assert.Equal(1110, fi.Length);
            Assert.Equal(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.True(fi.Exists);
        }


        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
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
            Assert.True(fi.Exists);
            Assert.Equal(1110, fi.Length);
            Assert.Equal(FileAttributes.Hidden | FileAttributes.System, fi.Attributes);

            fi = fs.GetFileInfo("foo.txt");
            Assert.False(fi.Exists);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
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

            Assert.False(fi.Exists);
            Assert.True(fi2.Exists);
            Assert.Equal(1, fi2.Length);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Equals(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            Assert.Equal(fs.GetFileInfo("foo.txt"), fs.GetFileInfo("foo.txt"));
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void Parent(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            fs.CreateDirectory(@"SOMEDIR\ADIR");
            using (Stream s = fs.OpenFile(@"SOMEDIR\ADIR\FILE.TXT", FileMode.Create)) { }

            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\ADIR\FILE.TXT");
            Assert.Equal(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Parent);
            Assert.Equal(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Directory);
        }

        [MemberData(nameof(FileSystemSource.ReadWriteFileSystems), MemberType = typeof(FileSystemSource))]
        public void VolumeLabel(NewFileSystemDelegate fsFactory)
        {
            DiscFileSystem fs = fsFactory();

            string volLabel = fs.VolumeLabel;
            Assert.NotNull(volLabel);
        }
    }
}

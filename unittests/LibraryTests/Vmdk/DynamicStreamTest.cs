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

namespace DiscUtils.Vmdk
{
    [TestFixture]
    public class DynamicStreamTest
    {
        [Test]
        public void Attributes()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.MonolithicSparse))
            {
                Stream s = disk.Content;
                Assert.IsTrue(s.CanRead);
                Assert.IsTrue(s.CanWrite);
                Assert.IsTrue(s.CanSeek);
            }
        }

        [Test]
        public void ReadWriteSmall()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk",16 * 1024L * 1024 * 1024, DiskCreateType.TwoGbMaxExtentSparse))
            {
                byte[] content = new byte[100];
                for(int i = 0; i < content.Length; ++i)
                {
                    content[i] = (byte)i;
                }

                Stream s = disk.Content;
                s.Write(content, 10, 40);
                Assert.AreEqual(40, s.Position);
                s.Write(content, 50, 50);
                Assert.AreEqual(90, s.Position);
                s.Position = 0;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 60);
                Assert.AreEqual(60, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual(0, buffer[i]);
                }
                for (int i = 10; i < 60; ++i)
                {
                    Assert.AreEqual(i, buffer[i]);
                }
            }

            // Check the data persisted
            using (Disk disk = new Disk(fs, "a.vmdk", FileAccess.Read))
            {
                Stream s = disk.Content;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 20);
                Assert.AreEqual(20, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual(0, buffer[i]);
                }
                for (int i = 10; i < 20; ++i)
                {
                    Assert.AreEqual(i, buffer[i]);
                }
            }
        }

        [Test]
        public void ReadWriteLarge()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.TwoGbMaxExtentSparse))
            {
                byte[] content = new byte[3 * 1024 * 1024];
                for (int i = 0; i < content.Length / 4; ++i)
                {
                    content[i * 4 + 0] = (byte)((i >> 24) & 0xFF);
                    content[i * 4 + 1] = (byte)((i >> 16) & 0xFF);
                    content[i * 4 + 2] = (byte)((i >> 8) & 0xFF);
                    content[i * 4 + 3] = (byte)(i & 0xFF);
                }

                Stream s = disk.Content;
                s.Position = 10;
                s.Write(content, 0, content.Length);

                byte[] buffer = new byte[content.Length];
                s.Position = 10;
                s.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < content.Length; ++i)
                {
                    if (buffer[i] != content[i])
                    {
                        Assert.Fail();
                    }
                }
            }
        }

        [Test]
        public void DisposeTest()
        {
            Stream contentStream;

            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.TwoGbMaxExtentSparse))
            {
                contentStream = disk.Content;
            }

            try
            {
                contentStream.Position = 0;
                Assert.Fail();
            }
            catch(ObjectDisposedException) { }
        }

        [Test]
        public void DisposeTestMonolithicSparse()
        {
            Stream contentStream;

            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.MonolithicSparse))
            {
                contentStream = disk.Content;
            }

            try
            {
                contentStream.Position = 0;
                Assert.Fail();
            }
            catch (ObjectDisposedException) { }
        }

        [Test]
        public void ReadNotPresent()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.TwoGbMaxExtentSparse))
            {
                byte[] buffer = new byte[100];
                disk.Content.Seek(2 * 1024 * 1024, SeekOrigin.Current);
                disk.Content.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < 100; ++i)
                {
                    if (buffer[i] != 0)
                    {
                        Assert.Fail();
                    }
                }
            }
        }

        [Test]
        public void AttributesVmfs()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.VmfsSparse))
            {
                Stream s = disk.Content;
                Assert.IsTrue(s.CanRead);
                Assert.IsTrue(s.CanWrite);
                Assert.IsTrue(s.CanSeek);
            }
        }

        [Test]
        public void ReadWriteSmallVmfs()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.VmfsSparse))
            {
                byte[] content = new byte[100];
                for (int i = 0; i < content.Length; ++i)
                {
                    content[i] = (byte)i;
                }

                Stream s = disk.Content;
                s.Write(content, 10, 40);
                Assert.AreEqual(40, s.Position);
                s.Write(content, 50, 50);
                Assert.AreEqual(90, s.Position);
                s.Position = 0;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 60);
                Assert.AreEqual(60, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual(0, buffer[i]);
                }
                for (int i = 10; i < 60; ++i)
                {
                    Assert.AreEqual(i, buffer[i]);
                }
            }

            // Check the data persisted
            using (Disk disk = new Disk(fs, "a.vmdk", FileAccess.Read))
            {
                Stream s = disk.Content;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 20);
                Assert.AreEqual(20, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual(0, buffer[i]);
                }
                for (int i = 10; i < 20; ++i)
                {
                    Assert.AreEqual(i, buffer[i]);
                }
            }
        }

        [Test]
        public void ReadWriteLargeVmfs()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.VmfsSparse))
            {
                byte[] content = new byte[3 * 1024 * 1024];
                for (int i = 0; i < content.Length / 4; ++i)
                {
                    content[i * 4 + 0] = (byte)((i >> 24) & 0xFF);
                    content[i * 4 + 1] = (byte)((i >> 16) & 0xFF);
                    content[i * 4 + 2] = (byte)((i >> 8) & 0xFF);
                    content[i * 4 + 3] = (byte)(i & 0xFF);
                }

                Stream s = disk.Content;
                s.Position = 10;
                s.Write(content, 0, content.Length);

                byte[] buffer = new byte[content.Length];
                s.Position = 10;
                s.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < content.Length; ++i)
                {
                    if (buffer[i] != content[i])
                    {
                        Assert.Fail();
                    }
                }
            }
        }

        [Test]
        public void DisposeTestVmfs()
        {
            Stream contentStream;

            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.VmfsSparse))
            {
                contentStream = disk.Content;
            }

            try
            {
                contentStream.Position = 0;
                Assert.Fail();
            }
            catch (ObjectDisposedException) { }
        }

        [Test]
        public void ReadNotPresentVmfs()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.VmfsSparse))
            {
                byte[] buffer = new byte[100];
                disk.Content.Seek(2 * 1024 * 1024, SeekOrigin.Current);
                disk.Content.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < 100; ++i)
                {
                    if (buffer[i] != 0)
                    {
                        Assert.Fail();
                    }
                }
            }
        }
    }
}

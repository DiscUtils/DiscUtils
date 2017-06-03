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
using DiscUtils;
using DiscUtils.Streams;
using DiscUtils.Vmdk;
using Xunit;

namespace LibraryTests.Vmdk
{
    public class DynamicStreamTest
    {
        [Fact]
        public void Attributes()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.MonolithicSparse))
            {
                Stream s = disk.Content;
                Assert.True(s.CanRead);
                Assert.True(s.CanWrite);
                Assert.True(s.CanSeek);
            }
        }

        [Fact]
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
                Assert.Equal(40, s.Position);
                s.Write(content, 50, 50);
                Assert.Equal(90, s.Position);
                s.Position = 0;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 60);
                Assert.Equal(60, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.Equal(0, buffer[i]);
                }
                for (int i = 10; i < 60; ++i)
                {
                    Assert.Equal(i, buffer[i]);
                }
            }

            // Check the data persisted
            using (Disk disk = new Disk(fs, "a.vmdk", FileAccess.Read))
            {
                Stream s = disk.Content;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 20);
                Assert.Equal(20, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.Equal(0, buffer[i]);
                }
                for (int i = 10; i < 20; ++i)
                {
                    Assert.Equal(i, buffer[i]);
                }
            }
        }

        [Fact]
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
                        Assert.True(false);
                    }
                }
            }
        }

        [Fact]
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
                Assert.True(false);
            }
            catch(ObjectDisposedException) { }
        }

        [Fact]
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
                Assert.True(false);
            }
            catch (ObjectDisposedException) { }
        }

        [Fact]
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
                        Assert.True(false);
                    }
                }
            }
        }

        [Fact]
        public void AttributesVmfs()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.VmfsSparse))
            {
                Stream s = disk.Content;
                Assert.True(s.CanRead);
                Assert.True(s.CanWrite);
                Assert.True(s.CanSeek);
            }
        }

        [Fact]
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
                Assert.Equal(40, s.Position);
                s.Write(content, 50, 50);
                Assert.Equal(90, s.Position);
                s.Position = 0;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 60);
                Assert.Equal(60, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.Equal(0, buffer[i]);
                }
                for (int i = 10; i < 60; ++i)
                {
                    Assert.Equal(i, buffer[i]);
                }
            }

            // Check the data persisted
            using (Disk disk = new Disk(fs, "a.vmdk", FileAccess.Read))
            {
                Stream s = disk.Content;

                byte[] buffer = new byte[100];
                s.Read(buffer, 10, 20);
                Assert.Equal(20, s.Position);
                for (int i = 0; i < 10; ++i)
                {
                    Assert.Equal(0, buffer[i]);
                }
                for (int i = 10; i < 20; ++i)
                {
                    Assert.Equal(i, buffer[i]);
                }
            }
        }

        [Fact]
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
                        Assert.True(false);
                    }
                }
            }
        }

        [Fact]
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
                Assert.True(false);
            }
            catch (ObjectDisposedException) { }
        }

        [Fact]
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
                        Assert.True(false);
                    }
                }
            }
        }

        [Fact]
        public void Extents()
        {
            // Fragile - this is the grain size in bytes of the VMDK file, so dependant on algorithm that
            // determines grain size for new VMDKs...
            const int unit = 128 * 512;

            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.TwoGbMaxExtentSparse))
            {
                disk.Content.Position = 20 * unit;
                disk.Content.Write(new byte[4 * unit], 0, 4 * unit);

                // Starts before first extent, ends before end of extent
                List<StreamExtent> extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(0, 21 * unit));
                Assert.Equal(1, extents.Count);
                Assert.Equal(20 * unit, extents[0].Start);
                Assert.Equal(1 * unit, extents[0].Length);

                // Limit to disk content length
                extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(21 * unit, 20 * unit));
                Assert.Equal(1, extents.Count);
                Assert.Equal(21 * unit, extents[0].Start);
                Assert.Equal(3 * unit, extents[0].Length);

                // Out of range
                extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(25 * unit, 4 * unit));
                Assert.Equal(0, extents.Count);

                // Non-unit multiples
                extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(21 * unit + 10, 20 * unit));
                Assert.Equal(1, extents.Count);
                Assert.Equal(21 * unit + 10, extents[0].Start);
                Assert.Equal(3 * unit - 10, extents[0].Length);
            }
        }
    }
}

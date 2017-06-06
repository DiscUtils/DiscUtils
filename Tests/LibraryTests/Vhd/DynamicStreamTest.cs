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
using DiscUtils.Vhd;
using Xunit;

namespace LibraryTests.Vhd
{
    public class DynamicStreamTest
    {
        [Fact]
        public void Attributes()
        {
            MemoryStream stream = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(stream, Ownership.Dispose, 16 * 1024L * 1024 * 1024))
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
            MemoryStream stream = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(stream, Ownership.None, 16 * 1024L * 1024 * 1024))
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
            using (Disk disk = new Disk(stream, Ownership.Dispose))
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
            MemoryStream stream = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(stream, Ownership.Dispose, 16 * 1024L * 1024 * 1024))
            {
                byte[] content = new byte[3 * 1024 * 1024];
                for (int i = 0; i < content.Length; ++i)
                {
                    content[i] = (byte)i;
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

            MemoryStream stream = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(stream, Ownership.None, 16 * 1024L * 1024 * 1024))
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
        public void ReadNotPresent()
        {
            MemoryStream stream = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(stream, Ownership.Dispose, 16 * 1024L * 1024 * 1024))
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
            MemoryStream stream = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(stream, Ownership.Dispose, 16 * 1024L * 1024 * 1024))
            {
                disk.Content.Position = 20 * 512;
                disk.Content.Write(new byte[4 * 512], 0, 4 * 512);

                // Starts before first extent, ends before end of extent
                List<StreamExtent> extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(0, 21 * 512));
                Assert.Equal(1, extents.Count);
                Assert.Equal(20 * 512, extents[0].Start);
                Assert.Equal(1 * 512, extents[0].Length);

                // Limit to disk content length
                extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(21 * 512, 20 * 512));
                Assert.Equal(1, extents.Count);
                Assert.Equal(21 * 512, extents[0].Start);
                Assert.Equal(3 * 512, extents[0].Length);

                // Out of range
                extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(25 * 512, 4 * 512));
                Assert.Equal(0, extents.Count);

                // Non-sector multiples
                extents = new List<StreamExtent>(disk.Content.GetExtentsInRange(21 * 512 + 10, 20 * 512));
                Assert.Equal(1, extents.Count);
                Assert.Equal(21 * 512 + 10, extents[0].Start);
                Assert.Equal(3 * 512 - 10, extents[0].Length);
            }
        }
    }
}

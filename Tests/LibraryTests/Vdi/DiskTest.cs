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
using DiscUtils.Streams;
using DiscUtils.Vdi;
using Xunit;

namespace LibraryTests.Vdi
{
    public class DiskTest
    {
        [Fact]
        public void InitializeFixed()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeFixed(ms, Ownership.None, 8 * 1024 * 1024))
            {
                Assert.NotNull(disk);
                Assert.True(disk.Geometry.Capacity > 7.5 * 1024 * 1024 && disk.Geometry.Capacity < 8 * 1024 * 1024);
                Assert.True(disk.Geometry.Capacity <= disk.Content.Length);
            }

            // Check the stream is still valid
            ms.ReadByte();
            ms.Dispose();
        }

        [Fact]
        public void InitializeFixedOwnStream()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeFixed(ms, Ownership.Dispose, 8 * 1024 * 1024))
            {
            }
            Assert.Throws<ObjectDisposedException>(() => ms.ReadByte());
        }

        [Fact]
        public void InitializeDynamic()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.None, 16 * 1024L * 1024 * 1024))
            {
                Assert.NotNull(disk);
                Assert.True(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity < 16 * 1024L * 1024 * 1024);
                Assert.True(disk.Geometry.Capacity <= disk.Content.Length);
            }

            Assert.True(1 * 1024 * 1024 > ms.Length);

            using (Disk disk = new Disk(ms))
            {
                Assert.True(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity < 16 * 1024L * 1024 * 1024);
                Assert.True(disk.Geometry.Capacity <= disk.Content.Length);
            }
        }

        [Fact]
        public void ConstructorDynamic()
        {
            Geometry geometry;
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.None, 16 * 1024L * 1024 * 1024))
            {
                geometry = disk.Geometry;
            }
            using (Disk disk = new Disk(ms))
            {
                Assert.Equal(geometry, disk.Geometry);
                Assert.NotNull(disk.Content);
            }
            using (Disk disk = new Disk(ms, Ownership.Dispose))
            {
                Assert.Equal(geometry, disk.Geometry);
                Assert.NotNull(disk.Content);
            }
        }
    }
}

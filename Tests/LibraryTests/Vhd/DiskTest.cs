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
using NUnit.Framework;

namespace DiscUtils.Vhd
{
    [TestFixture]
    public class DiskTest
    {
        [Test]
        public void InitializeFixed()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeFixed(ms, Ownership.None, 8 * 1024 * 1024))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 7.5 * 1024 * 1024 && disk.Geometry.Capacity <= 8 * 1024 * 1024);
            }

            // Check the stream is still valid
            ms.ReadByte();
            ms.Dispose();
        }

        [Test]
        [ExpectedException(typeof(ObjectDisposedException))]
        public void InitializeFixedOwnStream()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeFixed(ms, Ownership.Dispose, 8 * 1024 * 1024))
            {
            }
            ms.ReadByte();
        }

        [Test]
        public void InitializeDynamic()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.None, 16 * 1024L * 1024 * 1024))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity <= 16 * 1024L * 1024 * 1024);
            }

            Assert.Greater(1 * 1024 * 1024, ms.Length);

            using (Disk disk = new Disk(ms, Ownership.Dispose))
            {
                Assert.That(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity <= 16 * 1024L * 1024 * 1024);
            }
        }

        [Test]
        public void InitializeDifferencing()
        {
            MemoryStream baseStream = new MemoryStream();
            MemoryStream diffStream = new MemoryStream();
            DiskImageFile baseFile = DiskImageFile.InitializeDynamic(baseStream, Ownership.Dispose, 16 * 1024L * 1024 * 1024);
            using (Disk disk = Disk.InitializeDifferencing(diffStream, Ownership.None, baseFile, Ownership.Dispose, @"C:\TEMP\Base.vhd", @".\Base.vhd", DateTime.UtcNow))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity <= 16 * 1024L * 1024 * 1024);
                Assert.That(disk.Geometry.Capacity == baseFile.Geometry.Capacity);
                Assert.AreEqual(2, new List<VirtualDiskLayer>(disk.Layers).Count);
            }
            Assert.Greater(1 * 1024 * 1024, diffStream.Length);
            diffStream.Dispose();
        }

        [Test]
        public void ConstructorDynamic()
        {
            Geometry geometry;
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.None, 16 * 1024L * 1024 * 1024))
            {
                geometry = disk.Geometry;
            }
            using (Disk disk = new Disk(ms, Ownership.None))
            {
                Assert.AreEqual(geometry, disk.Geometry);
                Assert.IsNotNull(disk.Content);
            }
            using (Disk disk = new Disk(ms, Ownership.Dispose))
            {
                Assert.AreEqual(geometry, disk.Geometry);
                Assert.IsNotNull(disk.Content);
            }
        }

        [Test]
        public void ConstructorFromFiles()
        {
            MemoryStream baseStream = new MemoryStream();
            DiskImageFile baseFile = DiskImageFile.InitializeDynamic(baseStream, Ownership.Dispose, 16 * 1024L * 1024 * 1024);

            MemoryStream childStream = new MemoryStream();
            DiskImageFile childFile = DiskImageFile.InitializeDifferencing(childStream, Ownership.Dispose, baseFile, @"C:\temp\foo.vhd", @".\foo.vhd", DateTime.Now);

            MemoryStream grandChildStream = new MemoryStream();
            DiskImageFile grandChildFile = DiskImageFile.InitializeDifferencing(grandChildStream, Ownership.Dispose, childFile, @"C:\temp\child1.vhd", @".\child1.vhd", DateTime.Now);

            using (Disk disk = new Disk(new DiskImageFile[]{grandChildFile, childFile, baseFile}, Ownership.Dispose))
            {
                Assert.IsNotNull(disk.Content);
            }
        }

        [Test]
        public void UndisposedChangedDynamic()
        {
            byte[] firstSector = new byte[512];
            byte[] lastSector = new byte[512];

            MemoryStream ms = new MemoryStream();
            using (Disk newDisk = Disk.InitializeDynamic(ms, Ownership.None, 16 * 1024L * 1024 * 1024))
            {
            }

            using (Disk disk = new Disk(ms, Ownership.None))
            {
                disk.Content.Write(new byte[1024], 0, 1024);

                ms.Position = 0;
                ms.Read(firstSector, 0, 512);
                ms.Seek(-512, SeekOrigin.End);
                ms.Read(lastSector, 0, 512);
                Assert.AreEqual(firstSector, lastSector);
            }

            // Check disabling AutoCommit really doesn't do the commit
            using (Disk disk = new Disk(ms, Ownership.None))
            {
                disk.AutoCommitFooter = false;
                disk.Content.Position = 10 * 1024 * 1024;
                disk.Content.Write(new byte[1024], 0, 1024);

                ms.Position = 0;
                ms.Read(firstSector, 0, 512);
                ms.Seek(-512, SeekOrigin.End);
                ms.Read(lastSector, 0, 512);
                Assert.AreNotEqual(firstSector, lastSector);
            }

            // Also check that after disposing, the commit happens
            ms.Position = 0;
            ms.Read(firstSector, 0, 512);
            ms.Seek(-512, SeekOrigin.End);
            ms.Read(lastSector, 0, 512);
            Assert.AreEqual(firstSector, lastSector);


            // Finally, check default value for AutoCommit lines up with behaviour
            using (Disk disk = new Disk(ms, Ownership.None))
            {
                Assert.True(disk.AutoCommitFooter);
            }
        }


    }
}

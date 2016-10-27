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

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DiscUtils.Vmdk
{
    [TestFixture]
    public class DiskTest
    {
        [Test]
        public void InitializeFixed()
        {
            using (Disk disk = Disk.Initialize(new InMemoryFileSystem(), "a.vmdk", 8 * 1024 * 1024, DiskCreateType.MonolithicFlat))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 7.9 * 1024 * 1024 && disk.Geometry.Capacity < 8.1 * 1024 * 1024);
                Assert.That(disk.Geometry.Capacity == disk.Content.Length);

                List<DiskImageFile> links = new List<DiskImageFile>(disk.Links);
                List<string> paths = new List<string>(links[0].ExtentPaths);
                Assert.AreEqual(1, paths.Count);
                Assert.AreEqual("a-flat.vmdk", paths[0]);
            }
        }

        [Test]
        public void InitializeFixedIDE()
        {
            using (Disk disk = Disk.Initialize(new InMemoryFileSystem(), "a.vmdk", 8 * 1024 * 1024, DiskCreateType.MonolithicFlat, DiskAdapterType.Ide))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 7.9 * 1024 * 1024 && disk.Geometry.Capacity < 8.1 * 1024 * 1024);
                Assert.That(disk.Geometry.Capacity == disk.Content.Length);

                List<DiskImageFile> links = new List<DiskImageFile>(disk.Links);
                List<string> paths = new List<string>(links[0].ExtentPaths);
                Assert.AreEqual(1, paths.Count);
                Assert.AreEqual("a-flat.vmdk", paths[0]);
            }
        }

        [Test]
        public void InitializeDynamic()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.MonolithicSparse))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity <= 16 * 1024L * 1024 * 1024);
                Assert.That(disk.Content.Length == 16 * 1024L * 1024 * 1024);
            }

            Assert.Greater(fs.GetFileLength("a.vmdk"), 2 * 1024 * 1024);
            Assert.Less(fs.GetFileLength("a.vmdk"), 4 * 1024 * 1024);

            using (Disk disk = new Disk(fs, "a.vmdk", FileAccess.Read))
            {
                Assert.That(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity <= 16 * 1024L * 1024 * 1024);
                Assert.That(disk.Content.Length == 16 * 1024L * 1024 * 1024);

                List<DiskImageFile> links = new List<DiskImageFile>(disk.Links);
                List<string> paths = new List<string>(links[0].ExtentPaths);
                Assert.AreEqual(1, paths.Count);
                Assert.AreEqual("a.vmdk", paths[0]);
            }
        }

        [Test]
        public void InitializeDifferencing()
        {
            DiscFileSystem fs = new InMemoryFileSystem();

            DiskImageFile baseFile = DiskImageFile.Initialize(fs, @"\base\base.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.MonolithicSparse);
            using (Disk disk = Disk.InitializeDifferencing(fs, @"\diff\diff.vmdk", DiskCreateType.MonolithicSparse, @"\base\base.vmdk"))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity < 16 * 1024L * 1024 * 1024);
                Assert.That(disk.Content.Length == 16 * 1024L * 1024 * 1024);
                Assert.AreEqual(2, new List<VirtualDiskLayer>(disk.Layers).Count);

                List<DiskImageFile> links = new List<DiskImageFile>(disk.Links);
                Assert.AreEqual(2, links.Count);

                List<string> paths = new List<string>(links[0].ExtentPaths);
                Assert.AreEqual(1, paths.Count);
                Assert.AreEqual("diff.vmdk", paths[0]);
            }
            Assert.Greater(fs.GetFileLength(@"\diff\diff.vmdk"), 2 * 1024 * 1024);
            Assert.Less(fs.GetFileLength(@"\diff\diff.vmdk"), 4 * 1024 * 1024);
        }

        [Test]
        public void InitializeDifferencingRelPath()
        {
            DiscFileSystem fs = new InMemoryFileSystem();

            DiskImageFile baseFile = DiskImageFile.Initialize(fs, @"\dir\subdir\base.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.MonolithicSparse);
            using (Disk disk = Disk.InitializeDifferencing(fs, @"\dir\diff.vmdk", DiskCreateType.MonolithicSparse, @"subdir\base.vmdk"))
            {
                Assert.IsNotNull(disk);
                Assert.That(disk.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && disk.Geometry.Capacity < 16 * 1024L * 1024 * 1024);
                Assert.That(disk.Content.Length == 16 * 1024L * 1024 * 1024);
                Assert.AreEqual(2, new List<VirtualDiskLayer>(disk.Layers).Count);
            }
            Assert.Greater(fs.GetFileLength(@"\dir\diff.vmdk"), 2 * 1024 * 1024);
            Assert.Less(fs.GetFileLength(@"\dir\diff.vmdk"), 4 * 1024 * 1024);
        }

        [Test]
        public void ReadOnlyHosted()
        {
            DiscFileSystem fs = new InMemoryFileSystem();
            using (Disk disk = Disk.Initialize(fs, "a.vmdk", 16 * 1024L * 1024 * 1024, DiskCreateType.MonolithicSparse))
            {
            }

            Disk d2 = new Disk(fs, "a.vmdk", FileAccess.Read);
            Assert.IsFalse(d2.Content.CanWrite);
        }
    }
}

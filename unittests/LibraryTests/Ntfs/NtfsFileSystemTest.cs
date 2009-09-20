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

using System.IO;
using NUnit.Framework;

namespace DiscUtils.Ntfs
{
    [TestFixture]
    public class NtfsFileSystemTest
    {
        [Test]
        public void ReparsePoints_Empty()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.CreateDirectory("dir");
            ntfs.SetReparsePoint("dir", new ReparsePoint(12345, new byte[0]));

            ReparsePoint rp = ntfs.GetReparsePoint("dir");

            Assert.AreEqual(12345, rp.Tag);
            Assert.IsNotNull(rp.Content);
            Assert.AreEqual(0, rp.Content.Length);
        }

        [Test]
        public void ReparsePoints_NonEmpty()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.CreateDirectory("dir");
            ntfs.SetReparsePoint("dir", new ReparsePoint(123, new byte[] { 4, 5, 6 }));

            ReparsePoint rp = ntfs.GetReparsePoint("dir");

            Assert.AreEqual(123, rp.Tag);
            Assert.IsNotNull(rp.Content);
            Assert.AreEqual(3, rp.Content.Length);
        }

        [Test]
        public void Format_SmallDisk()
        {
            long size = 8 * 1024 * 1024;
            SparseMemoryStream partStream = new SparseMemoryStream();
            //VirtualDisk disk = Vhd.Disk.InitializeDynamic(partStream, Ownership.Dispose, size);
            NtfsFileSystem.Format(partStream, "New Partition", Geometry.FromCapacity(size), 0, size / 512);

            NtfsFileSystem ntfs = new NtfsFileSystem(partStream);
            ntfs.Dump(TextWriter.Null, "");
        }

        [Test]
        public void Format_LargeDisk()
        {
            long size = 320 * 1024 * 1024L * 1024;
            SparseMemoryStream partStream = new SparseMemoryStream();
            //VirtualDisk disk = Vhd.Disk.InitializeDynamic(partStream, Ownership.Dispose, size);
            NtfsFileSystem.Format(partStream, "New Partition", Geometry.FromCapacity(size), 0, size / 512);

            NtfsFileSystem ntfs = new NtfsFileSystem(partStream);
            ntfs.Dump(TextWriter.Null, "");
        }
    }
}

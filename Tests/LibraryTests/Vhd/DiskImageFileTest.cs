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

namespace DiscUtils.Vhd
{
    [TestFixture]
    public class DiskImageFileTest
    {
        [Test]
        public void InitializeDifferencing()
        {
            MemoryStream baseStream = new MemoryStream();
            MemoryStream diffStream = new MemoryStream();
            using (DiskImageFile baseFile = DiskImageFile.InitializeDynamic(baseStream, Ownership.Dispose, 16 * 1024L * 1024 * 1024))
            {
                using (DiskImageFile diffFile = DiskImageFile.InitializeDifferencing(diffStream, Ownership.None, baseFile, @"C:\TEMP\Base.vhd", @".\Base.vhd", new DateTime(2007, 12, 31)))
                {
                    Assert.IsNotNull(diffFile);
                    Assert.That(diffFile.Geometry.Capacity > 15.8 * 1024L * 1024 * 1024 && diffFile.Geometry.Capacity < 16 * 1024L * 1024 * 1024);
                    Assert.IsTrue(diffFile.IsSparse);
                    Assert.AreNotEqual(diffFile.CreationTimestamp, new DateTime(2007, 12, 31));
                }
            }
            Assert.Greater(1 * 1024 * 1024, diffStream.Length);
        }

        [Test]
        public void GetParentLocations()
        {
            MemoryStream baseStream = new MemoryStream();
            MemoryStream diffStream = new MemoryStream();
            using (DiskImageFile baseFile = DiskImageFile.InitializeDynamic(baseStream, Ownership.Dispose, 16 * 1024L * 1024 * 1024))
            {
                // Write some data - exposes bug if mis-calculating where to write data
                using (DiskImageFile diffFile = DiskImageFile.InitializeDifferencing(diffStream, Ownership.None, baseFile, @"C:\TEMP\Base.vhd", @".\Base.vhd", new DateTime(2007, 12, 31)))
                {
                    Disk disk = new Disk(new DiskImageFile[] { diffFile, baseFile }, Ownership.None);
                    disk.Content.Write(new byte[512], 0, 512);
                }
            }

            using (DiskImageFile diffFile = new DiskImageFile(diffStream))
            {
                // Testing the obsolete method - disable warning
#pragma warning disable 618
                string[] locations = diffFile.GetParentLocations(@"E:\FOO\");
#pragma warning restore 618
                Assert.AreEqual(2, locations.Length);
                Assert.AreEqual(@"C:\TEMP\Base.vhd", locations[0]);
                Assert.AreEqual(@"E:\FOO\Base.vhd", locations[1]);
            }

            using (DiskImageFile diffFile = new DiskImageFile(diffStream))
            {
                // Testing the new method - note relative path because diff file initialized without a path
                string[] locations = diffFile.GetParentLocations();
                Assert.AreEqual(2, locations.Length);
                Assert.AreEqual(@"C:\TEMP\Base.vhd", locations[0]);
                Assert.AreEqual(@".\Base.vhd", locations[1]);
            }
        }

        [Test]
        public void FooterMissing()
        {
            //
            // Simulates a partial failure extending the file, that the file footer is corrupt - should read start of the file instead.
            //

            Geometry geometry;

            MemoryStream stream = new MemoryStream();
            using (DiskImageFile file = DiskImageFile.InitializeDynamic(stream, Ownership.None, 16 * 1024L * 1024 * 1024))
            {
                geometry = file.Geometry;
            }

            stream.SetLength(stream.Length - 512);

            using (DiskImageFile file = new DiskImageFile(stream))
            {
                Assert.AreEqual(geometry, file.Geometry);
            }
        }
    }
}

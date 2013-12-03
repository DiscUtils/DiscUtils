//
// Copyright (c) 2008-2013, Kenneth Bell
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

namespace DiscUtils.Vhdx
{
    [TestFixture]
    public class DiskBuilderTest
    {
        private SparseStream diskContent;

        [SetUp]
        public void Setup()
        {
            MemoryStream fileStream = new MemoryStream();
            Disk baseFile = Disk.InitializeDynamic(fileStream, Ownership.Dispose, 16 * 1024L * 1024);
            for (int i = 0; i < 8; i += 1024 * 1024)
            {
                baseFile.Content.Position = i;
                baseFile.Content.WriteByte((byte)i);
            }

            baseFile.Content.Position = 15 * 1024 * 1024;
            baseFile.Content.WriteByte(0xFF);

            diskContent = baseFile.Content;
        }

        [Test]
        [Ignore]
        public void BuildFixed()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskType.Fixed;
            builder.Content = diskContent;


            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.AreEqual(1, fileSpecs.Length);
            Assert.AreEqual("foo.vhdx", fileSpecs[0].Name);

            using (Disk disk = new Disk(fileSpecs[0].OpenStream(), Ownership.Dispose))
            {
                for (int i = 0; i < 8; i += 1024 * 1024)
                {
                    disk.Content.Position = i;
                    Assert.AreEqual(i, disk.Content.ReadByte());
                }

                disk.Content.Position = 15 * 1024 * 1024;
                Assert.AreEqual(0xFF, disk.Content.ReadByte());
            }
        }

        [Test]
        public void BuildEmptyDynamic()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskType.Dynamic;
            builder.Content = new SparseMemoryStream();


            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.AreEqual(1, fileSpecs.Length);
            Assert.AreEqual("foo.vhdx", fileSpecs[0].Name);

            using (Disk disk = new Disk(fileSpecs[0].OpenStream(), Ownership.Dispose))
            {
                Assert.AreEqual(0, disk.Content.Length);
            }
        }

        [Test]
        [Ignore]
        public void BuildDynamic()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskType.Dynamic;
            builder.Content = diskContent;


            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.AreEqual(1, fileSpecs.Length);
            Assert.AreEqual("foo.vhdx", fileSpecs[0].Name);

            using (Disk disk = new Disk(fileSpecs[0].OpenStream(), Ownership.Dispose))
            {
                for (int i = 0; i < 8; i += 1024 * 1024)
                {
                    disk.Content.Position = i;
                    Assert.AreEqual(i, disk.Content.ReadByte());
                }

                disk.Content.Position = 15 * 1024 * 1024;
                Assert.AreEqual(0xFF, disk.Content.ReadByte());
            }
        }
    }
}

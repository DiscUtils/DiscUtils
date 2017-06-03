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

using System.IO;
using DiscUtils;
using DiscUtils.Streams;
using DiscUtils.Vmdk;
using Xunit;

namespace LibraryTests.Vmdk
{
    public class DiskBuilderTest
    {
        private SparseStream diskContent;
        
        public DiskBuilderTest()
        {
            MemoryStream fileStream = new MemoryStream();
            DiscUtils.Vhd.Disk baseFile = DiscUtils.Vhd.Disk.InitializeDynamic(fileStream, Ownership.Dispose, 16 * 1024L * 1024);
            for (int i = 0; i < 8; i += 1024 * 1024)
            {
                baseFile.Content.Position = i;
                baseFile.Content.WriteByte((byte)i);
            }

            baseFile.Content.Position = 15 * 1024 * 1024;
            baseFile.Content.WriteByte(0xFF);

            diskContent = baseFile.Content;
        }

        [Fact]
        public void BuildFixed()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskCreateType.Vmfs;
            builder.Content = diskContent;


            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.Equal(2, fileSpecs.Length);
            Assert.Equal("foo.vmdk", fileSpecs[0].Name);

            DiskBuilderFileSystem dbfs = new DiskBuilderFileSystem(fileSpecs);

            using (Disk disk = new Disk(dbfs, "foo.vmdk", FileAccess.Read))
            {
                for (int i = 0; i < 8; i += 1024 * 1024)
                {
                    disk.Content.Position = i;
                    Assert.Equal(i, disk.Content.ReadByte());
                }

                disk.Content.Position = 15 * 1024 * 1024;
                Assert.Equal(0xFF, disk.Content.ReadByte());
            }
        }

        [Fact]
        public void BuildDynamic()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskCreateType.VmfsSparse;
            builder.Content = diskContent;


            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.Equal(2, fileSpecs.Length);
            Assert.Equal("foo.vmdk", fileSpecs[0].Name);


            DiskBuilderFileSystem dbfs = new DiskBuilderFileSystem(fileSpecs);

            using (Disk disk = new Disk(dbfs, "foo.vmdk", FileAccess.Read))
            {
                for (int i = 0; i < 8; i += 1024 * 1024)
                {
                    disk.Content.Position = i;
                    Assert.Equal(i, disk.Content.ReadByte());
                }

                disk.Content.Position = 15 * 1024 * 1024;
                Assert.Equal(0xFF, disk.Content.ReadByte());
            }
        }
    }
}

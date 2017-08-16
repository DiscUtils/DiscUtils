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

using DiscUtils;
using DiscUtils.Streams;
using DiscUtils.Vhdx;
using Xunit;
using Xunit.Sdk;

namespace LibraryTests.Vhdx
{
    public class DiskBuilderTest
    {
        private SparseStream diskContent;

        public DiskBuilderTest()
        {
            SparseMemoryStream sourceStream = new SparseMemoryStream();
            sourceStream.SetLength(160 * 1024L * 1024);
            for (int i = 0; i < 8; ++i)
            {
                sourceStream.Position = i * 1024L * 1024;
                sourceStream.WriteByte((byte)i);
            }

            sourceStream.Position = 150 * 1024 * 1024;
            sourceStream.WriteByte(0xFF);

            diskContent = sourceStream;
        }

        [Fact(Skip = "Ported from DiscUtils")]
        public void BuildFixed()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskType.Fixed;
            builder.Content = diskContent;


            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.Equal(1, fileSpecs.Length);
            Assert.Equal("foo.vhdx", fileSpecs[0].Name);

            using (Disk disk = new Disk(fileSpecs[0].OpenStream(), Ownership.Dispose))
            {
                for (int i = 0; i < 8; ++i)
                {
                    disk.Content.Position = i * 1024L * 1024;
                    Assert.Equal(i, disk.Content.ReadByte());
                }

                disk.Content.Position = 150 * 1024 * 1024;
                Assert.Equal(0xFF, disk.Content.ReadByte());
            }
        }

        [Fact]
        public void BuildEmptyDynamic()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskType.Dynamic;
            builder.Content = new SparseMemoryStream();


            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.Equal(1, fileSpecs.Length);
            Assert.Equal("foo.vhdx", fileSpecs[0].Name);

            using (Disk disk = new Disk(fileSpecs[0].OpenStream(), Ownership.Dispose))
            {
                Assert.Equal(0, disk.Content.Length);
            }
        }

        [Fact]
        public void BuildDynamic()
        {
            DiskBuilder builder = new DiskBuilder();
            builder.DiskType = DiskType.Dynamic;
            builder.Content = diskContent;

            DiskImageFileSpecification[] fileSpecs = builder.Build("foo");
            Assert.Equal(1, fileSpecs.Length);
            Assert.Equal("foo.vhdx", fileSpecs[0].Name);

            using (Disk disk = new Disk(fileSpecs[0].OpenStream(), Ownership.Dispose))
            {
                for (int i = 0; i < 8; ++i)
                {
                    disk.Content.Position = i * 1024L * 1024;
                    Assert.Equal(i, disk.Content.ReadByte());
                }

                disk.Content.Position = 150 * 1024 * 1024;
                Assert.Equal(0xFF, disk.Content.ReadByte());
            }
        }
    }
}

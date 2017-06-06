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
using DiscUtils.Iso9660;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests.Iso9660
{
    public class IsoFileSystemTest
    {
        [Fact]
        public void CanWrite()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);
            Assert.False(fs.CanWrite);
        }

        [Fact]
        public void FileInfo()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);
            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\SOMEFILE.TXT");
            Assert.NotNull(fi);
        }

        [Fact]
        public void DirectoryInfo()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);
            DiscDirectoryInfo fi = fs.GetDirectoryInfo(@"SOMEDIR");
            Assert.NotNull(fi);
        }

        [Fact]
        public void FileSystemInfo()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);
            DiscFileSystemInfo fi = fs.GetFileSystemInfo(@"SOMEDIR\SOMEFILE");
            Assert.NotNull(fi);
        }

        [Fact]
        public void Root()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);
            Assert.NotNull(fs.Root);
            Assert.True(fs.Root.Exists);
            Assert.Empty(fs.Root.Name);
            Assert.Null(fs.Root.Parent);
        }

        [Fact]
        public void LargeDirectory()
        {
            CDBuilder builder = new CDBuilder();
            builder.UseJoliet = true;

            for (int i = 0; i < 3000; ++i)
            {
                builder.AddFile("FILE" + i + ".TXT", new byte[] { });
            }

            CDReader reader = new CDReader(builder.Build(), true);

            Assert.Equal(3000, reader.Root.GetFiles().Length);
        }

        [Fact]
        public void HideVersions()
        {
            CDBuilder builder = new CDBuilder();
            builder.UseJoliet = true;
            builder.AddFile("FILE.TXT;1", new byte[] { });

            MemoryStream ms = new MemoryStream();
            SparseStream.Pump(builder.Build(), ms);

            CDReader reader = new CDReader(ms, true, false);
            Assert.Equal("\\FILE.TXT;1", reader.GetFiles("")[0]);
            Assert.Equal("\\FILE.TXT;1", reader.GetFileSystemEntries("")[0]);

            reader = new CDReader(ms, true, true);
            Assert.Equal("\\FILE.TXT", reader.GetFiles("")[0]);
            Assert.Equal("\\FILE.TXT", reader.GetFileSystemEntries("")[0]);
        }
    }
}

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
using DiscUtils.Iso9660;
using Xunit;

namespace LibraryTests.Iso9660
{
    public class IsoFileInfoTest
    {
        [Fact]
        public void Length()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"FILE.TXT", new byte[0]);
            builder.AddFile(@"FILE2.TXT", new byte[1]);
            builder.AddFile(@"FILE3.TXT", new byte[10032]);
            builder.AddFile(@"FILE3.TXT;2", new byte[132]);
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal(0, fs.GetFileInfo("FILE.txt").Length);
            Assert.Equal(1, fs.GetFileInfo("FILE2.txt").Length);
            Assert.Equal(10032, fs.GetFileInfo("FILE3.txt;1").Length);
            Assert.Equal(132, fs.GetFileInfo("FILE3.txt;2").Length);
            Assert.Equal(132, fs.GetFileInfo("FILE3.txt").Length);
        }

        [Fact]
        [Trait("Category", "ThrowsException")]
        public void Open_FileNotFound()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            Assert.Throws<FileNotFoundException>(() =>
            {
                using (Stream s = di.Open(FileMode.Open))
                {
                }
            });
        }

        [Fact]
        public void Open_Read()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile("foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo di = fs.GetFileInfo("foo.txt");
            using (Stream s = di.Open(FileMode.Open, FileAccess.Read))
            {
                Assert.False(s.CanWrite);
                Assert.True(s.CanRead);

                Assert.Equal(1, s.ReadByte());
            }
        }

        [Fact]
        public void Name()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal("foo.txt", fs.GetFileInfo("foo.txt").Name);
            Assert.Equal("foo.txt", fs.GetFileInfo(@"path\foo.txt").Name);
            Assert.Equal("foo.txt", fs.GetFileInfo(@"\foo.txt").Name);
        }

        [Fact]
        public void Attributes()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile("foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo fi = fs.GetFileInfo("foo.txt");

            // Check default attributes
            Assert.Equal(FileAttributes.ReadOnly, fi.Attributes);
        }

        [Fact]
        public void Exists()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"dir\foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.False(fs.GetFileInfo("unknown.txt").Exists);
            Assert.True(fs.GetFileInfo(@"dir\foo.txt").Exists);
            Assert.False(fs.GetFileInfo(@"dir").Exists);
        }

        [Fact]
        public void CreationTimeUtc()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"foo.txt", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.True(DateTime.UtcNow >= fs.GetFileInfo("foo.txt").CreationTimeUtc);
            Assert.True(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)) <= fs.GetFileInfo("foo.txt").CreationTimeUtc);
        }

        [Fact]
        public void FileInfoEquals()
        {
            CDBuilder builder = new CDBuilder();
            CDReader fs = new CDReader(builder.Build(), false);

            Assert.Equal(fs.GetFileInfo("foo.txt"), fs.GetFileInfo("foo.txt"));
        }

        [Fact]
        public void Parent()
        {
            CDBuilder builder = new CDBuilder();
            builder.AddFile(@"SOMEDIR\ADIR\FILE.TXT", new byte[] { 1 });
            CDReader fs = new CDReader(builder.Build(), false);

            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\ADIR\FILE.TXT");
            Assert.Equal(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Parent);
            Assert.Equal(fs.GetDirectoryInfo(@"SOMEDIR\ADIR"), fi.Directory);
        }
    }
}

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
using DiscUtils.SquashFs;
using Xunit;

namespace LibraryTests.SquashFs
{
    public sealed class SquashFileSystemBuilderTest
    {
        [Fact]
        public void SingleFile()
        {
            MemoryStream fsImage = new MemoryStream();

            SquashFileSystemBuilder builder = new SquashFileSystemBuilder();
            builder.AddFile("file", new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            builder.Build(fsImage);

            SquashFileSystemReader reader = new SquashFileSystemReader(fsImage);
            Assert.Equal(1, reader.GetFileSystemEntries("\\").Length);
            Assert.Equal(4, reader.GetFileLength("file"));
            Assert.True(reader.FileExists("file"));
            Assert.False(reader.DirectoryExists("file"));
            Assert.False(reader.FileExists("otherfile"));
        }

        [Fact]
        public void CreateDirs()
        {
            MemoryStream fsImage = new MemoryStream();

            SquashFileSystemBuilder builder = new SquashFileSystemBuilder();
            builder.AddFile(@"\adir\anotherdir\file", new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            builder.Build(fsImage);

            SquashFileSystemReader reader = new SquashFileSystemReader(fsImage);
            Assert.True(reader.DirectoryExists(@"adir"));
            Assert.True(reader.DirectoryExists(@"adir\anotherdir"));
            Assert.True(reader.FileExists(@"adir\anotherdir\file"));
        }

        [Fact]
        public void Defaults()
        {
            MemoryStream fsImage = new MemoryStream();

            SquashFileSystemBuilder builder = new SquashFileSystemBuilder();
            builder.AddFile(@"file", new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            builder.AddDirectory(@"dir");

            builder.DefaultUser = 1000;
            builder.DefaultGroup = 1234;
            builder.DefaultFilePermissions = UnixFilePermissions.OwnerAll;
            builder.DefaultDirectoryPermissions = UnixFilePermissions.GroupAll;

            builder.AddFile("file2", new MemoryStream());
            builder.AddDirectory("dir2");

            builder.Build(fsImage);

            SquashFileSystemReader reader = new SquashFileSystemReader(fsImage);

            Assert.Equal(0, reader.GetUnixFileInfo("file").UserId);
            Assert.Equal(0, reader.GetUnixFileInfo("file").GroupId);
            Assert.Equal(UnixFilePermissions.OwnerRead | UnixFilePermissions.OwnerWrite | UnixFilePermissions.GroupRead | UnixFilePermissions.GroupWrite, reader.GetUnixFileInfo("file").Permissions);

            Assert.Equal(0, reader.GetUnixFileInfo("dir").UserId);
            Assert.Equal(0, reader.GetUnixFileInfo("dir").GroupId);
            Assert.Equal(UnixFilePermissions.OwnerAll | UnixFilePermissions.GroupRead | UnixFilePermissions.GroupExecute | UnixFilePermissions.OthersRead | UnixFilePermissions.OthersExecute, reader.GetUnixFileInfo("dir").Permissions);

            Assert.Equal(1000, reader.GetUnixFileInfo("file2").UserId);
            Assert.Equal(1234, reader.GetUnixFileInfo("file2").GroupId);
            Assert.Equal(UnixFilePermissions.OwnerAll, reader.GetUnixFileInfo("file2").Permissions);

            Assert.Equal(1000, reader.GetUnixFileInfo("dir2").UserId);
            Assert.Equal(1234, reader.GetUnixFileInfo("dir2").GroupId);
            Assert.Equal(UnixFilePermissions.GroupAll, reader.GetUnixFileInfo("dir2").Permissions);
        }

        [Fact]
        public void FragmentData()
        {
            MemoryStream fsImage = new MemoryStream();

            SquashFileSystemBuilder builder = new SquashFileSystemBuilder();
            builder.AddFile(@"file", new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            builder.Build(fsImage);

            SquashFileSystemReader reader = new SquashFileSystemReader(fsImage);

            using (Stream fs = reader.OpenFile("file", FileMode.Open))
            {
                byte[] buffer = new byte[100];
                int numRead = fs.Read(buffer, 0, 100);

                Assert.Equal(4, numRead);
                Assert.Equal(1, buffer[0]);
                Assert.Equal(2, buffer[1]);
                Assert.Equal(3, buffer[2]);
                Assert.Equal(4, buffer[3]);
            }
        }

        [Fact]
        public void BlockData()
        {
            byte[] testData = new byte[(512 * 1024) + 21];
            for (int i = 0; i < testData.Length; ++i)
            {
                testData[i] = (byte)(i % 33);
            }

            MemoryStream fsImage = new MemoryStream();

            SquashFileSystemBuilder builder = new SquashFileSystemBuilder();
            builder.AddFile(@"file", new MemoryStream(testData));
            builder.Build(fsImage);

            SquashFileSystemReader reader = new SquashFileSystemReader(fsImage);

            using (Stream fs = reader.OpenFile("file", FileMode.Open))
            {
                byte[] buffer = new byte[(512 * 1024) + 1024];
                int numRead = fs.Read(buffer, 0, buffer.Length);

                Assert.Equal(testData.Length, numRead);
                for (int i = 0; i < testData.Length; ++i)
                {
                    Assert.Equal(testData[i], buffer[i] /*, "Data differs at index " + i*/);
                }
            }
        }
    }
}

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
using NUnit.Framework;

namespace DiscUtils.SquashFs
{
    [TestFixture]
    internal sealed class SquashFileSystemBuilderTest
    {
        [Test]
        public void SingleFile()
        {
            MemoryStream fsImage = new MemoryStream();

            SquashFileSystemBuilder builder = new SquashFileSystemBuilder();
            builder.AddFile("file", new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            builder.Build(fsImage);

            SquashFileSystemReader reader = new SquashFileSystemReader(fsImage);
            Assert.AreEqual(1, reader.GetFileSystemEntries("\\").Length);
            Assert.AreEqual(4, reader.GetFileLength("file"));
            Assert.IsTrue(reader.FileExists("file"));
            Assert.IsFalse(reader.DirectoryExists("file"));
            Assert.IsFalse(reader.FileExists("otherfile"));
        }

        [Test]
        public void CreateDirs()
        {
            MemoryStream fsImage = new MemoryStream();

            SquashFileSystemBuilder builder = new SquashFileSystemBuilder();
            builder.AddFile(@"\adir\anotherdir\file", new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            builder.Build(fsImage);

            SquashFileSystemReader reader = new SquashFileSystemReader(fsImage);
            Assert.IsTrue(reader.DirectoryExists(@"adir"));
            Assert.IsTrue(reader.DirectoryExists(@"adir\anotherdir"));
            Assert.IsTrue(reader.FileExists(@"adir\anotherdir\file"));
        }

        [Test]
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

            Assert.AreEqual(0, reader.GetUnixFileInfo("file").UserId);
            Assert.AreEqual(0, reader.GetUnixFileInfo("file").GroupId);
            Assert.AreEqual(UnixFilePermissions.OwnerRead | UnixFilePermissions.OwnerWrite | UnixFilePermissions.GroupRead | UnixFilePermissions.GroupWrite, reader.GetUnixFileInfo("file").Permissions);

            Assert.AreEqual(0, reader.GetUnixFileInfo("dir").UserId);
            Assert.AreEqual(0, reader.GetUnixFileInfo("dir").GroupId);
            Assert.AreEqual(UnixFilePermissions.OwnerAll | UnixFilePermissions.GroupRead | UnixFilePermissions.GroupExecute | UnixFilePermissions.OthersRead | UnixFilePermissions.OthersExecute, reader.GetUnixFileInfo("dir").Permissions);

            Assert.AreEqual(1000, reader.GetUnixFileInfo("file2").UserId);
            Assert.AreEqual(1234, reader.GetUnixFileInfo("file2").GroupId);
            Assert.AreEqual(UnixFilePermissions.OwnerAll, reader.GetUnixFileInfo("file2").Permissions);

            Assert.AreEqual(1000, reader.GetUnixFileInfo("dir2").UserId);
            Assert.AreEqual(1234, reader.GetUnixFileInfo("dir2").GroupId);
            Assert.AreEqual(UnixFilePermissions.GroupAll, reader.GetUnixFileInfo("dir2").Permissions);
        }

        [Test]
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

                Assert.AreEqual(4, numRead);
                Assert.AreEqual(1, buffer[0]);
                Assert.AreEqual(2, buffer[1]);
                Assert.AreEqual(3, buffer[2]);
                Assert.AreEqual(4, buffer[3]);
            }
        }

        [Test]
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

                Assert.AreEqual(testData.Length, numRead);
                for (int i = 0; i < testData.Length; ++i)
                {
                    Assert.AreEqual(testData[i], buffer[i], "Data differs at index " + i);
                }
            }
        }
    }
}

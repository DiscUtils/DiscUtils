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
using System.Text;
using NUnit.Framework;

namespace DiscUtils.Fat
{
    [TestFixture]
    public class FatFileSystemTest
    {
        [Test]
        public void FormatFloppy()
        {
            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, "KBFLOPPY   ");
        }

        [Test]
        public void Cyrillic()
        {
            string lowerDE = "\x0434";
            string upperDE = "\x0414";

            MemoryStream ms = new MemoryStream();
            using (FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, "KBFLOPPY   "))
            {
                fs.FatOptions.FileNameEncoding = Encoding.GetEncoding(855);

                string name = lowerDE;
                fs.CreateDirectory(name);

                string[] dirs = fs.GetDirectories("");
                Assert.AreEqual(1, dirs.Length);
                Assert.AreEqual(upperDE, dirs[0]); // Uppercase

                Assert.IsTrue(fs.DirectoryExists(lowerDE));
                Assert.IsTrue(fs.DirectoryExists(upperDE));

                fs.CreateDirectory(lowerDE + lowerDE + lowerDE);
                Assert.AreEqual(2, fs.GetDirectories("").Length);

                fs.DeleteDirectory(lowerDE + lowerDE + lowerDE);
                Assert.AreEqual(1, fs.GetDirectories("").Length);
            }

            DiscFileSystem fs2 = FileSystemManager.DetectDefaultFileSystems(ms)[0].Open(
                ms,
                new FileSystemParameters { FileNameEncoding = Encoding.GetEncoding(855) });

            Assert.IsTrue(fs2.DirectoryExists(lowerDE));
            Assert.IsTrue(fs2.DirectoryExists(upperDE));
            Assert.AreEqual(1, fs2.GetDirectories("").Length);
        }

        [Test]
        public void DefaultCodepage()
        {
            string graphicChar = "\x255D";

            MemoryStream ms = new MemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(ms, FloppyDiskType.HighDensity, "KBFLOPPY   ");
            fs.FatOptions.FileNameEncoding = Encoding.GetEncoding(855);

            string name = graphicChar;
            fs.CreateDirectory(name);

            string[] dirs = fs.GetDirectories("");
            Assert.AreEqual(1, dirs.Length);
            Assert.AreEqual(graphicChar, dirs[0]); // Uppercase

            Assert.IsTrue(fs.DirectoryExists(graphicChar));
        }

        [Test]
        public void FormatPartition()
        {
            MemoryStream ms = new MemoryStream();

            Geometry g = Geometry.FromCapacity(1024 * 1024 * 32);
            FatFileSystem fs = FatFileSystem.FormatPartition(ms, "KBPARTITION", g, 0, (int)g.TotalSectorsLong, 13);

            fs.CreateDirectory(@"DIRB\DIRC");

            FatFileSystem fs2 = new FatFileSystem(ms);
            Assert.AreEqual(1, fs2.Root.GetDirectories().Length);
        }

        [Test]
        public void CreateDirectory()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");

            fs.CreateDirectory(@"UnItTeSt");
            Assert.AreEqual("UNITTEST", fs.Root.GetDirectories("UNITTEST")[0].Name);

            fs.CreateDirectory(@"folder\subflder");
            Assert.AreEqual("FOLDER", fs.Root.GetDirectories("FOLDER")[0].Name);

            fs.CreateDirectory(@"folder\subflder");
            Assert.AreEqual("SUBFLDER", fs.Root.GetDirectories("FOLDER")[0].GetDirectories("SUBFLDER")[0].Name);

        }

        [Test]
        public void CanWrite()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            Assert.AreEqual(true, fs.CanWrite);
        }

        [Test]
        public void Label()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            Assert.AreEqual("FLOPPY_IMG ", fs.VolumeLabel);

            fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, null);
            Assert.AreEqual("NO NAME    ", fs.VolumeLabel);
        }

        [Test]
        public void FileInfo()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\SOMEFILE.TXT");
            Assert.IsNotNull(fi);
        }

        [Test]
        public void DirectoryInfo()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            DiscDirectoryInfo fi = fs.GetDirectoryInfo(@"SOMEDIR");
            Assert.IsNotNull(fi);
        }

        [Test]
        public void FileSystemInfo()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            DiscFileSystemInfo fi = fs.GetFileSystemInfo(@"SOMEDIR\SOMEFILE");
            Assert.IsNotNull(fi);
        }

        [Test]
        public void Root()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            Assert.IsNotNull(fs.Root);
            Assert.IsTrue(fs.Root.Exists);
            Assert.IsEmpty(fs.Root.Name);
            Assert.IsNull(fs.Root.Parent);
        }

        [Test]
        [ExpectedException(typeof(DirectoryNotFoundException))]
        [Category("ThrowsException")]
        public void OpenFileAsDir()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");

            using (Stream s = fs.OpenFile("FOO.TXT", FileMode.Create, FileAccess.ReadWrite))
            {
                StreamWriter w = new StreamWriter(s);
                w.WriteLine("FOO - some sample text");
                w.Flush();
            }

            fs.GetFiles("FOO.TXT");
        }

        [Test]
        public void HonoursReadOnly()
        {
            SparseMemoryStream diskStream = new SparseMemoryStream();
            FatFileSystem fs = FatFileSystem.FormatFloppy(diskStream, FloppyDiskType.HighDensity, "FLOPPY_IMG ");

            fs.CreateDirectory(@"AAA");
            fs.CreateDirectory(@"BAR");
            using (Stream t = fs.OpenFile(@"BAR\AAA.TXT", FileMode.Create, FileAccess.ReadWrite)) { }
            using (Stream s = fs.OpenFile(@"BAR\FOO.TXT", FileMode.Create, FileAccess.ReadWrite))
            {
                StreamWriter w = new StreamWriter(s);
                w.WriteLine("FOO - some sample text");
                w.Flush();
            }
            fs.SetLastAccessTimeUtc(@"BAR", new DateTime(1980, 1, 1));
            fs.SetLastAccessTimeUtc(@"BAR\FOO.TXT", new DateTime(1980, 1, 1));

            // Check we can access a file without any errors
            SparseStream roDiskStream = SparseStream.ReadOnly(diskStream, Ownership.None);
            FatFileSystem fatFs = new FatFileSystem(roDiskStream);
            using (Stream fileStream = fatFs.OpenFile(@"BAR\FOO.TXT", FileMode.Open))
            {
                fileStream.ReadByte();
            }

        }
    }
}

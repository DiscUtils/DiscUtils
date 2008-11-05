//
// Copyright (c) 2008, Kenneth Bell
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
        public void FormatPartition()
        {
            MemoryStream ms = new MemoryStream();

            ushort cylinders;
            byte headsPerCylinder;
            byte sectorsPerTrack;

            CalcDefaultVHDGeometry((1024 * 1024 * 32)/512, out cylinders, out headsPerCylinder, out sectorsPerTrack);

            ulong actualSize = (ulong)cylinders * (ulong)headsPerCylinder * (ulong)sectorsPerTrack * 512;

            FatFileSystem fs = FatFileSystem.FormatPartition(ms, "KBPARTITION", cylinders, headsPerCylinder, sectorsPerTrack, 0, 13);

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
            Assert.AreEqual("FLOPPY_IMG ", fs.VolumeLabel);

            DiscFileInfo fi = fs.GetFileInfo(@"SOMEDIR\SOMEFILE.TXT");
            Assert.IsNotNull(fi);
        }

        [Test]
        public void DirectoryInfo()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            Assert.AreEqual("FLOPPY_IMG ", fs.VolumeLabel);

            DiscDirectoryInfo fi = fs.GetDirectoryInfo(@"SOMEDIR");
            Assert.IsNotNull(fi);
        }

        [Test]
        public void FileSystemInfo()
        {
            FatFileSystem fs = FatFileSystem.FormatFloppy(new MemoryStream(), FloppyDiskType.HighDensity, "FLOPPY_IMG ");
            Assert.AreEqual("FLOPPY_IMG ", fs.VolumeLabel);

            DiscFileSystemInfo fi = fs.GetFileSystemInfo(@"SOMEDIR\SOMEFILE");
            Assert.IsNotNull(fi);
        }

        internal static void CalcDefaultVHDGeometry(uint totalSectors, out ushort cylinders, out byte headsPerCylinder, out byte sectorsPerTrack)
        {
            // If more than ~128GB truncate at ~128GB
            if (totalSectors > 65535 * 16 * 255)
            {
                totalSectors = 65535 * 16 * 255;
            }

            // If more than ~32GB, break partition table compatibility.
            // Partition table has max 63 sectors per track.  Otherwise
            // we're looking for a geometry that's valid for both BIOS
            // and ATA.
            if (totalSectors > 65535 * 16 * 63)
            {
                sectorsPerTrack = 255;
                headsPerCylinder = 16;
            }
            else
            {
                sectorsPerTrack = 17;
                uint cylindersTimesHeads = totalSectors / sectorsPerTrack;
                headsPerCylinder = (byte)((cylindersTimesHeads + 1023) / 1024);

                if (headsPerCylinder < 4)
                {
                    headsPerCylinder = 4;
                }

                // If we need more than 1023 cylinders, or 16 heads, try more sectors per track
                if (cylindersTimesHeads >= (headsPerCylinder * 1024U) || headsPerCylinder > 16)
                {
                    sectorsPerTrack = 31;
                    headsPerCylinder = 16;
                    cylindersTimesHeads = totalSectors / sectorsPerTrack;
                }

                // We need 63 sectors per track to keep the cylinder count down
                if (cylindersTimesHeads >= (headsPerCylinder * 1024U))
                {
                    sectorsPerTrack = 63;
                    headsPerCylinder = 16;
                }

            }
            cylinders = (ushort)((totalSectors / sectorsPerTrack) / headsPerCylinder);
        }
    }
}

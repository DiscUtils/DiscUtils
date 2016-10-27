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
using System.Security.AccessControl;
using NUnit.Framework;
using System.Collections.Generic;

namespace DiscUtils.Ntfs
{
    [TestFixture]
    public class NtfsFileSystemTest
    {
        [Test]
        public void AclInheritance()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            RawSecurityDescriptor sd = new RawSecurityDescriptor("O:BAG:BAD:(A;OICINP;GA;;;BA)");
            ntfs.CreateDirectory("dir");
            ntfs.SetSecurity("dir", sd);

            ntfs.CreateDirectory(@"dir\subdir");
            RawSecurityDescriptor inheritedSd = ntfs.GetSecurity(@"dir\subdir");

            Assert.NotNull(inheritedSd);
            Assert.AreEqual("O:BAG:BAD:(A;ID;GA;;;BA)", inheritedSd.GetSddlForm(AccessControlSections.All));

            using (ntfs.OpenFile(@"dir\subdir\file", FileMode.Create, FileAccess.ReadWrite)) { }
            inheritedSd = ntfs.GetSecurity(@"dir\subdir\file");
            Assert.NotNull(inheritedSd);
            Assert.AreEqual("O:BAG:BAD:", inheritedSd.GetSddlForm(AccessControlSections.All));
        }

        [Test]
        public void ReparsePoints_Empty()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.CreateDirectory("dir");
            ntfs.SetReparsePoint("dir", new ReparsePoint(12345, new byte[0]));

            ReparsePoint rp = ntfs.GetReparsePoint("dir");

            Assert.AreEqual(12345, rp.Tag);
            Assert.IsNotNull(rp.Content);
            Assert.AreEqual(0, rp.Content.Length);
        }

        [Test]
        public void ReparsePoints_NonEmpty()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.CreateDirectory("dir");
            ntfs.SetReparsePoint("dir", new ReparsePoint(123, new byte[] { 4, 5, 6 }));

            ReparsePoint rp = ntfs.GetReparsePoint("dir");

            Assert.AreEqual(123, rp.Tag);
            Assert.IsNotNull(rp.Content);
            Assert.AreEqual(3, rp.Content.Length);
        }

        [Test]
        public void Format_SmallDisk()
        {
            long size = 8 * 1024 * 1024;
            SparseMemoryStream partStream = new SparseMemoryStream();
            //VirtualDisk disk = Vhd.Disk.InitializeDynamic(partStream, Ownership.Dispose, size);
            NtfsFileSystem.Format(partStream, "New Partition", Geometry.FromCapacity(size), 0, size / 512);

            NtfsFileSystem ntfs = new NtfsFileSystem(partStream);
            ntfs.Dump(TextWriter.Null, "");
        }

        [Test]
        public void Format_LargeDisk()
        {
            long size = 1024L * 1024 * 1024L * 1024; // 1 TB
            SparseMemoryStream partStream = new SparseMemoryStream();
            NtfsFileSystem.Format(partStream, "New Partition", Geometry.FromCapacity(size), 0, size / 512);

            NtfsFileSystem ntfs = new NtfsFileSystem(partStream);
            ntfs.Dump(TextWriter.Null, "");
        }

        [Test]
        public void ClusterInfo()
        {
            // 'Big' files have clusters
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();
            using (Stream s = ntfs.OpenFile(@"file", FileMode.Create, FileAccess.ReadWrite))
            {
                s.Write(new byte[(int)ntfs.ClusterSize], 0, (int)ntfs.ClusterSize);
            }

            var ranges = ntfs.PathToClusters("file");
            Assert.AreEqual(1, ranges.Length);
            Assert.AreEqual(1, ranges[0].Count);


            // Short files have no clusters (stored in MFT)
            using (Stream s = ntfs.OpenFile(@"file2", FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }
            ranges = ntfs.PathToClusters("file2");
            Assert.AreEqual(0, ranges.Length);
        }

        [Test]
        public void ExtentInfo()
        {
            using (SparseMemoryStream ms = new SparseMemoryStream())
            {
                Geometry diskGeometry = Geometry.FromCapacity(30 * 1024 * 1024);
                NtfsFileSystem ntfs = NtfsFileSystem.Format(ms, "", diskGeometry, 0, diskGeometry.TotalSectorsLong);

                // Check non-resident attribute
                using (Stream s = ntfs.OpenFile(@"file", FileMode.Create, FileAccess.ReadWrite))
                {
                    byte[] data = new byte[(int)ntfs.ClusterSize];
                    data[0] = 0xAE;
                    data[1] = 0x3F;
                    data[2] = 0x8D;
                    s.Write(data, 0, (int)ntfs.ClusterSize);
                }

                var extents = ntfs.PathToExtents("file");
                Assert.AreEqual(1, extents.Length);
                Assert.AreEqual(ntfs.ClusterSize, extents[0].Length);

                ms.Position = extents[0].Start;
                Assert.AreEqual(0xAE, ms.ReadByte());
                Assert.AreEqual(0x3F, ms.ReadByte());
                Assert.AreEqual(0x8D, ms.ReadByte());


                // Check resident attribute
                using (Stream s = ntfs.OpenFile(@"file2", FileMode.Create, FileAccess.ReadWrite))
                {
                    s.WriteByte(0xBA);
                    s.WriteByte(0x82);
                    s.WriteByte(0x2C);
                }
                extents = ntfs.PathToExtents("file2");
                Assert.AreEqual(1, extents.Length);
                Assert.AreEqual(3, extents[0].Length);

                byte[] read = new byte[100];
                ms.Position = extents[0].Start;
                ms.Read(read, 0, 100);

                Assert.AreEqual(0xBA, read[0]);
                Assert.AreEqual(0x82, read[1]);
                Assert.AreEqual(0x2C, read[2]);
            }
        }

        [Test]
        public void ManyAttributes()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();
            using (Stream s = ntfs.OpenFile(@"file", FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(32);
            }

            for (int i = 0; i < 50; ++i)
            {
                ntfs.CreateHardLink("file", "hl" + i);
            }

            using (Stream s = ntfs.OpenFile("hl35", FileMode.Open, FileAccess.ReadWrite))
            {
                Assert.AreEqual(32, s.ReadByte());
                s.Position = 0;
                s.WriteByte(12);
            }

            using (Stream s = ntfs.OpenFile("hl5", FileMode.Open, FileAccess.ReadWrite))
            {
                Assert.AreEqual(12, s.ReadByte());
            }

            for (int i = 0; i < 50; ++i)
            {
                ntfs.DeleteFile("hl" + i);
            }

            Assert.AreEqual(1, ntfs.GetFiles(@"\").Length);

            ntfs.DeleteFile("file");

            Assert.AreEqual(0, ntfs.GetFiles(@"\").Length);
        }

        [Test]
        public void ShortNames()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            // Check we can find a short name in the same directory
            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) {}
            ntfs.SetShortName("ALongFileName.txt", "ALONG~01.TXT");
            Assert.AreEqual("ALONG~01.TXT", ntfs.GetShortName("ALongFileName.txt"));
            Assert.IsTrue(ntfs.FileExists("ALONG~01.TXT"));

            // Check path handling
            ntfs.CreateDirectory("DIR");
            using (Stream s = ntfs.OpenFile(@"DIR\ALongFileName2.txt", FileMode.CreateNew)) { }
            ntfs.SetShortName(@"DIR\ALongFileName2.txt", "ALONG~02.TXT");
            Assert.AreEqual("ALONG~02.TXT", ntfs.GetShortName(@"DIR\ALongFileName2.txt"));
            Assert.IsTrue(ntfs.FileExists(@"DIR\ALONG~02.TXT"));

            // Check we can open a file by the short name
            using (Stream s = ntfs.OpenFile("ALONG~01.TXT", FileMode.Open)) { }

            // Delete the long name, and make sure the file is gone
            ntfs.DeleteFile("ALONG~01.TXT");
            Assert.IsFalse(ntfs.FileExists("ALONG~01.TXT"));

            // Delete the short name, and make sure the file is gone
            ntfs.DeleteFile(@"DIR\ALONG~02.TXT");
            Assert.IsFalse(ntfs.FileExists(@"DIR\ALongFileName2.txt"));
        }

        [Test]
        public void HardLinkCount()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) { }
            Assert.AreEqual(1, ntfs.GetHardLinkCount("ALongFileName.txt"));

            ntfs.CreateHardLink("ALongFileName.txt", "AHardLink.TXT");
            Assert.AreEqual(2, ntfs.GetHardLinkCount("ALongFileName.txt"));

            ntfs.CreateDirectory("DIR");
            ntfs.CreateHardLink(@"ALongFileName.txt", @"DIR\SHORTLNK.TXT");
            Assert.AreEqual(3, ntfs.GetHardLinkCount("ALongFileName.txt"));

            // If we enumerate short names, then the initial long name results in two 'hardlinks'
            ntfs.NtfsOptions.HideDosFileNames = false;
            Assert.AreEqual(4, ntfs.GetHardLinkCount("ALongFileName.txt"));
        }

        [Test]
        public void HasHardLink()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) { }
            Assert.IsFalse(ntfs.HasHardLinks("ALongFileName.txt"));

            ntfs.CreateHardLink("ALongFileName.txt", "AHardLink.TXT");
            Assert.IsTrue(ntfs.HasHardLinks("ALongFileName.txt"));

            using (Stream s = ntfs.OpenFile("ALongFileName2.txt", FileMode.CreateNew)) { }

            // If we enumerate short names, then the initial long name results in two 'hardlinks'
            ntfs.NtfsOptions.HideDosFileNames = false;
            Assert.IsTrue(ntfs.HasHardLinks("ALongFileName2.txt"));
        }

        [Test]
        public void MoveLongName()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) { }

            Assert.IsTrue(ntfs.FileExists("ALONGF~1.TXT"));

            ntfs.MoveFile("ALongFileName.txt", "ADifferentLongFileName.txt");

            Assert.IsFalse(ntfs.FileExists("ALONGF~1.TXT"));
            Assert.IsTrue(ntfs.FileExists("ADIFFE~1.TXT"));

            ntfs.CreateDirectory("ALongDirectoryName");
            Assert.IsTrue(ntfs.DirectoryExists("ALONGD~1"));

            ntfs.MoveDirectory("ALongDirectoryName", "ADifferentLongDirectoryName");
            Assert.IsFalse(ntfs.DirectoryExists("ALONGD~1"));
            Assert.IsTrue(ntfs.DirectoryExists("ADIFFE~1"));
        }

        [Test]
        public void OpenRawStream()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

#pragma warning disable 618
            Assert.Null(ntfs.OpenRawStream(@"$Extend\$ObjId", AttributeType.Data, null, FileAccess.Read));
#pragma warning restore 618
        }

        [Test]
        public void GetAlternateDataStreams()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.OpenFile("AFILE.TXT", FileMode.Create).Close();
            Assert.AreEqual(0, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);

            ntfs.OpenFile("AFILE.TXT:ALTSTREAM", FileMode.Create).Close();
            Assert.AreEqual(1, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);
            Assert.AreEqual("ALTSTREAM", ntfs.GetAlternateDataStreams("AFILE.TXT")[0]);
        }

        [Test]
        public void DeleteAlternateDataStreams()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.OpenFile("AFILE.TXT", FileMode.Create).Close();
            ntfs.OpenFile("AFILE.TXT:ALTSTREAM", FileMode.Create).Close();
            Assert.AreEqual(1, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);

            ntfs.DeleteFile("AFILE.TXT:ALTSTREAM");
            Assert.AreEqual(1, ntfs.GetFileSystemEntries("").Length);
            Assert.AreEqual(0, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);
        }

        [Test]
        public void DeleteShortNameDir()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.CreateDirectory(@"\TestLongName1\TestLongName2");
            ntfs.SetShortName(@"\TestLongName1\TestLongName2", "TESTLO~1");

            Assert.IsTrue(ntfs.DirectoryExists(@"\TestLongName1\TESTLO~1"));
            Assert.IsTrue(ntfs.DirectoryExists(@"\TestLongName1\TestLongName2"));

            ntfs.DeleteDirectory(@"\TestLongName1", true);

            Assert.IsFalse(ntfs.DirectoryExists(@"\TestLongName1"));
        }

        [Test]
        public void GetFileLength()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.OpenFile(@"AFILE.TXT", FileMode.Create).Close();
            Assert.AreEqual(0, ntfs.GetFileLength("AFILE.TXT"));

            using (var stream = ntfs.OpenFile(@"AFILE.TXT", FileMode.Open))
            {
                stream.Write(new byte[14325], 0, 14325);
            }
            Assert.AreEqual(14325, ntfs.GetFileLength("AFILE.TXT"));

            using (var attrStream = ntfs.OpenFile(@"AFILE.TXT:altstream", FileMode.Create))
            {
                attrStream.Write(new byte[122], 0, 122);
            }
            Assert.AreEqual(122, ntfs.GetFileLength("AFILE.TXT:altstream"));


            // Test NTFS options for hardlink behaviour
            ntfs.CreateDirectory("Dir");
            ntfs.CreateHardLink("AFILE.TXT", @"Dir\OtherLink.txt");

            using (var stream = ntfs.OpenFile("AFILE.TXT", FileMode.Open, FileAccess.ReadWrite))
            {
                stream.SetLength(50);
            }
            Assert.AreEqual(50, ntfs.GetFileLength("AFILE.TXT"));
            Assert.AreEqual(14325, ntfs.GetFileLength(@"Dir\OtherLink.txt"));

            ntfs.NtfsOptions.FileLengthFromDirectoryEntries = false;

            Assert.AreEqual(50, ntfs.GetFileLength(@"Dir\OtherLink.txt"));
        }

        [Test]
        public void Fragmented()
        {
            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            ntfs.CreateDirectory(@"DIR");

            byte[] buffer = new byte[4096];

            for(int i = 0; i < 2500; ++i)
            {
                using(var stream = ntfs.OpenFile(@"DIR\file" + i + ".bin", FileMode.Create, FileAccess.ReadWrite))
                {
                    stream.Write(buffer, 0,buffer.Length);
                }

                using(var stream = ntfs.OpenFile(@"DIR\" + i + ".bin", FileMode.Create, FileAccess.ReadWrite))
                {
                    stream.Write(buffer, 0,buffer.Length);
                }
            }

            for (int i = 0; i < 2500; ++i)
            {
                ntfs.DeleteFile(@"DIR\file" + i + ".bin");
            }

            // Create fragmented file (lots of small writes)
            using (var stream = ntfs.OpenFile(@"DIR\fragmented.bin", FileMode.Create, FileAccess.ReadWrite))
            {
                for (int i = 0; i < 2500; ++i)
                {
                    stream.Write(buffer, 0, buffer.Length);
                }
            }

            // Try a large write
            byte[] largeWriteBuffer = new byte[200 * 1024];
            for (int i = 0; i < largeWriteBuffer.Length / 4096; ++i)
            {
                largeWriteBuffer[i * 4096] = (byte)i;
            }
            using (var stream = ntfs.OpenFile(@"DIR\fragmented.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                stream.Position = stream.Length - largeWriteBuffer.Length;
                stream.Write(largeWriteBuffer, 0, largeWriteBuffer.Length);
            }

            // And a large read
            byte[] largeReadBuffer = new byte[largeWriteBuffer.Length];
            using (var stream = ntfs.OpenFile(@"DIR\fragmented.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                stream.Position = stream.Length - largeReadBuffer.Length;
                stream.Read(largeReadBuffer, 0, largeReadBuffer.Length);
            }

            Assert.AreEqual(largeWriteBuffer, largeReadBuffer);
        }

        [Test]
        public void Sparse()
        {
            int fileSize = 1 * 1024 * 1024;

            NtfsFileSystem ntfs = new FileSystemSource().NtfsFileSystem();

            byte[] data = new byte[fileSize];
            for (int i = 0; i < fileSize; i++)
            {
                data[i] = (byte)i;
            }

            using (SparseStream s = ntfs.OpenFile("file.bin", FileMode.CreateNew))
            {
                s.Write(data, 0, fileSize);

                ntfs.SetAttributes("file.bin", ntfs.GetAttributes("file.bin") | FileAttributes.SparseFile);

                s.Position = 64 * 1024;
                s.Clear(128 * 1024);
                s.Position = fileSize - 64 * 1024;
                s.Clear(128 * 1024);
            }

            using (SparseStream s = ntfs.OpenFile("file.bin", FileMode.Open))
            {
                Assert.AreEqual(fileSize + 64 * 1024, s.Length);

                List<StreamExtent> extents = new List<StreamExtent>(s.Extents);

                Assert.AreEqual(2, extents.Count);
                Assert.AreEqual(0, extents[0].Start);
                Assert.AreEqual(64 * 1024, extents[0].Length);
                Assert.AreEqual((64 + 128) * 1024, extents[1].Start);
                Assert.AreEqual(fileSize - (64 * 1024) - ((64 + 128) * 1024), extents[1].Length);


                s.Position = 72 * 1024;
                s.WriteByte(99);

                byte[] readBuffer = new byte[fileSize];
                s.Position = 0;
                s.Read(readBuffer, 0, fileSize);

                for (int i = 64 * 1024; i < (128 + 64) * 1024; ++i)
                {
                    data[i] = 0;
                }
                for (int i = fileSize - (64 * 1024); i < fileSize; ++i)
                {
                    data[i] = 0;
                }
                data[72 * 1024] = 99;

                Assert.AreEqual(data, readBuffer);
            }
        }
    }
}

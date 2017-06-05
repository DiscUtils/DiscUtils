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

using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests.Ntfs
{
    public class NtfsFileSystemTest
    {
        [Fact(Skip = "Issue #14")]
        public void AclInheritance()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            RawSecurityDescriptor sd = new RawSecurityDescriptor("O:BAG:BAD:(A;OICINP;GA;;;BA)");
            ntfs.CreateDirectory("dir");
            ntfs.SetSecurity("dir", sd);

            ntfs.CreateDirectory(@"dir\subdir");
            RawSecurityDescriptor inheritedSd = ntfs.GetSecurity(@"dir\subdir");

            Assert.NotNull(inheritedSd);
            Assert.Equal("O:BAG:BAD:(A;ID;GA;;;BA)", inheritedSd.GetSddlForm(AccessControlSections.All));

            using (ntfs.OpenFile(@"dir\subdir\file", FileMode.Create, FileAccess.ReadWrite)) { }
            inheritedSd = ntfs.GetSecurity(@"dir\subdir\file");
            Assert.NotNull(inheritedSd);
            Assert.Equal("O:BAG:BAD:", inheritedSd.GetSddlForm(AccessControlSections.All));
        }

        [Fact]
        public void ReparsePoints_Empty()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            ntfs.CreateDirectory("dir");
            ntfs.SetReparsePoint("dir", new ReparsePoint(12345, new byte[0]));

            ReparsePoint rp = ntfs.GetReparsePoint("dir");

            Assert.Equal(12345, rp.Tag);
            Assert.NotNull(rp.Content);
            Assert.Equal(0, rp.Content.Length);
        }

        [Fact]
        public void ReparsePoints_NonEmpty()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            ntfs.CreateDirectory("dir");
            ntfs.SetReparsePoint("dir", new ReparsePoint(123, new byte[] { 4, 5, 6 }));

            ReparsePoint rp = ntfs.GetReparsePoint("dir");

            Assert.Equal(123, rp.Tag);
            Assert.NotNull(rp.Content);
            Assert.Equal(3, rp.Content.Length);
        }

        [Fact(Skip = "Issue #14")]
        public void Format_SmallDisk()
        {
            long size = 8 * 1024 * 1024;
            SparseMemoryStream partStream = new SparseMemoryStream();
            //VirtualDisk disk = Vhd.Disk.InitializeDynamic(partStream, Ownership.Dispose, size);
            NtfsFileSystem.Format(partStream, "New Partition", Geometry.FromCapacity(size), 0, size / 512);

            NtfsFileSystem ntfs = new NtfsFileSystem(partStream);
            ntfs.Dump(TextWriter.Null, "");
        }

        [Fact(Skip = "Issue #14")]
        public void Format_LargeDisk()
        {
            long size = 1024L * 1024 * 1024L * 1024; // 1 TB
            SparseMemoryStream partStream = new SparseMemoryStream();
            NtfsFileSystem.Format(partStream, "New Partition", Geometry.FromCapacity(size), 0, size / 512);

            NtfsFileSystem ntfs = new NtfsFileSystem(partStream);
            ntfs.Dump(TextWriter.Null, "");
        }

        [Fact]
        public void ClusterInfo()
        {
            // 'Big' files have clusters
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();
            using (Stream s = ntfs.OpenFile(@"file", FileMode.Create, FileAccess.ReadWrite))
            {
                s.Write(new byte[(int)ntfs.ClusterSize], 0, (int)ntfs.ClusterSize);
            }

            var ranges = ntfs.PathToClusters("file");
            Assert.Equal(1, ranges.Length);
            Assert.Equal(1, ranges[0].Count);


            // Short files have no clusters (stored in MFT)
            using (Stream s = ntfs.OpenFile(@"file2", FileMode.Create, FileAccess.ReadWrite))
            {
                s.WriteByte(1);
            }
            ranges = ntfs.PathToClusters("file2");
            Assert.Equal(0, ranges.Length);
        }

        [Fact]
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
                Assert.Equal(1, extents.Length);
                Assert.Equal(ntfs.ClusterSize, extents[0].Length);

                ms.Position = extents[0].Start;
                Assert.Equal(0xAE, ms.ReadByte());
                Assert.Equal(0x3F, ms.ReadByte());
                Assert.Equal(0x8D, ms.ReadByte());


                // Check resident attribute
                using (Stream s = ntfs.OpenFile(@"file2", FileMode.Create, FileAccess.ReadWrite))
                {
                    s.WriteByte(0xBA);
                    s.WriteByte(0x82);
                    s.WriteByte(0x2C);
                }
                extents = ntfs.PathToExtents("file2");
                Assert.Equal(1, extents.Length);
                Assert.Equal(3, extents[0].Length);

                byte[] read = new byte[100];
                ms.Position = extents[0].Start;
                ms.Read(read, 0, 100);

                Assert.Equal(0xBA, read[0]);
                Assert.Equal(0x82, read[1]);
                Assert.Equal(0x2C, read[2]);
            }
        }

        [Fact]
        public void ManyAttributes()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();
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
                Assert.Equal(32, s.ReadByte());
                s.Position = 0;
                s.WriteByte(12);
            }

            using (Stream s = ntfs.OpenFile("hl5", FileMode.Open, FileAccess.ReadWrite))
            {
                Assert.Equal(12, s.ReadByte());
            }

            for (int i = 0; i < 50; ++i)
            {
                ntfs.DeleteFile("hl" + i);
            }

            Assert.Equal(1, ntfs.GetFiles(@"\").Length);

            ntfs.DeleteFile("file");

            Assert.Equal(0, ntfs.GetFiles(@"\").Length);
        }

        [Fact]
        public void ShortNames()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            // Check we can find a short name in the same directory
            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) {}
            ntfs.SetShortName("ALongFileName.txt", "ALONG~01.TXT");
            Assert.Equal("ALONG~01.TXT", ntfs.GetShortName("ALongFileName.txt"));
            Assert.True(ntfs.FileExists("ALONG~01.TXT"));

            // Check path handling
            ntfs.CreateDirectory("DIR");
            using (Stream s = ntfs.OpenFile(@"DIR\ALongFileName2.txt", FileMode.CreateNew)) { }
            ntfs.SetShortName(@"DIR\ALongFileName2.txt", "ALONG~02.TXT");
            Assert.Equal("ALONG~02.TXT", ntfs.GetShortName(@"DIR\ALongFileName2.txt"));
            Assert.True(ntfs.FileExists(@"DIR\ALONG~02.TXT"));

            // Check we can open a file by the short name
            using (Stream s = ntfs.OpenFile("ALONG~01.TXT", FileMode.Open)) { }

            // Delete the long name, and make sure the file is gone
            ntfs.DeleteFile("ALONG~01.TXT");
            Assert.False(ntfs.FileExists("ALONG~01.TXT"));

            // Delete the short name, and make sure the file is gone
            ntfs.DeleteFile(@"DIR\ALONG~02.TXT");
            Assert.False(ntfs.FileExists(@"DIR\ALongFileName2.txt"));
        }

        [Fact]
        public void HardLinkCount()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) { }
            Assert.Equal(1, ntfs.GetHardLinkCount("ALongFileName.txt"));

            ntfs.CreateHardLink("ALongFileName.txt", "AHardLink.TXT");
            Assert.Equal(2, ntfs.GetHardLinkCount("ALongFileName.txt"));

            ntfs.CreateDirectory("DIR");
            ntfs.CreateHardLink(@"ALongFileName.txt", @"DIR\SHORTLNK.TXT");
            Assert.Equal(3, ntfs.GetHardLinkCount("ALongFileName.txt"));

            // If we enumerate short names, then the initial long name results in two 'hardlinks'
            ntfs.NtfsOptions.HideDosFileNames = false;
            Assert.Equal(4, ntfs.GetHardLinkCount("ALongFileName.txt"));
        }

        [Fact]
        public void HasHardLink()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) { }
            Assert.False(ntfs.HasHardLinks("ALongFileName.txt"));

            ntfs.CreateHardLink("ALongFileName.txt", "AHardLink.TXT");
            Assert.True(ntfs.HasHardLinks("ALongFileName.txt"));

            using (Stream s = ntfs.OpenFile("ALongFileName2.txt", FileMode.CreateNew)) { }

            // If we enumerate short names, then the initial long name results in two 'hardlinks'
            ntfs.NtfsOptions.HideDosFileNames = false;
            Assert.True(ntfs.HasHardLinks("ALongFileName2.txt"));
        }

        [Fact]
        public void MoveLongName()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            using (Stream s = ntfs.OpenFile("ALongFileName.txt", FileMode.CreateNew)) { }

            Assert.True(ntfs.FileExists("ALONGF~1.TXT"));

            ntfs.MoveFile("ALongFileName.txt", "ADifferentLongFileName.txt");

            Assert.False(ntfs.FileExists("ALONGF~1.TXT"));
            Assert.True(ntfs.FileExists("ADIFFE~1.TXT"));

            ntfs.CreateDirectory("ALongDirectoryName");
            Assert.True(ntfs.DirectoryExists("ALONGD~1"));

            ntfs.MoveDirectory("ALongDirectoryName", "ADifferentLongDirectoryName");
            Assert.False(ntfs.DirectoryExists("ALONGD~1"));
            Assert.True(ntfs.DirectoryExists("ADIFFE~1"));
        }

        [Fact]
        public void OpenRawStream()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

#pragma warning disable 618
            Assert.Null(ntfs.OpenRawStream(@"$Extend\$ObjId", AttributeType.Data, null, FileAccess.Read));
#pragma warning restore 618
        }

        [Fact]
        public void GetAlternateDataStreams()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            ntfs.OpenFile("AFILE.TXT", FileMode.Create).Dispose();
            Assert.Equal(0, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);

            ntfs.OpenFile("AFILE.TXT:ALTSTREAM", FileMode.Create).Dispose();
            Assert.Equal(1, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);
            Assert.Equal("ALTSTREAM", ntfs.GetAlternateDataStreams("AFILE.TXT")[0]);
        }

        [Fact]
        public void DeleteAlternateDataStreams()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            ntfs.OpenFile("AFILE.TXT", FileMode.Create).Dispose();
            ntfs.OpenFile("AFILE.TXT:ALTSTREAM", FileMode.Create).Dispose();
            Assert.Equal(1, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);

            ntfs.DeleteFile("AFILE.TXT:ALTSTREAM");
            Assert.Equal(1, ntfs.GetFileSystemEntries("").Length);
            Assert.Equal(0, ntfs.GetAlternateDataStreams("AFILE.TXT").Length);
        }

        [Fact]
        public void DeleteShortNameDir()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            ntfs.CreateDirectory(@"\TestLongName1\TestLongName2");
            ntfs.SetShortName(@"\TestLongName1\TestLongName2", "TESTLO~1");

            Assert.True(ntfs.DirectoryExists(@"\TestLongName1\TESTLO~1"));
            Assert.True(ntfs.DirectoryExists(@"\TestLongName1\TestLongName2"));

            ntfs.DeleteDirectory(@"\TestLongName1", true);

            Assert.False(ntfs.DirectoryExists(@"\TestLongName1"));
        }

        [Fact]
        public void GetFileLength()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

            ntfs.OpenFile(@"AFILE.TXT", FileMode.Create).Dispose();
            Assert.Equal(0, ntfs.GetFileLength("AFILE.TXT"));

            using (var stream = ntfs.OpenFile(@"AFILE.TXT", FileMode.Open))
            {
                stream.Write(new byte[14325], 0, 14325);
            }
            Assert.Equal(14325, ntfs.GetFileLength("AFILE.TXT"));

            using (var attrStream = ntfs.OpenFile(@"AFILE.TXT:altstream", FileMode.Create))
            {
                attrStream.Write(new byte[122], 0, 122);
            }
            Assert.Equal(122, ntfs.GetFileLength("AFILE.TXT:altstream"));


            // Test NTFS options for hardlink behaviour
            ntfs.CreateDirectory("Dir");
            ntfs.CreateHardLink("AFILE.TXT", @"Dir\OtherLink.txt");

            using (var stream = ntfs.OpenFile("AFILE.TXT", FileMode.Open, FileAccess.ReadWrite))
            {
                stream.SetLength(50);
            }
            Assert.Equal(50, ntfs.GetFileLength("AFILE.TXT"));
            Assert.Equal(14325, ntfs.GetFileLength(@"Dir\OtherLink.txt"));

            ntfs.NtfsOptions.FileLengthFromDirectoryEntries = false;

            Assert.Equal(50, ntfs.GetFileLength(@"Dir\OtherLink.txt"));
        }

        [Fact]
        public void Fragmented()
        {
            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

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

            Assert.Equal(largeWriteBuffer, largeReadBuffer);
        }

        [Fact]
        public void Sparse()
        {
            int fileSize = 1 * 1024 * 1024;

            NtfsFileSystem ntfs = FileSystemSource.NtfsFileSystem();

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
                Assert.Equal(fileSize + 64 * 1024, s.Length);

                List<StreamExtent> extents = new List<StreamExtent>(s.Extents);

                Assert.Equal(2, extents.Count);
                Assert.Equal(0, extents[0].Start);
                Assert.Equal(64 * 1024, extents[0].Length);
                Assert.Equal((64 + 128) * 1024, extents[1].Start);
                Assert.Equal(fileSize - (64 * 1024) - ((64 + 128) * 1024), extents[1].Length);


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

                Assert.Equal(data, readBuffer);
            }
        }
    }
}

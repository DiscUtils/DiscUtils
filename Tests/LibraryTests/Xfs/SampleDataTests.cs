using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using DiscUtils;
using DiscUtils.Complete;
using DiscUtils.Streams;
using DiscUtils.Xfs;
using DiscUtils.Vhdx;
using LibraryTests.Utilities;
using Xunit;
using File=System.IO.File;

namespace LibraryTests.Xfs
{
    public class SampleDataTests
    {
        [Fact]
        public void XfsVhdxZip()
        {
            SetupHelper.SetupComplete();
            using (FileStream fs = File.OpenRead(Path.Combine("..", "..", "..", "Xfs", "Data", "xfs.zip")))
            using (Stream vhdx = ZipUtilities.ReadFileFromZip(fs))
            using (var diskImage = new DiskImageFile(vhdx, Ownership.Dispose))
            using (var disk = new Disk(new List<DiskImageFile> { diskImage }, Ownership.Dispose))
            {
                var manager = new VolumeManager(disk);
                var logicalVolumes = manager.GetLogicalVolumes();
                Assert.Equal(1, logicalVolumes.Length);

                var volume = logicalVolumes[0];
                var filesystems = FileSystemManager.DetectFileSystems(volume);
                Assert.Equal(1, filesystems.Length);

                var filesystem = filesystems[0];
                Assert.Equal("xfs", filesystem.Name);

                using (var xfs = filesystem.Open(volume))
                {
                    Assert.IsType<XfsFileSystem>(xfs);

                    Assert.Equal(9081139200, xfs.AvailableSpace);
                    Assert.Equal(10725863424, xfs.Size);
                    Assert.Equal(1644724224, xfs.UsedSpace);
                    ValidateContent(xfs);
                }
            }
        }

        [Fact]
        public void Xfs5VhdxZip()
        {
            SetupHelper.SetupComplete();
            using (FileStream fs = File.OpenRead(Path.Combine("..", "..", "..", "Xfs", "Data", "xfs5.zip")))
            using (Stream vhdx = ZipUtilities.ReadFileFromZip(fs))
            using (var diskImage = new DiskImageFile(vhdx, Ownership.Dispose))
            using (var disk = new Disk(new List<DiskImageFile> { diskImage }, Ownership.Dispose))
            {
                var manager = new VolumeManager(disk);
                var logicalVolumes = manager.GetLogicalVolumes();
                Assert.Equal(1, logicalVolumes.Length);

                var volume = logicalVolumes[0];
                var filesystems = FileSystemManager.DetectFileSystems(volume);
                Assert.Equal(1, filesystems.Length);

                var filesystem = filesystems[0];
                Assert.Equal("xfs", filesystem.Name);

                using (var xfs = filesystem.Open(volume))
                {
                    Assert.IsType<XfsFileSystem>(xfs);

                    Assert.Equal(9080827904, xfs.AvailableSpace);
                    Assert.Equal(10725883904, xfs.Size);
                    Assert.Equal(1645056000, xfs.UsedSpace);
                    ValidateContent(xfs);
                }
            }
        }

        private void ValidateContent(DiscFileSystem xfs)
        {
            Assert.True(xfs.DirectoryExists(""));
            Assert.True(xfs.FileExists("folder\\nested\\file"));
            Assert.Equal(0, xfs.GetFileSystemEntries("empty").Length);
            for (int i = 1; i <= 1000; i++)
            {
                Assert.True(xfs.FileExists($"folder\\file.{i}"), $"File file.{i} not found");
            }

            using (var file = xfs.OpenFile("folder\\file.100", FileMode.Open))
            {
                var md5 = MD5.Create().ComputeHash(file);
                Assert.Equal("620f0b67a91f7f74151bc5be745b7110", BitConverter.ToString(md5).ToLowerInvariant().Replace("-", string.Empty));
            }

            using (var file = xfs.OpenFile("folder\\file.random", FileMode.Open))
            {
                var md5 = MD5.Create().ComputeHash(file);
                Assert.Equal("9a202a11d6e87688591eb97714ed56f1", BitConverter.ToString(md5).ToLowerInvariant().Replace("-", string.Empty));
            }

            for (int i = 1; i <= 999; i++)
            {
                Assert.True(xfs.FileExists($"huge\\{i}"), $"File huge/{i} not found");
            }
        }
    }
}
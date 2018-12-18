using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using DiscUtils;
using DiscUtils.Btrfs;
using DiscUtils.Streams;
using DiscUtils.Vhdx;
using LibraryTests.Utilities;
using Xunit;

namespace LibraryTests.Btrfs
{
    public class SampleDataTests
    {
        [Fact]
        public void BtrfsVhdxZip()
        {
            DiscUtils.Setup.SetupHelper.RegisterAssembly(typeof(Disk).GetTypeInfo().Assembly);
            DiscUtils.Setup.SetupHelper.RegisterAssembly(typeof(BtrfsFileSystem).GetTypeInfo().Assembly);
            using (FileStream fs = File.OpenRead(Path.Combine("..", "..", "..", "Btrfs", "Data", "btrfs.zip")))
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
                Assert.Equal("btrfs", filesystem.Name);

                using (var btrfs = filesystem.Open(volume))
                {
                    Assert.IsType<BtrfsFileSystem>(btrfs);

                    Assert.Equal(1072594944, btrfs.AvailableSpace);
                    Assert.Equal(1072693248, btrfs.Size);
                    Assert.Equal(98304, btrfs.UsedSpace);

                    var subvolumes = ((BtrfsFileSystem)btrfs).GetSubvolumes();
                    Assert.Equal(1, subvolumes.Length);
                    Assert.Equal(256UL, subvolumes[0].Id);
                    Assert.Equal("subvolume", subvolumes[0].Name);

                    Assert.Equal("text\n", GetFileContent(@"\folder\subfolder\file", btrfs));
                    Assert.Equal("f64464c2024778f347277de6fa26fe87", GetFileChecksum(@"\folder\subfolder\f64464c2024778f347277de6fa26fe87", btrfs));
                    Assert.Equal("fa121c8b73cf3b01a4840b1041b35e9f", GetFileChecksum(@"\folder\subfolder\fa121c8b73cf3b01a4840b1041b35e9f", btrfs));
                    IsAllZero(@"folder\subfolder\sparse", btrfs);
                    Assert.Equal("test\n", GetFileContent(@"\subvolume\subvolumefolder\subvolumefile", btrfs));
                    Assert.Equal("b0d5fae237588b6641f974459404d197", GetFileChecksum(@"\folder\subfolder\compressed", btrfs));
                    Assert.Equal("test\n", GetFileContent(@"\folder\symlink", btrfs)); //PR#36
                    Assert.Equal("b0d5fae237588b6641f974459404d197", GetFileChecksum(@"\folder\subfolder\lzo", btrfs));
                }

                using (var subvolume = new BtrfsFileSystem(volume.Open(), new BtrfsFileSystemOptions { SubvolumeId = 256, VerifyChecksums = true}))
                {
                    Assert.Equal("test\n", GetFileContent(@"\subvolumefolder\subvolumefile", subvolume));
                }
            }
        }

        private static void IsAllZero(string path, IFileSystem fs)
        {
            var fileInfo = fs.GetFileInfo(path);
            byte[] buffer = new byte[4*Sizes.OneKiB];
            using (var file = fileInfo.OpenRead())
            {
                var count = file.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(0, buffer[i]);
                }
            }
        }

        private static string GetFileContent(string path, IFileSystem fs)
        {
            var fileInfo = fs.GetFileInfo(path);
            using (var file = fileInfo.OpenText())
            {
                return file.ReadToEnd();
            }
        }

        private static string GetFileChecksum(string path, IFileSystem fs)
        {
            var fileInfo = fs.GetFileInfo(path);
            var md5 = MD5.Create();
            using (var file = fileInfo.OpenRead())
            {
                var checksum = md5.ComputeHash(file);
                return BitConverter.ToString(checksum).Replace("-", String.Empty).ToLower();
            }
        }
    }
}
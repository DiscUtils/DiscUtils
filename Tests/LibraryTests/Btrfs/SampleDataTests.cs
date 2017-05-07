using System.Collections.Generic;
using System.IO;
using DiscUtils;
using DiscUtils.Btrfs;
using DiscUtils.Complete;
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
            SetupHelper.SetupComplete();
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

                var btrfs = filesystem.Open(volume);
                Assert.IsType<BtrfsFileSystem>(btrfs);

                Assert.Equal(10736152576, btrfs.AvailableSpace);
                Assert.Equal(10736349184, btrfs.Size);
                Assert.Equal(196608, btrfs.UsedSpace);
            }
        }
    }
}
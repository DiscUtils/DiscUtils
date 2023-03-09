using DiscUtils;
using DiscUtils.Complete;
using DiscUtils.Streams;
using DiscUtils.Swap;
using DiscUtils.Vhdx;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace LibraryTests.Swap
{
    public class SampleDataTests
    {
        [Fact]
        public void SwapVhdxGzip()
        {
            SetupHelper.SetupComplete();

            using (var vhdx = Helpers.Helpers.LoadDataFileFromGZipFile(Path.Combine("..", "..", "..", "Swap", "Data", "swap.vhdx.gz")))
            using (var diskImage = new DiskImageFile(vhdx, Ownership.Dispose))
            using (var disk = new Disk(new List<DiskImageFile> { diskImage }, Ownership.Dispose))
            {
                var manager = new VolumeManager(disk);
                var logicalVolumes = manager.GetLogicalVolumes();
                Assert.Single(logicalVolumes);

                var volume = logicalVolumes[0];
                var filesystems = FileSystemManager.DetectFileSystems(volume);
                Assert.Single(filesystems);

                var filesystem = filesystems[0];
                Assert.Equal("Swap", filesystem.Name);

                var swap = filesystem.Open(volume);
                Assert.IsType<SwapFileSystem>(swap);

                Assert.Equal(0, swap.AvailableSpace);
                Assert.Equal(10737414144, swap.Size);
                Assert.Equal(swap.Size, swap.UsedSpace);
            }
        }
    }
}
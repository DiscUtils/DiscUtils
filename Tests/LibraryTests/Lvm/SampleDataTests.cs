using System.Collections.Generic;
using System.IO;
using DiscUtils;
using DiscUtils.Complete;
using DiscUtils.Streams;
using DiscUtils.Vhdx;
using LibraryTests.Utilities;
using Xunit;

namespace LibraryTests.Lvm
{
    public class SampleDataTests
    {
        [Fact]
        public void Lvm2VhdxZip()
        {
            SetupHelper.SetupComplete();
            using (FileStream fs = File.OpenRead(Path.Combine("..", "..", "..", "Lvm", "Data", "lvm2.zip")))
            using (Stream vhdx = ZipUtilities.ReadFileFromZip(fs))
            using (var diskImage = new DiskImageFile(vhdx, Ownership.Dispose))
            using (var disk = new Disk(new List<DiskImageFile> { diskImage }, Ownership.Dispose))
            {
                var manager = new VolumeManager(disk);
                var logicalVolumes = manager.GetLogicalVolumes();
                Assert.Equal(3, logicalVolumes.Length);

                Assert.Equal(1283457024, logicalVolumes[0].Length);
                Assert.Equal(746586112, logicalVolumes[1].Length);
                Assert.Equal(1178599424, logicalVolumes[2].Length);
            }
        }
    }
}
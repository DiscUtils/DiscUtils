using DiscUtils;
using DiscUtils.Iso9660;
using System.IO;
using Xunit;

namespace LibraryTests.Iso9660
{
    public class SampleDataTests
    {
        [Fact]
        public void BrokenAppleIsoTest()
        {
            using (var iso = Helpers.Helpers.LoadDataFileFromGZipFile(Path.Combine("..", "..", "..", "Iso9660", "Data", "apple-test.iso.gz")))
            using (var cr = new CDReader(iso, false))
            {
                DiscDirectoryInfo dir = cr.GetDirectoryInfo("sub-directory");
                Assert.NotNull(dir);
                Assert.Equal("sub-directory", dir.Name);

                DiscFileInfo[] file = dir.GetFiles("apple-test.txt");
                Assert.Single(file);
                Assert.Equal(21, file[0].Length);
                Assert.Equal("apple-test.txt", file[0].Name);
                Assert.Equal(dir, file[0].Directory);
            }
        }
    }
}
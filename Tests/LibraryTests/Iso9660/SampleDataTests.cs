using System.IO;
using DiscUtils;
using DiscUtils.Iso9660;
using LibraryTests.Utilities;
using Xunit;

namespace LibraryTests.Iso9660
{
    public class SampleDataTests
    {
        [Fact]
        public void AppleTestZip()
        {
            using (FileStream fs = File.OpenRead(Path.Combine("..", "..", "..", "Iso9660", "Data", "apple-test.zip")))
            using (Stream iso = ZipUtilities.ReadFileFromZip(fs))
            using (CDReader cr = new CDReader(iso, false))
            {
                DiscDirectoryInfo dir = cr.GetDirectoryInfo("sub-directory");
                Assert.NotNull(dir);
                Assert.Equal("sub-directory", dir.Name);

                DiscFileInfo[] file = dir.GetFiles("apple-test.txt");
                Assert.Equal(1, file.Length);
                Assert.Equal(21, file[0].Length);
                Assert.Equal("apple-test.txt", file[0].Name);
                Assert.Equal(dir, file[0].Directory);
            }
        }
    }
}
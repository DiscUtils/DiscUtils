using System.IO;
using DiscUtils;
using DiscUtils.Iso9660;
using LibraryTests.Utilities;
using NUnit.Framework;

namespace LibraryTests.Iso9660
{
    [TestFixture]
    public class SampleDataTests
    {
        [Test]
        public void AppleTestZip()
        {
            using (FileStream fs = File.OpenRead(Path.Combine("..", "..", "..", "Iso9660", "Data", "apple-test.zip")))
            using (Stream iso = ZipUtilities.ReadFileFromZip(fs))
            using (CDReader cr = new CDReader(iso, false))
            {
                DiscDirectoryInfo dir = cr.GetDirectoryInfo("sub-directory");
                Assert.NotNull(dir);
                Assert.AreEqual("sub-directory", dir.Name);

                DiscFileInfo[] file = dir.GetFiles("apple-test.txt");
                Assert.AreEqual(1, file.Length);
                Assert.AreEqual(21, file[0].Length);
                Assert.AreEqual("apple-test.txt", file[0].Name);
                Assert.AreEqual(dir, file[0].Directory);
            }
        }
    }
}
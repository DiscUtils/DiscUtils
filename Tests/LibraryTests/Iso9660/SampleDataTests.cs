using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Iso9660;
using LibraryTests.Extensions;
using LibraryTests.Utilities;
using NUnit.Framework;

namespace LibraryTests.Iso9660
{
    [TestFixture]
    public class SampleDataTests
    {
        private string testDirectory;

        [OneTimeSetUp]
        public void Setup()
        {
            testDirectory = TestContext.CurrentContext.TestDirectory;
        }

        // Verifies that iso-9660 files with certain non-complient susp data can be opened and read.
        // See Data/Readme.md for more information.
        [Test]
        public void AppleNonComplientSusp()
        {
            var isoFilePath = FindTestIsoFile(testDirectory);

            Assert.IsNotNull(isoFilePath, "Test .iso file could not be found");

            using (FileStream fs = File.OpenRead(isoFilePath))
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

        // This is a hack to help locate the test .iso file for use by the test code.
        // This should not be necessary. The test .iso file should be copied to the output
        // folder by the build tools and easily located with a static relative path.
        // Currently, dotnet-build is being used on AppVeyor, which does not copy the file.
        // If that can be resolved, then thos silliness can be removed.
        private static string FindTestIsoFile(string path)
        {
            while (path != null)
            {
                FileInfo isoFileInfo = new FileInfo(
                    Path.Combine(
                        path
                            .AsSingle()
                            .Concat("Iso9660", "Data", "apple-test.zip")
                            .ToArray()
                    )
                );

                if (isoFileInfo.Exists)
                {
                    return isoFileInfo.FullName;
                }

                path = Path.GetDirectoryName(path);
            }

            return null;
        }
    }
}
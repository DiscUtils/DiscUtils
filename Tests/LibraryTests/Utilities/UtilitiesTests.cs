using System;
using Xunit;

namespace LibraryTests.Utilities
{
    public class UtilitiesTests
    {
        [Fact]
        public void CanResolveRelativePath()
        {
            CheckResolvePath(@"\etc\rc.d", @"init.d", @"\etc\init.d");
            CheckResolvePath(@"\etc\rc.d\", @"init.d", @"\etc\rc.d\init.d");
            // For example: (\TEMP\Foo.txt, ..\..\Bar.txt) gives (\Bar.txt).
            CheckResolvePath(@"\TEMP\Foo.txt", @"..\..\Bar.txt", @"\Bar.txt");
        }

        private void CheckResolvePath(string basePath, string relativePath, string expectedResult)
        {
            var result = DiscUtils.Internal.Utilities.ResolvePath(basePath, relativePath);
            Assert.Equal(expectedResult, result);
        }
    }
}

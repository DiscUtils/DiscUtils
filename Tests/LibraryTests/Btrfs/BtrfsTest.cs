using System;
using System.IO;
using System.Linq;
using DiscUtils.Btrfs;
using Xunit;

namespace LibraryTests.Btrfs
{
    public class BtrfsTest
    {
        [Fact]
        public void IgnoreInvalidLabelData()
        {
            using (var ms = new MemoryStream())
            {
                ms.Position = 0x20000; //set fs length
                ms.WriteByte(1);
                ms.Position = 0x10000L + 0x12b; //Label offset
                ms.Write(Enumerable.Repeat((byte)1, 0x100).ToArray(), 0, 0x100);//create label without null terminator
                ms.Seek(0, SeekOrigin.Begin);
                var ex = Assert.Throws<IOException>(() => new BtrfsFileSystem(ms));
                Assert.Equal("Invalid Superblock Magic", ex.Message);
            }
        }

        [Fact]
        public void EmptyStreamIsNoValidBtrfs()
        {
            using (var ms = new MemoryStream())
            {
                var ex = Assert.Throws<IOException>(() => new BtrfsFileSystem(ms));
                Assert.Equal("No Superblock detected", ex.Message);
            }
        }
    }
}

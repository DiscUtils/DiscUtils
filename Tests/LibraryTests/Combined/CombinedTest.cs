//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System.IO;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using DiscUtils.Vhd;
using Xunit;

namespace LibraryTests.Combined
{
    public class CombinedTest
    {
        [Fact]
        public void SimpleVhdFat()
        {
            using (Disk disk = Disk.InitializeDynamic(new MemoryStream(), Ownership.Dispose, 16 * 1024 * 1024))
            {
                BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsFat);
                using (FatFileSystem fs = FatFileSystem.FormatPartition(disk, 0, null))
                {
                    fs.CreateDirectory("Foo");
                }
            }
        }

        [Fact]
        public void FormatSecondFatPartition()
        {
            MemoryStream ms = new MemoryStream();

            VirtualDisk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 30 * 1024 * 1204);

            PartitionTable pt = BiosPartitionTable.Initialize(disk);
            pt.Create(15 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);
            pt.Create(5 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);

            FatFileSystem fileSystem = FatFileSystem.FormatPartition(disk, 1, null);
            long fileSystemSize = fileSystem.TotalSectors * fileSystem.BytesPerSector;
            Assert.True(fileSystemSize > (5 * 1024 * 1024) * 0.9);
        }

    }
}

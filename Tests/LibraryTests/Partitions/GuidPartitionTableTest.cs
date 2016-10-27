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
using NUnit.Framework;
using DiscUtils.Vdi;

namespace DiscUtils.Partitions
{
    [TestFixture]
    public class GuidPartitionTableTest
    {
        [Test]
        public void Initialize()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 3 * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                Assert.AreEqual(0, table.Count);
            }
        }

        [Test]
        public void CreateSmallWholeDisk()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 3 * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                int idx = table.Create(WellKnownPartitionType.WindowsFat, true);

                // Make sure the partition fills from first to last usable.
                Assert.AreEqual(table.FirstUsableSector, table[idx].FirstSector);
                Assert.AreEqual(table.LastUsableSector, table[idx].LastSector);
            }
        }

        [Test]
        public void CreateMediumWholeDisk()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 2 * 1024L * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                int idx = table.Create(WellKnownPartitionType.WindowsFat, true);

                Assert.AreEqual(2, table.Partitions.Count);
                Assert.AreEqual(GuidPartitionTypes.MicrosoftReserved, table[0].GuidType);
                Assert.AreEqual(32 * 1024 * 1024, table[0].SectorCount * 512);

                // Make sure the partition fills from first to last usable, allowing for MicrosoftReserved sector.
                Assert.AreEqual(table[0].LastSector + 1, table[idx].FirstSector);
                Assert.AreEqual(table.LastUsableSector, table[idx].LastSector);
            }
        }

        [Test]
        public void CreateLargeWholeDisk()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 200 * 1024L * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                int idx = table.Create(WellKnownPartitionType.WindowsFat, true);

                Assert.AreEqual(2, table.Partitions.Count);
                Assert.AreEqual(GuidPartitionTypes.MicrosoftReserved, table[0].GuidType);
                Assert.AreEqual(128 * 1024 * 1024, table[0].SectorCount * 512);

                // Make sure the partition fills from first to last usable, allowing for MicrosoftReserved sector.
                Assert.AreEqual(table[0].LastSector + 1, table[idx].FirstSector);
                Assert.AreEqual(table.LastUsableSector, table[idx].LastSector);
            }
        }

        [Test]
        public void CreateAlignedWholeDisk()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 200 * 1024L * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                int idx = table.CreateAligned(WellKnownPartitionType.WindowsFat, true, 1024 * 1024);

                Assert.AreEqual(2, table.Partitions.Count);
                Assert.AreEqual(GuidPartitionTypes.MicrosoftReserved, table[0].GuidType);
                Assert.AreEqual(128 * 1024 * 1024, table[0].SectorCount * 512);

                // Make sure the partition is aligned
                Assert.AreEqual(0, table[idx].FirstSector % 2048);
                Assert.AreEqual(0, (table[idx].LastSector + 1) % 2048);

                // Ensure partition fills most of the disk
                Assert.Greater((table[idx].SectorCount * 512), disk.Capacity * 0.9);
            }
        }

        [Test]
        public void CreateBySize()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 3 * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                int idx = table.Create(2 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);

                // Make sure the partition is within 10% of the size requested.
                Assert.That((2 * 1024 * 2) * 0.9 < table[idx].SectorCount);

                Assert.AreEqual(table.FirstUsableSector, table[idx].FirstSector);
            }
        }

        [Test]
        public void CreateBySizeInGap()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 300 * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                table.Create(10 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);

                table.Create((20 * 1024 * 1024) / 512, ((30 * 1024 * 1024) / 512) - 1, GuidPartitionTypes.WindowsBasicData, 0, "Data Partition");

                table.Create((60 * 1024 * 1024) / 512, ((70 * 1024 * 1024) / 512) - 1, GuidPartitionTypes.WindowsBasicData, 0, "Data Partition");

                int idx = table.Create(20 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);
                Assert.AreEqual(((30 * 1024 * 1024) / 512), table[idx].FirstSector);
                Assert.AreEqual(((50 * 1024 * 1024) / 512) - 1, table[idx].LastSector);
            }
        }

        [Test]
        public void CreateBySizeInGapAligned()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 300 * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                table.Create(10 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);

                // Note: end is unaligned
                table.Create((20 * 1024 * 1024) / 512, ((30 * 1024 * 1024) / 512) - 5, GuidPartitionTypes.WindowsBasicData, 0, "Data Partition");

                table.Create((60 * 1024 * 1024) / 512, ((70 * 1024 * 1024) / 512) - 1, GuidPartitionTypes.WindowsBasicData, 0, "Data Partition");

                int idx = table.CreateAligned(20 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false, 64 * 1024);
                Assert.AreEqual(((30 * 1024 * 1024) / 512), table[idx].FirstSector);
                Assert.AreEqual(((50 * 1024 * 1024) / 512) - 1, table[idx].LastSector);
            }
        }

        [Test]
        public void Delete()
        {
            MemoryStream ms = new MemoryStream();
            using (Disk disk = Disk.InitializeDynamic(ms, Ownership.Dispose, 10 * 1024 * 1024))
            {
                GuidPartitionTable table = GuidPartitionTable.Initialize(disk);

                Assert.AreEqual(0, table.Create(1 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false));
                Assert.AreEqual(1, table.Create(2 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false));
                Assert.AreEqual(2, table.Create(3 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false));

                long[] sectorCount = new long[] { table[0].SectorCount, table[1].SectorCount, table[2].SectorCount };

                table.Delete(1);

                Assert.AreEqual(2, table.Count);
                Assert.AreEqual(sectorCount[2], table[1].SectorCount);
            }
        }

    }
}

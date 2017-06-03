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

using DiscUtils;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests.Partitions
{
    public class BiosPartitionTableTest
    {
        [Fact]
        public void Initialize()
        {
            long capacity = 3 * 1024 * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.FromCapacity(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.Equal(0, table.Count);
        }

        [Fact]
        public void CreateWholeDisk()
        {
            long capacity = 3 * 1024 * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.FromCapacity(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            int idx = table.Create(WellKnownPartitionType.WindowsFat, true);

            // Make sure the partition fills all but the first track on the disk.
            Assert.Equal(geom.TotalSectorsLong, table[idx].SectorCount + geom.SectorsPerTrack);

            // Make sure FAT16 was selected for a disk of this size
            Assert.Equal(BiosPartitionTypes.Fat16, table[idx].BiosType);

            // Make sure partition starts where expected
            Assert.Equal(new ChsAddress(0, 1, 1), ((BiosPartitionInfo)table[idx]).Start);

            // Make sure partition ends at end of disk
            Assert.Equal(geom.ToLogicalBlockAddress(geom.LastSector), table[idx].LastSector);
            Assert.Equal(geom.LastSector, ((BiosPartitionInfo)table[idx]).End);

            // Make sure the 'active' flag made it through...
            Assert.True(((BiosPartitionInfo)table[idx]).IsActive);

            // Make sure the partition index is Zero
            Assert.Equal(0, ((BiosPartitionInfo)table[idx]).PrimaryIndex);
        }

        [Fact]
        public void CreateWholeDiskAligned()
        {
            long capacity = 3 * 1024 * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.FromCapacity(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            int idx = table.CreateAligned(WellKnownPartitionType.WindowsFat, true, 64 * 1024);

            Assert.Equal(0, table[idx].FirstSector % 128);
            Assert.Equal(0, (table[idx].LastSector + 1) % 128);
            Assert.True(table[idx].SectorCount * 512 > capacity * 0.9);

            // Make sure the partition index is Zero
            Assert.Equal(0, ((BiosPartitionInfo)table[idx]).PrimaryIndex);
        }

        [Fact]
        public void CreateBySize()
        {
            long capacity = 3 * 1024 * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.FromCapacity(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            int idx = table.Create(2 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);

            // Make sure the partition is within 10% of the size requested.
            Assert.True((2 * 1024 * 2) * 0.9 < table[idx].SectorCount);

            Assert.Equal(geom.ToLogicalBlockAddress(new ChsAddress(0, 1, 1)), table[idx].FirstSector);
            Assert.Equal(geom.HeadsPerCylinder - 1, geom.ToChsAddress((int)table[idx].LastSector).Head);
            Assert.Equal(geom.SectorsPerTrack, geom.ToChsAddress((int)table[idx].LastSector).Sector);
        }

        [Fact]
        public void CreateBySizeInGap()
        {
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry geom = new Geometry(15, 30, 63);
            ms.SetLength(geom.Capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.Equal(0, table.CreatePrimaryByCylinder(0, 4, 33, false));
            Assert.Equal(1, table.CreatePrimaryByCylinder(10, 14, 33, false));
            table.Create(geom.ToLogicalBlockAddress(new ChsAddress(4, 0, 1)) * 512, WellKnownPartitionType.WindowsFat, true);
        }

        [Fact]
        public void CreateBySizeInGapAligned()
        {
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry geom = new Geometry(15, 30, 63);
            ms.SetLength(geom.Capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.Equal(0, table.CreatePrimaryByCylinder(0, 4, 33, false));
            Assert.Equal(1, table.CreatePrimaryByCylinder(10, 14, 33, false));

            int idx = table.CreateAligned(3 * 1024 * 1024, WellKnownPartitionType.WindowsFat, true, 64 * 1024);
            Assert.Equal(2, idx);

            Assert.Equal(0, table[idx].FirstSector % 128);
            Assert.Equal(0, (table[idx].LastSector + 1) % 128);
        }

        [Fact]
        public void CreateByCylinder()
        {
            SparseMemoryStream ms = new SparseMemoryStream();
            Geometry geom = new Geometry(15, 30, 63);
            ms.SetLength(geom.Capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.Equal(0, table.CreatePrimaryByCylinder(0, 4, 33, false));
            Assert.Equal(1, table.CreatePrimaryByCylinder(10, 14, 33, false));

            Assert.Equal(geom.ToLogicalBlockAddress(new ChsAddress(0, 1, 1)), table[0].FirstSector);
            Assert.Equal(geom.ToLogicalBlockAddress(new ChsAddress(5, 0, 1)) - 1, table[0].LastSector);
            Assert.Equal(geom.ToLogicalBlockAddress(new ChsAddress(10, 0, 1)), table[1].FirstSector);
            Assert.Equal(geom.ToLogicalBlockAddress(new ChsAddress(14, 29, 63)), table[1].LastSector);
        }

        [Fact]
        public void Delete()
        {
            long capacity = 10 * 1024 * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.FromCapacity(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.Equal(0, table.Create(1 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false));
            Assert.Equal(1, table.Create(2 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false));
            Assert.Equal(2, table.Create(3 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false));

            long[] sectorCount = new long[] { table[0].SectorCount, table[1].SectorCount, table[2].SectorCount };

            table.Delete(1);

            Assert.Equal(2, table.Count);
            Assert.Equal(sectorCount[2], table[1].SectorCount);
        }

        [Fact]
        public void SetActive()
        {
            long capacity = 10 * 1024 * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.FromCapacity(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            table.Create(1 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);
            table.Create(2 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);
            table.Create(3 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);

            table.SetActivePartition(1);
            table.SetActivePartition(2);
            Assert.False(((BiosPartitionInfo)table.Partitions[1]).IsActive);
            Assert.True(((BiosPartitionInfo)table.Partitions[2]).IsActive);
        }

        [Fact]
        public void LargeDisk()
        {
            long capacity = 300 * 1024L * 1024L * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.LbaAssistedBiosGeometry(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            table.Create(150 * 1024L * 1024L * 1024, WellKnownPartitionType.WindowsNtfs, false);
            table.Create(20 * 1024L * 1024L * 1024, WellKnownPartitionType.WindowsNtfs, false);
            table.Create(20 * 1024L * 1024L * 1024, WellKnownPartitionType.WindowsNtfs, false);

            Assert.Equal(3, table.Partitions.Count);
            Assert.True(table[0].SectorCount * 512L > 140 * 1024L * 1024L * 1024);
            Assert.True(table[1].SectorCount * 512L > 19 * 1024L * 1024L * 1024);
            Assert.True(table[2].SectorCount * 512L > 19 * 1024L * 1024L * 1024);

            Assert.True(table[0].FirstSector > 0);
            Assert.True(table[1].FirstSector > table[0].LastSector);
            Assert.True(table[2].FirstSector > table[1].LastSector);
        }

        [Fact]
        public void VeryLargePartition()
        {
            long capacity = 1300 * 1024L * 1024L * 1024;
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(capacity);
            Geometry geom = Geometry.LbaAssistedBiosGeometry(capacity);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            // exception occurs here
            int i = table.CreatePrimaryByCylinder(0, 150000, (byte)WellKnownPartitionType.WindowsNtfs, true);

            Assert.Equal(150000, geom.ToChsAddress(table[0].LastSector).Cylinder);
        }
    }
}

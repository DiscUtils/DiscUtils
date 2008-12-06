//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils.Partitions
{
    [TestFixture]
    public class BiosPartitionTableTest
    {
        [Test]
        public void Initialize()
        {
            MemoryStream ms = new MemoryStream();
            Geometry geom = Geometry.FromCapacity(3 * 1024 * 1024);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.AreEqual(0, table.Count);
        }

        [Test]
        public void CreateWholeDisk()
        {
            MemoryStream ms = new MemoryStream();
            Geometry geom = Geometry.FromCapacity(3 * 1024 * 1024);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            int idx = table.Create(WellKnownPartitionType.WindowsFat, true);

            // Make sure the partition fills all but the first track on the disk.
            Assert.AreEqual(geom.TotalSectors, table[idx].SectorCount + geom.SectorsPerTrack);

            // Make sure FAT16 was selected for a disk of this size
            Assert.AreEqual(BiosPartitionTypes.Fat16, table[idx].BiosType);

            // Make sure partition starts where expected
            Assert.AreEqual(new ChsAddress(0, 1, 1), ((BiosPartitionInfo)table[idx]).Start);

            // Make sure partition ends at end of disk
            Assert.AreEqual(geom.ToLogicalBlockAddress(geom.LastSector), table[idx].LastSector);
            Assert.AreEqual(geom.LastSector, ((BiosPartitionInfo)table[idx]).End);

            // Make sure the 'active' flag made it through...
            Assert.IsTrue(((BiosPartitionInfo)table[idx]).IsActive);
        }

        [Test]
        public void CreateBySize()
        {
            MemoryStream ms = new MemoryStream();
            Geometry geom = Geometry.FromCapacity(3 * 1024 * 1024);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            int idx = table.Create(2 * 1024 * 1024, WellKnownPartitionType.WindowsFat, false);

            // Make sure the partition is within 10% of the size requested.
            Assert.That((2 * 1024 * 2) * 0.9 < table[idx].SectorCount);

            Assert.AreEqual(geom.ToLogicalBlockAddress(new ChsAddress(0, 1, 1)), table[idx].FirstSector);
            Assert.AreEqual(geom.HeadsPerCylinder - 1, geom.ToChsAddress((int)table[idx].LastSector).Head);
            Assert.AreEqual(geom.SectorsPerTrack, geom.ToChsAddress((int)table[idx].LastSector).Sector);
        }

        [Test]
        public void CreateBySizeInGap()
        {
            MemoryStream ms = new MemoryStream();
            Geometry geom = new Geometry(15, 30, 63);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.AreEqual(0, table.CreatePrimaryByCylinder(0, 4, 33, false));
            Assert.AreEqual(1, table.CreatePrimaryByCylinder(10, 14, 33, false));
            table.Create(geom.ToLogicalBlockAddress(new ChsAddress(4, 0, 1)) * 512, WellKnownPartitionType.WindowsFat, true);
        }

        [Test]
        public void CreateByCylinder()
        {
            MemoryStream ms = new MemoryStream();
            Geometry geom = new Geometry(15, 30, 63);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

            Assert.AreEqual(0, table.CreatePrimaryByCylinder(0, 4, 33, false));
            Assert.AreEqual(1, table.CreatePrimaryByCylinder(10, 14, 33, false));

            Assert.AreEqual(geom.ToLogicalBlockAddress(new ChsAddress(0, 1, 1)), table[0].FirstSector);
            Assert.AreEqual(geom.ToLogicalBlockAddress(new ChsAddress(5, 0, 1)) - 1, table[0].LastSector);
            Assert.AreEqual(geom.ToLogicalBlockAddress(new ChsAddress(10, 0, 1)), table[1].FirstSector);
            Assert.AreEqual(geom.ToLogicalBlockAddress(new ChsAddress(14, 29, 63)), table[1].LastSector);
        }

        [Test]
        public void Delete()
        {
            MemoryStream ms = new MemoryStream();
            Geometry geom = Geometry.FromCapacity(10 * 1024 * 1024);
            BiosPartitionTable table = BiosPartitionTable.Initialize(ms, geom);

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

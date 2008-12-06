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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Represents a BIOS (MBR) Partition Table.
    /// </summary>
    public class BiosPartitionTable : PartitionTable
    {
        private Stream _diskData;
        private Geometry _diskGeometry;

        /// <summary>
        /// Creates a new instance to access an existing partition table on a disk.
        /// </summary>
        /// <param name="disk">The disk containing the partition table</param>
        public BiosPartitionTable(VirtualDisk disk)
        {
            Init(disk.Content, disk.Geometry);
        }

        /// <summary>
        /// Creates a new instance to access an existing partition table.
        /// </summary>
        /// <param name="disk">The stream containing the disk data</param>
        /// <param name="diskGeometry">The geometry of the disk</param>
        public BiosPartitionTable(Stream disk, Geometry diskGeometry)
        {
            Init(disk, diskGeometry);
        }

        /// <summary>
        /// Creates a new partition table on a disk.
        /// </summary>
        /// <param name="disk">The disk to initialize.</param>
        /// <returns>An object to access the newly created partition table</returns>
        public static BiosPartitionTable Initialize(VirtualDisk disk)
        {
            return Initialize(disk.Content, disk.Geometry);
        }

        /// <summary>
        /// Creates a new partition table on a disk containing a single partition.
        /// </summary>
        /// <param name="disk">The disk to initialize.</param>
        /// <param name="type">The partition type for the single partition</param>
        /// <returns>An object to access the newly created partition table</returns>
        public static BiosPartitionTable Initialize(VirtualDisk disk, WellKnownPartitionType type)
        {
            BiosPartitionTable table = Initialize(disk.Content, disk.Geometry);
            table.Create(type, true);
            return table;
        }

        /// <summary>
        /// Creates a new partition table on a disk.
        /// </summary>
        /// <param name="disk">The stream containing the disk data</param>
        /// <param name="diskGeometry">The geometry of the disk</param>
        /// <returns>An object to access the newly created partition table</returns>
        public static BiosPartitionTable Initialize(Stream disk, Geometry diskGeometry)
        {
            Stream data = disk;

            byte[] bootSector;
            if (data.Length >= Utilities.SectorSize)
            {
                data.Position = 0;
                bootSector = Utilities.ReadFully(data, Utilities.SectorSize);
            }
            else
            {
                bootSector = new byte[Utilities.SectorSize];
            }

            // Wipe all four 16-byte partition table entries
            Array.Clear(bootSector, 0x01BE, 16 * 4);

            // Marker bytes
            bootSector[510] = 0x55;
            bootSector[511] = 0xAA;

            data.Position = 0;
            data.Write(bootSector, 0, bootSector.Length);

            return new BiosPartitionTable(disk, diskGeometry);
        }

        /// <summary>
        /// Creates a new partition that encompasses the entire disk.
        /// </summary>
        /// <param name="type">The partition type</param>
        /// <param name="active">Whether the partition is active (bootable)</param>
        /// <returns>The index of the partition</returns>
        /// <remarks>The partition table must be empty before this method is called,
        /// otherwise IOException is thrown.</remarks>
        public override int Create(WellKnownPartitionType type, bool active)
        {
            ChsAddress start = new ChsAddress(0, 1, 1);
            ChsAddress last = _diskGeometry.LastSector;

            long startLba = _diskGeometry.ToLogicalBlockAddress(start);
            long lastLba = _diskGeometry.ToLogicalBlockAddress(last);

            return CreatePrimaryByCylinder(0, _diskGeometry.Cylinders - 1, ConvertType(type, (lastLba - startLba) * Utilities.SectorSize), active);
        }

        /// <summary>
        /// Creates a new primary partition with a target size.
        /// </summary>
        /// <param name="size">The target size (in bytes)</param>
        /// <param name="type">The partition type</param>
        /// <param name="active">Whether the partition is active (bootable)</param>
        /// <returns>The index of the new partition</returns>
        public override int Create(long size, WellKnownPartitionType type, bool active)
        {
            int cylinderCapacity = _diskGeometry.SectorsPerTrack * _diskGeometry.HeadsPerCylinder * _diskGeometry.BytesPerSector;
            int numCylinders = (int)(size / cylinderCapacity);

            int startCylinder = FindCylinderGap(numCylinders);

            return CreatePrimaryByCylinder(startCylinder, startCylinder + numCylinders - 1, ConvertType(type, size), active);
        }

        /// <summary>
        /// Deletes a partition at a given index.
        /// </summary>
        /// <param name="index">The index of the partition</param>
        public override void Delete(int index)
        {
            WriteRecord(index, new BiosPartitionRecord());
        }

        /// <summary>
        /// Creates a new Primary Partition that occupies whole cylinders, for best compatibility.
        /// </summary>
        /// <param name="first">The first cylinder to include in the partition (inclusive)</param>
        /// <param name="last">The last cylinder to include in the partition (inclusive)</param>
        /// <param name="type">The BIOS (MBR) type of the new partition</param>
        /// <param name="markActive">Whether to mark the partition active (bootable)</param>
        /// <returns>The index of the new partition</returns>
        /// <remarks>If the cylinder 0 is given, the first track will not be used, to reserve space
        /// for the meta-data at the start of the disk.</remarks>
        public int CreatePrimaryByCylinder(int first, int last, byte type, bool markActive)
        {
            if (first < 0)
            {
                throw new ArgumentOutOfRangeException("first", first, "First cylinder must be Zero or greater");
            }

            if (last <= first)
            {
                throw new ArgumentException("Last cylinder must be greater than first");
            }

            int lbaStart = (first == 0) ? _diskGeometry.ToLogicalBlockAddress(0, 1, 1) : _diskGeometry.ToLogicalBlockAddress(first, 0, 1);
            int lbaLast = _diskGeometry.ToLogicalBlockAddress(last, _diskGeometry.HeadsPerCylinder - 1, _diskGeometry.SectorsPerTrack);

            return CreatePrimaryBySector(lbaStart, lbaLast, type, markActive);
        }

        /// <summary>
        /// Creates a new Primary Partition, specified by Logical Block Addresses.
        /// </summary>
        /// <param name="first">The LBA address of the first sector (inclusive)</param>
        /// <param name="last">The LBA address of the last sector (inclusive)</param>
        /// <param name="type">The BIOS (MBR) type of the new partition</param>
        /// <param name="markActive">Whether to mark the partition active (bootable)</param>
        /// <returns>The index of the new partition</returns>
        public int CreatePrimaryBySector(int first, int last, byte type, bool markActive)
        {
            if (first >= last)
            {
                throw new ArgumentException("The first sector in a partition must be before the last");
            }

            if (last > _diskGeometry.TotalSectors)
            {
                throw new ArgumentOutOfRangeException("last", last, "The last sector extends beyond the end of the disk");
            }

            BiosPartitionRecord[] existing = GetPrimaryRecords();

            BiosPartitionRecord newRecord = new BiosPartitionRecord();
            ChsAddress startAddr = _diskGeometry.ToChsAddress(first);
            ChsAddress endAddr = _diskGeometry.ToChsAddress(last);

            // Because C/H/S addresses can max out at lower values than the LBA values,
            // the special tuple (1023, 254, 63) is used.
            if (startAddr.Cylinder > 1023)
            {
                startAddr = new ChsAddress(1023, 254, 63);
            }
            if (endAddr.Cylinder > 1023)
            {
                endAddr = new ChsAddress(1023, 254, 63);
            }

            newRecord.StartCylinder = (ushort)startAddr.Cylinder;
            newRecord.StartHead = (byte)startAddr.Head;
            newRecord.StartSector = (byte)startAddr.Sector;
            newRecord.EndCylinder = (ushort)endAddr.Cylinder;
            newRecord.EndHead = (byte)endAddr.Head;
            newRecord.EndSector = (byte)endAddr.Sector;
            newRecord.LBAStart = (uint)first;
            newRecord.LBALength = (uint)(last - first + 1);
            newRecord.PartitionType = type;
            newRecord.Status = (byte)(markActive ? 0x80 : 0x00);

            // First check for overlap with existing partition...
            foreach (var r in existing)
            {
                if (Utilities.RangesOverlap((uint)first, (uint)last + 1, r.LBAStartAbsolute, r.LBAStartAbsolute + r.LBALength))
                {
                    throw new IOException("New partition overlaps with existing partition");
                }
            }

            // Now look for empty partition
            for (int i = 0; i < 4; ++i)
            {
                if (!existing[i].IsValid)
                {
                    WriteRecord(i, newRecord);
                    return i;
                }
            }

            throw new IOException("No primary partition slots available");
        }

        /// <summary>
        /// Gets a collection of the partitions for storing Operating System file-systems.
        /// </summary>
        public ReadOnlyCollection<BiosPartitionInfo> BiosUserPartitions
        {
            get
            {
                List<BiosPartitionInfo> result = new List<BiosPartitionInfo>();
                foreach (BiosPartitionRecord r in GetAllRecords())
                {
                    if (r.IsValid)
                    {
                        result.Add(new BiosPartitionInfo(this, r));
                    }
                }
                return new ReadOnlyCollection<BiosPartitionInfo>(result);
            }
        }

        /// <summary>
        /// Gets a collection of the partitions for storing Operating System file-systems.
        /// </summary>
        public override ReadOnlyCollection<PartitionInfo> Partitions
        {
            get
            {
                List<PartitionInfo> result = new List<PartitionInfo>();
                foreach (BiosPartitionRecord r in GetAllRecords())
                {
                    if (r.IsValid)
                    {
                        result.Add(new BiosPartitionInfo(this, r));
                    }
                }
                return new ReadOnlyCollection<PartitionInfo>(result);
            }
        }

        private BiosPartitionRecord[] GetAllRecords()
        {
            List<BiosPartitionRecord> newList = new List<BiosPartitionRecord>();
            newList.AddRange(GetPrimaryRecords());
            newList.AddRange(GetExtendedRecords());
            return newList.ToArray();
        }

        private BiosPartitionRecord[] GetPrimaryRecords()
        {
            _diskData.Position = 0;
            byte[] bootSector = Utilities.ReadFully(_diskData, Utilities.SectorSize);

            BiosPartitionRecord[] records = new BiosPartitionRecord[4];
            records[0] = new BiosPartitionRecord(bootSector, 0x01BE, 0);
            records[1] = new BiosPartitionRecord(bootSector, 0x01CE, 0);
            records[2] = new BiosPartitionRecord(bootSector, 0x01DE, 0);
            records[3] = new BiosPartitionRecord(bootSector, 0x01EE, 0);

            return records;
        }

        private BiosPartitionRecord[] GetExtendedRecords()
        {
            List<BiosPartitionRecord> result = new List<BiosPartitionRecord>();
            foreach (var t in GetExtendedPartitionTables())
            {
                result.AddRange(t.GetPartitions());
            }
            return result.ToArray();
        }

        private BiosExtendedPartitionTable[] GetExtendedPartitionTables()
        {
            List<BiosExtendedPartitionTable> result = new List<BiosExtendedPartitionTable>();

            foreach (BiosPartitionRecord r in GetPrimaryRecords())
            {
                if (r.IsValid
                    && (r.PartitionType == BiosPartitionTypes.Extended
                        || r.PartitionType == BiosPartitionTypes.ExtendedLba))
                {
                    result.Add(new BiosExtendedPartitionTable(_diskData, _diskGeometry, r.LBAStart));
                }
            }

            return result.ToArray();
        }

        private void WriteRecord(int i, BiosPartitionRecord newRecord)
        {
            _diskData.Position = 0;
            byte[] bootSector = Utilities.ReadFully(_diskData, Utilities.SectorSize);
            newRecord.WriteTo(bootSector, 0x01BE + (i * 16));
            _diskData.Position = 0;
            _diskData.Write(bootSector, 0, bootSector.Length);
        }

        private static byte ConvertType(WellKnownPartitionType type, long size)
        {
            switch (type)
            {
                case WellKnownPartitionType.WindowsFat:
                    if (size < 512 * 1024 * 1024) // 512MB
                    {
                        return BiosPartitionTypes.Fat16;
                    }
                    else
                    {
                        return BiosPartitionTypes.Fat32;
                    }
                case WellKnownPartitionType.WindowsNtfs:
                    return BiosPartitionTypes.Ntfs;
                case WellKnownPartitionType.Linux:
                    return BiosPartitionTypes.LinuxNative;
                case WellKnownPartitionType.LinuxSwap:
                    return BiosPartitionTypes.LinuxSwap;
                case WellKnownPartitionType.LinuxLvm:
                    return BiosPartitionTypes.LinuxLvm;
                default:
                    throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Unrecognized partition type: '{0}'", type), "type");
            }
        }

        private int FindCylinderGap(int numCylinders)
        {
            var list = Utilities.Filter<List<BiosPartitionRecord>, BiosPartitionRecord>(GetPrimaryRecords(), (r) => r.IsValid);
            list.Sort();

            int startCylinder = 0;
            foreach (var r in list)
            {
                if (!Utilities.RangesOverlap(startCylinder, startCylinder + numCylinders - 1, r.StartCylinder, r.EndCylinder))
                {
                    break;
                }
                else
                {
                    startCylinder = r.EndCylinder + 1;
                }
            }

            return startCylinder;
        }

        private void Init(Stream disk, Geometry diskGeometry)
        {
            _diskData = disk;
            _diskGeometry = diskGeometry;

            _diskData.Position = 0;
            byte[] bootSector = Utilities.ReadFully(_diskData, Utilities.SectorSize);
            if (bootSector[510] != 0x55 || bootSector[511] != 0xAA)
            {
                throw new IOException("Invalid boot sector - no magic number 0xAA55");
            }
        }

        internal Stream Open(BiosPartitionRecord record)
        {
            return new SubStream(_diskData, false, record.LBAStartAbsolute * Utilities.SectorSize, record.LBALength * Utilities.SectorSize);
        }
    }
}

//
// Copyright (c) 2008-2010, Kenneth Bell
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
    public sealed class BiosPartitionTable : PartitionTable
    {
        private Stream _diskData;
        private Geometry _diskGeometry;

        /// <summary>
        /// Creates a new instance to access an existing partition table on a disk.
        /// </summary>
        /// <param name="disk">The disk containing the partition table</param>
        public BiosPartitionTable(VirtualDisk disk)
        {
            Init(disk.Content, disk.BiosGeometry);
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
            return Initialize(disk.Content, disk.BiosGeometry);
        }

        /// <summary>
        /// Creates a new partition table on a disk containing a single partition.
        /// </summary>
        /// <param name="disk">The disk to initialize.</param>
        /// <param name="type">The partition type for the single partition</param>
        /// <returns>An object to access the newly created partition table</returns>
        public static BiosPartitionTable Initialize(VirtualDisk disk, WellKnownPartitionType type)
        {
            BiosPartitionTable table = Initialize(disk.Content, disk.BiosGeometry);
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
        /// Gets the GUID that uniquely identifies this disk, if supported (else returns <c>null</c>).
        /// </summary>
        public override Guid DiskGuid
        {
            get { return Guid.Empty; }
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
            Geometry allocationGeometry = new Geometry(_diskData.Length, _diskGeometry.HeadsPerCylinder, _diskGeometry.SectorsPerTrack, _diskGeometry.BytesPerSector);

            ChsAddress start = new ChsAddress(0, 1, 1);
            ChsAddress last = allocationGeometry.LastSector;

            long startLba = allocationGeometry.ToLogicalBlockAddress(start);
            long lastLba = allocationGeometry.ToLogicalBlockAddress(last);

            return CreatePrimaryByCylinder(0, allocationGeometry.Cylinders - 1, ConvertType(type, (lastLba - startLba) * Utilities.SectorSize), active);
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

            long lbaStart = (first == 0) ? _diskGeometry.ToLogicalBlockAddress(0, 1, 1) : _diskGeometry.ToLogicalBlockAddress(first, 0, 1);
            long lbaLast = _diskGeometry.ToLogicalBlockAddress(last, _diskGeometry.HeadsPerCylinder - 1, _diskGeometry.SectorsPerTrack);

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
        public int CreatePrimaryBySector(long first, long last, byte type, bool markActive)
        {
            if (first >= last)
            {
                throw new ArgumentException("The first sector in a partition must be before the last");
            }

            if ((last + 1) * _diskGeometry.BytesPerSector > _diskData.Length)
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

        /// <summary>
        /// Sets the active partition.
        /// </summary>
        /// <param name="index">The index of the primary partition to mark bootable, or <c>-1</c> for none</param>
        /// <remarks>The supplied index is the index within the primary partition, see <c>PrimaryIndex</c> on <c>BiosPartitionInfo</c>.</remarks>
        public void SetActivePartition(int index)
        {
            List<BiosPartitionRecord> records = new List<BiosPartitionRecord>(GetPrimaryRecords());

            for (int i = 0; i < records.Count; ++i)
            {
                records[i].Status = (i == index) ? (byte)0x80 : (byte)0x00;
                WriteRecord(i, records[i]);
            }
        }

        /// <summary>
        /// Makes a best guess at the geometry of a disk.
        /// </summary>
        /// <param name="disk">String containing the disk image to detect the geometry from</param>
        /// <returns>The detected geometry</returns>
        public static Geometry DetectGeometry(Stream disk)
        {
            if (disk.Length >= Utilities.SectorSize)
            {
                disk.Position = 0;
                byte[] bootSector = Utilities.ReadFully(disk, Utilities.SectorSize);
                if (bootSector[510] == 0x55 && bootSector[511] == 0xAA)
                {
                    byte maxHead = 0;
                    byte maxSector = 0;
                    foreach (var record in ReadPrimaryRecords(bootSector))
                    {
                        maxHead = Math.Max(maxHead, record.EndHead);
                        maxSector = Math.Max(maxSector, record.EndSector);
                    }

                    if (maxHead > 0 && maxSector > 0)
                    {
                        int cylSize = (maxHead + 1) * maxSector * 512;
                        return new Geometry((int)Utilities.Ceil(disk.Length, cylSize), maxHead + 1, maxSector);
                    }
                }
            }

            return Geometry.FromCapacity(disk.Length);
        }

        /// <summary>
        /// Indicates if a stream contains a valid partition table.
        /// </summary>
        /// <param name="disk">The stream to inspect</param>
        /// <returns><c>true</c> if the partition table is valid, else <c>false</c>.</returns>
        public static bool IsValid(Stream disk)
        {
            if (disk.Length < Utilities.SectorSize)
            {
                return false;
            }

            disk.Position = 0;
            byte[] bootSector = Utilities.ReadFully(disk, Utilities.SectorSize);

            // Check for the 'bootable sector' marker
            if (bootSector[510] != 0x55 || bootSector[511] != 0xAA)
            {
                return false;
            }

            bool foundPartition = false;

            List<StreamExtent> knownPartitions = new List<StreamExtent>();
            foreach (var record in ReadPrimaryRecords(bootSector))
            {
                // If the partition extends beyond the end of the disk, this is probably an invalid partition table
                if ((record.LBAStart + record.LBALength) * Sizes.Sector > disk.Length)
                {
                    return false;
                }

                if (record.LBALength > 0)
                {
                    foundPartition = true;
                }

                StreamExtent[] thisPartitionExtents = new StreamExtent[] { new StreamExtent(record.LBAStart, record.LBALength) };

                // If the partition intersects another partition, this is probably an invalid partition table
                foreach (var overlap in StreamExtent.Intersect(knownPartitions, thisPartitionExtents))
                {
                    return false;
                }

                knownPartitions = new List<StreamExtent>(StreamExtent.Union(knownPartitions, thisPartitionExtents));
            }

            return foundPartition;
        }

        private BiosPartitionRecord[] GetAllRecords()
        {
            List<BiosPartitionRecord> newList = new List<BiosPartitionRecord>();

            foreach (BiosPartitionRecord primaryRecord in GetPrimaryRecords())
            {
                if (primaryRecord.IsValid)
                {
                    if (IsExtendedPartition(primaryRecord))
                    {
                        newList.AddRange(GetExtendedRecords(primaryRecord));
                    }
                    else
                    {
                        newList.Add(primaryRecord);
                    }
                }
            }

            return newList.ToArray();
        }

        private BiosPartitionRecord[] GetPrimaryRecords()
        {
            _diskData.Position = 0;
            byte[] bootSector = Utilities.ReadFully(_diskData, Utilities.SectorSize);

            return ReadPrimaryRecords(bootSector);
        }

        private static BiosPartitionRecord[] ReadPrimaryRecords(byte[] bootSector)
        {
            BiosPartitionRecord[] records = new BiosPartitionRecord[4];
            for (int i = 0; i < 4; ++i)
            {
                records[i] = new BiosPartitionRecord(bootSector, 0x01BE + i * 0x10, 0, i);
            }
            return records;
        }

        private BiosPartitionRecord[] GetExtendedRecords(BiosPartitionRecord r)
        {
            return new BiosExtendedPartitionTable(_diskData, r.LBAStart).GetPartitions();
        }

        private void WriteRecord(int i, BiosPartitionRecord newRecord)
        {
            _diskData.Position = 0;
            byte[] bootSector = Utilities.ReadFully(_diskData, Utilities.SectorSize);
            newRecord.WriteTo(bootSector, 0x01BE + (i * 16));
            _diskData.Position = 0;
            _diskData.Write(bootSector, 0, bootSector.Length);
        }

        private static bool IsExtendedPartition(BiosPartitionRecord r)
        {
            return r.PartitionType == BiosPartitionTypes.Extended || r.PartitionType == BiosPartitionTypes.ExtendedLba;
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
                    else if (size < 1023 * (long)254 * 63 * 512) // Max BIOS size
                    {
                        return BiosPartitionTypes.Fat32;
                    }
                    else
                    {
                        return BiosPartitionTypes.Fat32Lba;
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
                int existingStart = r.StartCylinder;
                int existingEnd = r.EndCylinder;

                // LBA can represent bigger disk locations than CHS, so assume the LBA to be definitive in the case where it
                // appears the CHS address has been truncated.
                if (r.LBAStart > _diskGeometry.ToLogicalBlockAddress(r.StartCylinder, r.StartHead, r.StartSector))
                {
                    existingStart = _diskGeometry.ToChsAddress((int)r.LBAStart).Cylinder;
                }
                if (r.LBAStart + r.LBALength > _diskGeometry.ToLogicalBlockAddress(r.EndCylinder, r.EndHead, r.EndSector))
                {
                    existingEnd = _diskGeometry.ToChsAddress((int)(r.LBAStart + r.LBALength)).Cylinder;
                }

                if (!Utilities.RangesOverlap(startCylinder, startCylinder + numCylinders - 1, existingStart, existingEnd))
                {
                    break;
                }
                else
                {
                    startCylinder = existingEnd + 1;
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

        internal SparseStream Open(BiosPartitionRecord record)
        {
            return new SubStream(_diskData, Ownership.None, ((long)record.LBAStartAbsolute) * Utilities.SectorSize, ((long)record.LBALength) * Utilities.SectorSize);
        }
    }
}

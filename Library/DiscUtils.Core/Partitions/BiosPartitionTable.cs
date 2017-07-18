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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

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
        /// Initializes a new instance of the BiosPartitionTable class.
        /// </summary>
        /// <param name="disk">The disk containing the partition table.</param>
        public BiosPartitionTable(VirtualDisk disk)
        {
            Init(disk.Content, disk.BiosGeometry);
        }

        /// <summary>
        /// Initializes a new instance of the BiosPartitionTable class.
        /// </summary>
        /// <param name="disk">The stream containing the disk data.</param>
        /// <param name="diskGeometry">The geometry of the disk.</param>
        public BiosPartitionTable(Stream disk, Geometry diskGeometry)
        {
            Init(disk, diskGeometry);
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
        /// Gets the GUID that uniquely identifies this disk, if supported (else returns <c>null</c>).
        /// </summary>
        public override Guid DiskGuid
        {
            get { return Guid.Empty; }
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
        /// Makes a best guess at the geometry of a disk.
        /// </summary>
        /// <param name="disk">String containing the disk image to detect the geometry from.</param>
        /// <returns>The detected geometry.</returns>
        public static Geometry DetectGeometry(Stream disk)
        {
            if (disk.Length >= Sizes.Sector)
            {
                disk.Position = 0;
                byte[] bootSector = StreamUtilities.ReadExact(disk, Sizes.Sector);
                if (bootSector[510] == 0x55 && bootSector[511] == 0xAA)
                {
                    byte maxHead = 0;
                    byte maxSector = 0;
                    foreach (BiosPartitionRecord record in ReadPrimaryRecords(bootSector))
                    {
                        maxHead = Math.Max(maxHead, record.EndHead);
                        maxSector = Math.Max(maxSector, record.EndSector);
                    }

                    if (maxHead > 0 && maxSector > 0)
                    {
                        int cylSize = (maxHead + 1) * maxSector * 512;
                        return new Geometry((int)MathUtilities.Ceil(disk.Length, cylSize), maxHead + 1, maxSector);
                    }
                }
            }

            return Geometry.FromCapacity(disk.Length);
        }

        /// <summary>
        /// Indicates if a stream contains a valid partition table.
        /// </summary>
        /// <param name="disk">The stream to inspect.</param>
        /// <returns><c>true</c> if the partition table is valid, else <c>false</c>.</returns>
        public static bool IsValid(Stream disk)
        {
            if (disk.Length < Sizes.Sector)
            {
                return false;
            }

            disk.Position = 0;
            byte[] bootSector = StreamUtilities.ReadExact(disk, Sizes.Sector);

            // Check for the 'bootable sector' marker
            if (bootSector[510] != 0x55 || bootSector[511] != 0xAA)
            {
                return false;
            }

            List<StreamExtent> knownPartitions = new List<StreamExtent>();
            foreach (BiosPartitionRecord record in ReadPrimaryRecords(bootSector))
            {
                // If the partition extends beyond the end of the disk, this is probably an invalid partition table
                if (record.LBALength != 0xFFFFFFFF &&
                    (record.LBAStart + (long)record.LBALength) * Sizes.Sector > disk.Length)
                {
                    return false;
                }

                if (record.LBALength > 0)
                {
                    StreamExtent[] thisPartitionExtents = { new StreamExtent(record.LBAStart, record.LBALength) };

                    // If the partition intersects another partition, this is probably an invalid partition table
                    foreach (StreamExtent overlap in StreamExtent.Intersect(knownPartitions, thisPartitionExtents))
                    {
                        return false;
                    }

                    knownPartitions = new List<StreamExtent>(StreamExtent.Union(knownPartitions, thisPartitionExtents));
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a new partition table on a disk.
        /// </summary>
        /// <param name="disk">The disk to initialize.</param>
        /// <returns>An object to access the newly created partition table.</returns>
        public static BiosPartitionTable Initialize(VirtualDisk disk)
        {
            return Initialize(disk.Content, disk.BiosGeometry);
        }

        /// <summary>
        /// Creates a new partition table on a disk containing a single partition.
        /// </summary>
        /// <param name="disk">The disk to initialize.</param>
        /// <param name="type">The partition type for the single partition.</param>
        /// <returns>An object to access the newly created partition table.</returns>
        public static BiosPartitionTable Initialize(VirtualDisk disk, WellKnownPartitionType type)
        {
            BiosPartitionTable table = Initialize(disk.Content, disk.BiosGeometry);
            table.Create(type, true);
            return table;
        }

        /// <summary>
        /// Creates a new partition table on a disk.
        /// </summary>
        /// <param name="disk">The stream containing the disk data.</param>
        /// <param name="diskGeometry">The geometry of the disk.</param>
        /// <returns>An object to access the newly created partition table.</returns>
        public static BiosPartitionTable Initialize(Stream disk, Geometry diskGeometry)
        {
            Stream data = disk;

            byte[] bootSector;
            if (data.Length >= Sizes.Sector)
            {
                data.Position = 0;
                bootSector = StreamUtilities.ReadExact(data, Sizes.Sector);
            }
            else
            {
                bootSector = new byte[Sizes.Sector];
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
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <returns>The index of the partition.</returns>
        /// <remarks>The partition table must be empty before this method is called,
        /// otherwise IOException is thrown.</remarks>
        public override int Create(WellKnownPartitionType type, bool active)
        {
            Geometry allocationGeometry = new Geometry(_diskData.Length, _diskGeometry.HeadsPerCylinder,
                _diskGeometry.SectorsPerTrack, _diskGeometry.BytesPerSector);

            ChsAddress start = new ChsAddress(0, 1, 1);
            ChsAddress last = allocationGeometry.LastSector;

            long startLba = allocationGeometry.ToLogicalBlockAddress(start);
            long lastLba = allocationGeometry.ToLogicalBlockAddress(last);

            return CreatePrimaryByCylinder(0, allocationGeometry.Cylinders - 1,
                ConvertType(type, (lastLba - startLba) * Sizes.Sector), active);
        }

        /// <summary>
        /// Creates a new primary partition with a target size.
        /// </summary>
        /// <param name="size">The target size (in bytes).</param>
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <returns>The index of the new partition.</returns>
        public override int Create(long size, WellKnownPartitionType type, bool active)
        {
            int cylinderCapacity = _diskGeometry.SectorsPerTrack * _diskGeometry.HeadsPerCylinder *
                                   _diskGeometry.BytesPerSector;
            int numCylinders = (int)(size / cylinderCapacity);

            int startCylinder = FindCylinderGap(numCylinders);

            return CreatePrimaryByCylinder(startCylinder, startCylinder + numCylinders - 1, ConvertType(type, size),
                active);
        }

        /// <summary>
        /// Creates a new aligned partition that encompasses the entire disk.
        /// </summary>
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <param name="alignment">The alignment (in bytes).</param>
        /// <returns>The index of the partition.</returns>
        /// <remarks>The partition table must be empty before this method is called,
        /// otherwise IOException is thrown.</remarks>
        /// <remarks>
        /// Traditionally partitions were aligned to the physical structure of the underlying disk,
        /// however with modern storage greater efficiency is acheived by aligning partitions on
        /// large values that are a power of two.
        /// </remarks>
        public override int CreateAligned(WellKnownPartitionType type, bool active, int alignment)
        {
            Geometry allocationGeometry = new Geometry(_diskData.Length, _diskGeometry.HeadsPerCylinder,
                _diskGeometry.SectorsPerTrack, _diskGeometry.BytesPerSector);

            ChsAddress start = new ChsAddress(0, 1, 1);

            long startLba = MathUtilities.RoundUp(allocationGeometry.ToLogicalBlockAddress(start),
                alignment / _diskGeometry.BytesPerSector);
            long lastLba = MathUtilities.RoundDown(_diskData.Length / _diskGeometry.BytesPerSector,
                alignment / _diskGeometry.BytesPerSector);

            return CreatePrimaryBySector(startLba, lastLba - 1,
                ConvertType(type, (lastLba - startLba) * _diskGeometry.BytesPerSector), active);
        }

        /// <summary>
        /// Creates a new aligned partition with a target size.
        /// </summary>
        /// <param name="size">The target size (in bytes).</param>
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <param name="alignment">The alignment (in bytes).</param>
        /// <returns>The index of the new partition.</returns>
        /// <remarks>
        /// Traditionally partitions were aligned to the physical structure of the underlying disk,
        /// however with modern storage greater efficiency is achieved by aligning partitions on
        /// large values that are a power of two.
        /// </remarks>
        public override int CreateAligned(long size, WellKnownPartitionType type, bool active, int alignment)
        {
            if (size < _diskGeometry.BytesPerSector)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "size must be at least one sector");
            }

            if (alignment % _diskGeometry.BytesPerSector != 0)
            {
                throw new ArgumentException("Alignment is not a multiple of the sector size");
            }

            if (size % alignment != 0)
            {
                throw new ArgumentException("Size is not a multiple of the alignment");
            }

            long sectorLength = size / _diskGeometry.BytesPerSector;
            long start = FindGap(size / _diskGeometry.BytesPerSector, alignment / _diskGeometry.BytesPerSector);

            return CreatePrimaryBySector(start, start + sectorLength - 1,
                ConvertType(type, sectorLength * Sizes.Sector), active);
        }

        /// <summary>
        /// Deletes a partition at a given index.
        /// </summary>
        /// <param name="index">The index of the partition.</param>
        public override void Delete(int index)
        {
            WriteRecord(index, new BiosPartitionRecord());
        }

        /// <summary>
        /// Creates a new Primary Partition that occupies whole cylinders, for best compatibility.
        /// </summary>
        /// <param name="first">The first cylinder to include in the partition (inclusive).</param>
        /// <param name="last">The last cylinder to include in the partition (inclusive).</param>
        /// <param name="type">The BIOS (MBR) type of the new partition.</param>
        /// <param name="markActive">Whether to mark the partition active (bootable).</param>
        /// <returns>The index of the new partition.</returns>
        /// <remarks>If the cylinder 0 is given, the first track will not be used, to reserve space
        /// for the meta-data at the start of the disk.</remarks>
        public int CreatePrimaryByCylinder(int first, int last, byte type, bool markActive)
        {
            if (first < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(first), first, "First cylinder must be Zero or greater");
            }

            if (last <= first)
            {
                throw new ArgumentException("Last cylinder must be greater than first");
            }

            long lbaStart = first == 0
                ? _diskGeometry.ToLogicalBlockAddress(0, 1, 1)
                : _diskGeometry.ToLogicalBlockAddress(first, 0, 1);
            long lbaLast = _diskGeometry.ToLogicalBlockAddress(last, _diskGeometry.HeadsPerCylinder - 1,
                _diskGeometry.SectorsPerTrack);

            return CreatePrimaryBySector(lbaStart, lbaLast, type, markActive);
        }

        /// <summary>
        /// Creates a new Primary Partition, specified by Logical Block Addresses.
        /// </summary>
        /// <param name="first">The LBA address of the first sector (inclusive).</param>
        /// <param name="last">The LBA address of the last sector (inclusive).</param>
        /// <param name="type">The BIOS (MBR) type of the new partition.</param>
        /// <param name="markActive">Whether to mark the partition active (bootable).</param>
        /// <returns>The index of the new partition.</returns>
        public int CreatePrimaryBySector(long first, long last, byte type, bool markActive)
        {
            if (first >= last)
            {
                throw new ArgumentException("The first sector in a partition must be before the last");
            }

            if ((last + 1) * _diskGeometry.BytesPerSector > _diskData.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(last), last,
                    "The last sector extends beyond the end of the disk");
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
            foreach (BiosPartitionRecord r in existing)
            {
                if (Utilities.RangesOverlap((uint)first, (uint)last + 1, r.LBAStartAbsolute,
                    r.LBAStartAbsolute + r.LBALength))
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
        /// Sets the active partition.
        /// </summary>
        /// <param name="index">The index of the primary partition to mark bootable, or <c>-1</c> for none.</param>
        /// <remarks>The supplied index is the index within the primary partition, see <c>PrimaryIndex</c> on <c>BiosPartitionInfo</c>.</remarks>
        public void SetActivePartition(int index)
        {
            List<BiosPartitionRecord> records = new List<BiosPartitionRecord>(GetPrimaryRecords());

            for (int i = 0; i < records.Count; ++i)
            {
                records[i].Status = i == index ? (byte)0x80 : (byte)0x00;
                WriteRecord(i, records[i]);
            }
        }

        /// <summary>
        /// Gets all of the disk ranges containing partition table metadata.
        /// </summary>
        /// <returns>Set of stream extents, indicated as byte offset from the start of the disk.</returns>
        public IEnumerable<StreamExtent> GetMetadataDiskExtents()
        {
            List<StreamExtent> extents = new List<StreamExtent>();

            extents.Add(new StreamExtent(0, Sizes.Sector));

            foreach (BiosPartitionRecord primaryRecord in GetPrimaryRecords())
            {
                if (primaryRecord.IsValid)
                {
                    if (IsExtendedPartition(primaryRecord))
                    {
                        extents.AddRange(
                            new BiosExtendedPartitionTable(_diskData, primaryRecord.LBAStart).GetMetadataDiskExtents());
                    }
                }
            }

            return extents;
        }

        /// <summary>
        /// Updates the CHS fields in partition records to reflect a new BIOS geometry.
        /// </summary>
        /// <param name="geometry">The disk's new BIOS geometry.</param>
        /// <remarks>The partitions are not relocated to a cylinder boundary, just the CHS fields are updated on the
        /// assumption the LBA fields are definitive.</remarks>
        public void UpdateBiosGeometry(Geometry geometry)
        {
            _diskData.Position = 0;
            byte[] bootSector = StreamUtilities.ReadExact(_diskData, Sizes.Sector);

            BiosPartitionRecord[] records = ReadPrimaryRecords(bootSector);
            for (int i = 0; i < records.Length; ++i)
            {
                BiosPartitionRecord record = records[i];
                if (record.IsValid)
                {
                    ChsAddress newStartAddress = geometry.ToChsAddress(record.LBAStartAbsolute);
                    if (newStartAddress.Cylinder > 1023)
                    {
                        newStartAddress = new ChsAddress(1023, geometry.HeadsPerCylinder - 1, geometry.SectorsPerTrack);
                    }

                    ChsAddress newEndAddress = geometry.ToChsAddress(record.LBAStartAbsolute + record.LBALength - 1);
                    if (newEndAddress.Cylinder > 1023)
                    {
                        newEndAddress = new ChsAddress(1023, geometry.HeadsPerCylinder - 1, geometry.SectorsPerTrack);
                    }

                    record.StartCylinder = (ushort)newStartAddress.Cylinder;
                    record.StartHead = (byte)newStartAddress.Head;
                    record.StartSector = (byte)newStartAddress.Sector;
                    record.EndCylinder = (ushort)newEndAddress.Cylinder;
                    record.EndHead = (byte)newEndAddress.Head;
                    record.EndSector = (byte)newEndAddress.Sector;

                    WriteRecord(i, record);
                }
            }

            _diskGeometry = geometry;
        }

        internal SparseStream Open(BiosPartitionRecord record)
        {
            return new SubStream(_diskData, Ownership.None,
                record.LBAStartAbsolute * _diskGeometry.BytesPerSector,
                record.LBALength * _diskGeometry.BytesPerSector);
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

        private static bool IsExtendedPartition(BiosPartitionRecord r)
        {
            return r.PartitionType == BiosPartitionTypes.Extended || r.PartitionType == BiosPartitionTypes.ExtendedLba;
        }

        private static byte ConvertType(WellKnownPartitionType type, long size)
        {
            switch (type)
            {
                case WellKnownPartitionType.WindowsFat:
                    if (size < 512 * Sizes.OneMiB)
                    {
                        return BiosPartitionTypes.Fat16;
                    }
                    if (size < 1023 * (long)254 * 63 * 512)
                    {
                        // Max BIOS size
                        return BiosPartitionTypes.Fat32;
                    }
                    return BiosPartitionTypes.Fat32Lba;

                case WellKnownPartitionType.WindowsNtfs:
                    return BiosPartitionTypes.Ntfs;
                case WellKnownPartitionType.Linux:
                    return BiosPartitionTypes.LinuxNative;
                case WellKnownPartitionType.LinuxSwap:
                    return BiosPartitionTypes.LinuxSwap;
                case WellKnownPartitionType.LinuxLvm:
                    return BiosPartitionTypes.LinuxLvm;
                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Unrecognized partition type: '{0}'", type),
                        nameof(type));
            }
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
            byte[] bootSector = StreamUtilities.ReadExact(_diskData, Sizes.Sector);

            return ReadPrimaryRecords(bootSector);
        }

        private BiosPartitionRecord[] GetExtendedRecords(BiosPartitionRecord r)
        {
            return new BiosExtendedPartitionTable(_diskData, r.LBAStart).GetPartitions();
        }

        private void WriteRecord(int i, BiosPartitionRecord newRecord)
        {
            _diskData.Position = 0;
            byte[] bootSector = StreamUtilities.ReadExact(_diskData, Sizes.Sector);
            newRecord.WriteTo(bootSector, 0x01BE + i * 16);
            _diskData.Position = 0;
            _diskData.Write(bootSector, 0, bootSector.Length);
        }

        private int FindCylinderGap(int numCylinders)
        {
            List<BiosPartitionRecord> list = Utilities.Filter<List<BiosPartitionRecord>, BiosPartitionRecord>(GetPrimaryRecords(),
                r => r.IsValid);
            list.Sort();

            int startCylinder = 0;
            foreach (BiosPartitionRecord r in list)
            {
                int existingStart = r.StartCylinder;
                int existingEnd = r.EndCylinder;

                // LBA can represent bigger disk locations than CHS, so assume the LBA to be definitive in the case where it
                // appears the CHS address has been truncated.
                if (r.LBAStart > _diskGeometry.ToLogicalBlockAddress(r.StartCylinder, r.StartHead, r.StartSector))
                {
                    existingStart = _diskGeometry.ToChsAddress((int)r.LBAStart).Cylinder;
                }

                if (r.LBAStart + r.LBALength >
                    _diskGeometry.ToLogicalBlockAddress(r.EndCylinder, r.EndHead, r.EndSector))
                {
                    existingEnd = _diskGeometry.ToChsAddress((int)(r.LBAStart + r.LBALength)).Cylinder;
                }

                if (
                    !Utilities.RangesOverlap(startCylinder, startCylinder + numCylinders - 1, existingStart, existingEnd))
                {
                    break;
                }
                startCylinder = existingEnd + 1;
            }

            return startCylinder;
        }

        private long FindGap(long numSectors, long alignmentSectors)
        {
            List<BiosPartitionRecord> list = Utilities.Filter<List<BiosPartitionRecord>, BiosPartitionRecord>(GetPrimaryRecords(),
                r => r.IsValid);
            list.Sort();

            long startSector = MathUtilities.RoundUp(_diskGeometry.ToLogicalBlockAddress(0, 1, 1), alignmentSectors);

            int idx = 0;
            while (idx < list.Count)
            {
                BiosPartitionRecord entry = list[idx];
                while (idx < list.Count && startSector >= entry.LBAStartAbsolute + entry.LBALength)
                {
                    idx++;
                    entry = list[idx];
                }

                if (Utilities.RangesOverlap(startSector, startSector + numSectors, entry.LBAStartAbsolute,
                    entry.LBAStartAbsolute + entry.LBALength))
                {
                    startSector = MathUtilities.RoundUp(entry.LBAStartAbsolute + entry.LBALength, alignmentSectors);
                }

                idx++;
            }

            if (_diskGeometry.TotalSectorsLong - startSector < numSectors)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "Unable to find free space of {0} sectors", numSectors));
            }

            return startSector;
        }

        private void Init(Stream disk, Geometry diskGeometry)
        {
            _diskData = disk;
            _diskGeometry = diskGeometry;

            _diskData.Position = 0;
            byte[] bootSector = StreamUtilities.ReadExact(_diskData, Sizes.Sector);
            if (bootSector[510] != 0x55 || bootSector[511] != 0xAA)
            {
                throw new IOException("Invalid boot sector - no magic number 0xAA55");
            }
        }
    }
}
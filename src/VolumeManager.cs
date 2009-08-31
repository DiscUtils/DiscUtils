//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.Globalization;
using System.IO;
using DiscUtils.Partitions;

namespace DiscUtils
{
    /// <summary>
    /// VolumeManager interprets partitions and other on-disk structures (possibly combining multiple disks).
    /// </summary>
    /// <remarks>
    /// <para>Although file systems commonly are placed directly within partitions on a disk, in some
    /// cases a logical volume manager / logical disk manager may be used, to combine disk regions in multiple
    /// ways for data redundancy or other purposes.</para>
    /// <para>Initially, VolumeManager only supports so-called 'physical volumes' (aka 'hard partitions'), which
    /// directly correspond to the partitions on a disk.</para>
    /// </remarks>
    public sealed class VolumeManager
    {
        private List<VirtualDisk> _disks;
        private bool _needScan;

        private Dictionary<string, PhysicalVolumeInfo> _physicalVolumes;
        private Dictionary<string, LogicalVolumeInfo> _logicalVolumes;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public VolumeManager()
        {
            _disks = new List<VirtualDisk>();
            _physicalVolumes = new Dictionary<string, PhysicalVolumeInfo>();
            _logicalVolumes = new Dictionary<string, LogicalVolumeInfo>();
        }

        /// <summary>
        /// Creates a new instance from the contents of an existing disk.
        /// </summary>
        /// <param name="initialDisk">The initial disk to add.</param>
        public VolumeManager(VirtualDisk initialDisk)
            : this()
        {
            AddDisk(initialDisk);
        }

        /// <summary>
        /// Creates a new instance from the contents of an existing disk.
        /// </summary>
        /// <param name="initialDiskContent">Content of the initial disk to add.</param>
        public VolumeManager(Stream initialDiskContent)
            : this()
        {
            AddDisk(initialDiskContent);
        }

        /// <summary>
        /// Adds a disk to the volume manager.
        /// </summary>
        /// <param name="disk">The disk to add</param>
        /// <returns>The GUID the volume manager will use to identify the disk</returns>
        public string AddDisk(VirtualDisk disk)
        {
            _needScan = true;
            int ordinal = _disks.Count;
            _disks.Add(disk);
            return GetDiskId(ordinal);
        }

        /// <summary>
        /// Adds a disk to the volume manager.
        /// </summary>
        /// <param name="content">The contents of the disk to add</param>
        /// <returns>The GUID the volume manager will use to identify the disk</returns>
        public string AddDisk(Stream content)
        {
            return AddDisk(new Raw.Disk(content, Ownership.None));
        }

        /// <summary>
        /// Gets the physical volumes from all disks added to this volume manager.
        /// </summary>
        /// <returns>An array of physical volumes</returns>
        public PhysicalVolumeInfo[] GetPhysicalVolumes()
        {
            if (_needScan)
            {
                Scan();
            }

            return new List<PhysicalVolumeInfo>(_physicalVolumes.Values).ToArray();
        }

        /// <summary>
        /// Gets the logical volumes from all disks added to this volume manager.
        /// </summary>
        /// <returns>An array of logical volumes</returns>
        public LogicalVolumeInfo[] GetLogicalVolumes()
        {
            if (_needScan)
            {
                Scan();
            }

            return new List<LogicalVolumeInfo>(_logicalVolumes.Values).ToArray();
        }

        /// <summary>
        /// Gets a particular volume, based on it's identity.
        /// </summary>
        /// <param name="identity">The volume's identity</param>
        /// <returns>The volume information for the volume, or returns <c>null</c></returns>
        public VolumeInfo GetVolume(string identity)
        {
            if (_needScan)
            {
                Scan();
            }

            PhysicalVolumeInfo pvi;
            if (_physicalVolumes.TryGetValue(identity, out pvi))
            {
                return pvi;
            }

            LogicalVolumeInfo lvi;
            if (_logicalVolumes.TryGetValue(identity, out lvi))
            {
                return lvi;
            }

            return null;
        }

        /// <summary>
        /// Gets the physical volumes held on a disk.
        /// </summary>
        /// <param name="diskContent">The contents of the disk to inspect</param>
        /// <returns>An array of volumes</returns>
        /// <remarks>
        /// <para>By preference, use the form of this method that takes a disk parameter.</para>
        /// <para>If the disk isn't partitioned, this method returns the entire disk contents
        /// as a single volume.</para>
        /// </remarks>
        public static PhysicalVolumeInfo[] GetPhysicalVolumes(Stream diskContent)
        {
            return GetPhysicalVolumes(new Raw.Disk(diskContent, Ownership.None));
        }

        /// <summary>
        /// Gets the physical volumes held on a disk.
        /// </summary>
        /// <param name="disk">The disk to inspect</param>
        /// <returns>An array of volumes</returns>
        /// <remarks>If the disk isn't partitioned, this method returns the entire disk contents
        /// as a single volume.</remarks>
        public static PhysicalVolumeInfo[] GetPhysicalVolumes(VirtualDisk disk)
        {
            return new VolumeManager(disk).GetPhysicalVolumes();
        }

        /// <summary>
        /// Scans all of the disks for their physical and logical volumes.
        /// </summary>
        private void Scan()
        {
            Dictionary<string,PhysicalVolumeInfo> newPhysicalVolumes = ScanForPhysicalVolumes();
            Dictionary<string, LogicalVolumeInfo> newLogicalVolumes = ScanForLogicalVolumes(newPhysicalVolumes);

            _physicalVolumes = newPhysicalVolumes;
            _logicalVolumes = newLogicalVolumes;

            _needScan = false;
        }

        private static Dictionary<string, LogicalVolumeInfo> ScanForLogicalVolumes(Dictionary<string, PhysicalVolumeInfo> physicalVolumes)
        {
            Dictionary<string, LogicalVolumeInfo> result = new Dictionary<string, LogicalVolumeInfo>();

            foreach (var physicalVol in physicalVolumes.Values)
            {
                LogicalVolumeInfo lvi = new LogicalVolumeInfo(
                    physicalVol.PartitionIdentity,
                    physicalVol.Identity,
                    delegate() { return physicalVol.Open(); },
                    physicalVol.Length);

                result.Add(lvi.Identity, lvi);
            }

            return result;
        }

        private Dictionary<string, PhysicalVolumeInfo> ScanForPhysicalVolumes()
        {
            Dictionary<string, PhysicalVolumeInfo> result = new Dictionary<string, PhysicalVolumeInfo>();

            // First scan physical volumes
            for (int i = 0; i < _disks.Count; ++i)
            {
                VirtualDisk disk = _disks[i];
                string diskId = GetDiskId(i);

                if (disk.IsPartitioned)
                {
                    PartitionTable pt = disk.Partitions;
                    int diskSig = disk.Signature;
                    Guid diskGuid = pt.DiskGuid;
                    PhysicalVolumeType type = (pt is GuidPartitionTable) ? PhysicalVolumeType.GptPartition : PhysicalVolumeType.BiosPartition;

                    for (int j = 0; j < pt.Count; ++j)
                    {
                        PartitionInfo pi = pt[j];
                        GuidPartitionInfo gpi = pi as GuidPartitionInfo;

                        PhysicalVolumeInfo pvi = new PhysicalVolumeInfo(
                            pi.Open,
                            diskId,
                            diskSig,
                            diskGuid,
                            gpi == null ? PhysicalVolumeType.BiosPartition : PhysicalVolumeType.GptPartition,
                            gpi == null ? Guid.Empty : gpi.Identity,
                            pi.FirstSector * Sizes.Sector,
                            pi.SectorCount * Sizes.Sector);

                        result.Add(pvi.Identity, pvi);
                    }

                }
                else
                {
                    // Can't just return disk.Content because it may be opened and closed multiple times.
                    SparseStreamOpenDelegate openDelegate = delegate() { return new SubStream(disk.Content, Ownership.None, 0, disk.Capacity); };

                    PhysicalVolumeInfo pvi = new PhysicalVolumeInfo(
                        openDelegate,
                        diskId,
                        0,
                        Guid.Empty,
                        PhysicalVolumeType.EntireDisk,
                        Guid.Empty,
                        0,
                        disk.Capacity);
                    result.Add(pvi.Identity, pvi);
                }
            }

            return result;
        }

        private string GetDiskId(int ordinal)
        {
            VirtualDisk disk = _disks[ordinal];
            if (disk.IsPartitioned)
            {
                Guid guid = disk.Partitions.DiskGuid;
                if (guid != Guid.Empty)
                {
                    return "DG" + guid.ToString("B");
                }
            }

            int sig = disk.Signature;
            if (sig != 0)
            {
                return "DS" + sig.ToString("X8", CultureInfo.InvariantCulture);
            }

            return "DO" + ordinal;
        }

    }
}

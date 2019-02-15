//
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Lvm
{
    using System.Collections.Generic;
    using DiscUtils.Partitions;

    /// <summary>
    /// A class that understands Linux LVM structures, mapping physical volumes to logical volumes.
    /// </summary>
    public class LogicalVolumeManager
    {
        private List<PhysicalVolume> _devices;
        private List<MetadataVolumeGroupSection> _volumeGroups;
        /// <summary>
        /// Initializes a new instance of the LogicalVolumeManager class.
        /// </summary>
        /// <param name="disks">The initial set of disks to manage.</param>
        public LogicalVolumeManager(IEnumerable<VirtualDisk> disks)
        {
            _devices = new List<PhysicalVolume>();
            _volumeGroups = new List<MetadataVolumeGroupSection>();
            foreach (var disk in disks)
            {
                if (disk.IsPartitioned)
                {
                    foreach (var partition in disk.Partitions.Partitions)
                    {
                        PhysicalVolume pv;
                        if (PhysicalVolume.TryOpen(partition, out pv))
                        {
                            _devices.Add(pv);
                        }
                    }
                }
                else
                {
                    PhysicalVolume pv;
                    if (PhysicalVolume.TryOpen(disk.Content, out pv))
                    {
                        _devices.Add(pv);
                    }
                }
            }
            foreach (var device in _devices)
            {
                foreach (var vg in device.VgMetadata.ParsedMetadata.VolumeGroupSections)
                {
                    if (!_volumeGroups.Exists(x => x.Id == vg.Id))
                    {
                        _volumeGroups.Add(vg);
                    }
                }
            }
        }

        /// <summary>
        /// Determines if a physical volume contains LVM data.
        /// </summary>
        /// <param name="volumeInfo">The volume to inspect.</param>
        /// <returns><c>true</c> if the physical volume contains LVM data, else <c>false</c>.</returns>
        public static bool HandlesPhysicalVolume(PhysicalVolumeInfo volumeInfo)
        {
            var partition = volumeInfo.Partition;
            if (partition == null) return false;
            return partition.BiosType == BiosPartitionTypes.LinuxLvm ||
                   partition.GuidType == GuidPartitionTypes.LinuxLvm;
        }

        /// <summary>
        /// Gets the logical volumes held across the set of managed disks.
        /// </summary>
        /// <returns>An array of logical volumes.</returns>
        public LogicalVolumeInfo[] GetLogicalVolumes()
        {
            List<LogicalVolumeInfo> result = new List<LogicalVolumeInfo>();
            foreach (var vg in _volumeGroups)
            {
                foreach (var lv in vg.LogicalVolumes)
                {
                    var pvs = new Dictionary<string, PhysicalVolume>();
                    bool allPvsAvailable = true;
                    bool segmentTypesSupported = true;
                    foreach (var segment in lv.Segments)
                    {
                        if (segment.Type != SegmentType.Striped)
                            segmentTypesSupported = false;
                        foreach (var stripe in segment.Stripes)
                        {
                            var pvAlias = stripe.PhysicalVolumeName;
                            if (!pvs.ContainsKey(pvAlias))
                            {
                                var pvm = GetPhysicalVolumeMetadata(vg, pvAlias);
                                if (pvm == null)
                                {
                                    allPvsAvailable = false;
                                    break;
                                }
                                var pv = GetPhysicalVolume(pvm.Id);
                                if (pv == null)
                                {
                                    allPvsAvailable = false;
                                    break;
                                }
                                pvs.Add(pvm.Name, pv);
                            }
                        }
                        if (!allPvsAvailable || !segmentTypesSupported)
                            break;
                    }
                    if (allPvsAvailable && segmentTypesSupported)
                    {
                        LogicalVolumeInfo lvi = new LogicalVolumeInfo(
                            lv.Identity,
                            null,
                            lv.Open(pvs, vg.ExtentSize),
                            lv.ExtentCount * (long) vg.ExtentSize * PhysicalVolume.SECTOR_SIZE,
                            0,
                            DiscUtils.LogicalVolumeStatus.Healthy);
                        result.Add(lvi);
                    }
                }
            }
            return result.ToArray();
        }

        private PhysicalVolume GetPhysicalVolume(string id)
        {
            foreach (var pv in _devices)
            {
                if (pv.PvHeader.Uuid == id)
                    return pv;
            }
            return null;
        }

        private MetadataPhysicalVolumeSection GetPhysicalVolumeMetadata(MetadataVolumeGroupSection vg, string name)
        {
            foreach (var pv in vg.PhysicalVolumes)
            {
                if (pv.Name == name)
                    return pv;
            }
            return null;
        }
    }
}

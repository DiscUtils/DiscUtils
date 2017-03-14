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

using System.Collections.Generic;
using System.IO;
using DiscUtils.Partitions;

namespace DiscUtils.LogicalDiskManager
{
    /// <summary>
    /// A class that understands Windows LDM structures, mapping physical volumes to logical volumes.
    /// </summary>
    public class DynamicDiskManager : IDiagnosticTraceable
    {
        private readonly Dictionary<string, DynamicDiskGroup> _groups;

        /// <summary>
        /// Initializes a new instance of the DynamicDiskManager class.
        /// </summary>
        /// <param name="disks">The initial set of disks to manage.</param>
        public DynamicDiskManager(params VirtualDisk[] disks)
        {
            _groups = new Dictionary<string, DynamicDiskGroup>();

            foreach (VirtualDisk disk in disks)
            {
                Add(disk);
            }
        }

        /// <summary>
        /// Writes a diagnostic report about the state of the disk manager.
        /// </summary>
        /// <param name="writer">The writer to send the report to.</param>
        /// <param name="linePrefix">The prefix to place at the start of each line.</param>
        public void Dump(TextWriter writer, string linePrefix)
        {
            writer.WriteLine(linePrefix + "DISK GROUPS");
            foreach (DynamicDiskGroup group in _groups.Values)
            {
                group.Dump(writer, linePrefix + "  ");
            }
        }

        /// <summary>
        /// Determines if a physical volume contains LDM data.
        /// </summary>
        /// <param name="volumeInfo">The volume to inspect.</param>
        /// <returns><c>true</c> if the physical volume contains LDM data, else <c>false</c>.</returns>
        public static bool HandlesPhysicalVolume(PhysicalVolumeInfo volumeInfo)
        {
            PartitionInfo pi = volumeInfo.Partition;
            if (pi != null)
            {
                return IsLdmPartition(pi);
            }

            return false;
        }

        /// <summary>
        /// Determines if a disk is 'dynamic' (i.e. contains LDM volumes).
        /// </summary>
        /// <param name="disk">The disk to inspect.</param>
        /// <returns><c>true</c> if the disk contains LDM volumes, else <c>false</c>.</returns>
        public static bool IsDynamicDisk(VirtualDisk disk)
        {
            if (disk.IsPartitioned)
            {
                foreach (PartitionInfo partition in disk.Partitions.Partitions)
                {
                    if (IsLdmPartition(partition))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a new disk to be managed.
        /// </summary>
        /// <param name="disk">The disk to manage.</param>
        public void Add(VirtualDisk disk)
        {
            PrivateHeader header = DynamicDisk.GetPrivateHeader(disk);

            DynamicDiskGroup group;
            if (_groups.TryGetValue(header.DiskGroupId, out group))
            {
                group.Add(disk);
            }
            else
            {
                group = new DynamicDiskGroup(disk);
                _groups.Add(header.DiskGroupId, group);
            }
        }

        /// <summary>
        /// Gets the logical volumes held across the set of managed disks.
        /// </summary>
        /// <returns>An array of logical volumes.</returns>
        public LogicalVolumeInfo[] GetLogicalVolumes()
        {
            List<LogicalVolumeInfo> result = new List<LogicalVolumeInfo>();
            foreach (DynamicDiskGroup group in _groups.Values)
            {
                foreach (DynamicVolume volume in group.GetVolumes())
                {
                    LogicalVolumeInfo lvi = new LogicalVolumeInfo(
                        volume.Identity,
                        null,
                        volume.Open,
                        volume.Length,
                        volume.BiosType,
                        volume.Status);
                    result.Add(lvi);
                }
            }

            return result.ToArray();
        }

        private static bool IsLdmPartition(PartitionInfo partition)
        {
            return partition.BiosType == BiosPartitionTypes.WindowsDynamicVolume
                   || partition.GuidType == GuidPartitionTypes.WindowsLdmMetadata
                   || partition.GuidType == GuidPartitionTypes.WindowsLdmData;
        }
    }
}
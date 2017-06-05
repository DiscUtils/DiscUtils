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
using System.Globalization;
using System.IO;
using System.Reflection;
using DiscUtils.CoreCompat;
using DiscUtils.Internal;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils.Streams;

namespace DiscUtils
{
    /// <summary>
    /// VolumeManager interprets partitions and other on-disk structures (possibly combining multiple disks).
    /// </summary>
    /// <remarks>
    /// <para>Although file systems commonly are placed directly within partitions on a disk, in some
    /// cases a logical volume manager / logical disk manager may be used, to combine disk regions in multiple
    /// ways for data redundancy or other purposes.</para>
    /// </remarks>
    public sealed class VolumeManager
#if !NETCORE
        : MarshalByRefObject
#endif
    {
        private static List<LogicalVolumeFactory> s_logicalVolumeFactories;
        private readonly List<VirtualDisk> _disks;
        private bool _needScan;

        private Dictionary<string, PhysicalVolumeInfo> _physicalVolumes;
        private Dictionary<string, LogicalVolumeInfo> _logicalVolumes;
        private static readonly Assembly _coreAssembly = ReflectionHelper.GetAssembly(typeof(VolumeManager));

        /// <summary>
        /// Initializes a new instance of the VolumeManager class.
        /// </summary>
        public VolumeManager()
        {
            _disks = new List<VirtualDisk>();
            _physicalVolumes = new Dictionary<string, PhysicalVolumeInfo>();
            _logicalVolumes = new Dictionary<string, LogicalVolumeInfo>();
        }

        /// <summary>
        /// Initializes a new instance of the VolumeManager class.
        /// </summary>
        /// <param name="initialDisk">The initial disk to add.</param>
        public VolumeManager(VirtualDisk initialDisk)
            : this()
        {
            AddDisk(initialDisk);
        }

        /// <summary>
        /// Initializes a new instance of the VolumeManager class.
        /// </summary>
        /// <param name="initialDiskContent">Content of the initial disk to add.</param>
        public VolumeManager(Stream initialDiskContent)
            : this()
        {
            AddDisk(initialDiskContent);
        }

        private static List<LogicalVolumeFactory> LogicalVolumeFactories
        {
            get
            {
                if (s_logicalVolumeFactories == null)
                {
                    List<LogicalVolumeFactory> factories = new List<LogicalVolumeFactory>();
                    factories.AddRange(GetLogicalVolumeFactories(_coreAssembly));
                    s_logicalVolumeFactories = factories;
                }

                return s_logicalVolumeFactories;
            }
        }

        private static IEnumerable<LogicalVolumeFactory> GetLogicalVolumeFactories(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                foreach (LogicalVolumeFactoryAttribute attr in ReflectionHelper.GetCustomAttributes(type, typeof(LogicalVolumeFactoryAttribute), false))
                {
                    yield return (LogicalVolumeFactory)Activator.CreateInstance(type);
                }
            }
        }

        /// <summary>
        /// Register new LogicalVolumeFactories detected in an assembly
        /// </summary>
        /// <param name="assembly">The assembly to inspect</param>
        public static void RegisterLogicalVolumeFactory(Assembly assembly)
        {
            if (assembly == _coreAssembly) return;
            LogicalVolumeFactories.AddRange(GetLogicalVolumeFactories(assembly));
        }

        /// <summary>
        /// Gets the physical volumes held on a disk.
        /// </summary>
        /// <param name="diskContent">The contents of the disk to inspect.</param>
        /// <returns>An array of volumes.</returns>
        /// <remarks>
        /// <para>By preference, use the form of this method that takes a disk parameter.</para>
        /// <para>If the disk isn't partitioned, this method returns the entire disk contents
        /// as a single volume.</para>
        /// </remarks>
        public static PhysicalVolumeInfo[] GetPhysicalVolumes(Stream diskContent)
        {
            return GetPhysicalVolumes(new Disk(diskContent, Ownership.None));
        }

        /// <summary>
        /// Gets the physical volumes held on a disk.
        /// </summary>
        /// <param name="disk">The disk to inspect.</param>
        /// <returns>An array of volumes.</returns>
        /// <remarks>If the disk isn't partitioned, this method returns the entire disk contents
        /// as a single volume.</remarks>
        public static PhysicalVolumeInfo[] GetPhysicalVolumes(VirtualDisk disk)
        {
            return new VolumeManager(disk).GetPhysicalVolumes();
        }

        /// <summary>
        /// Adds a disk to the volume manager.
        /// </summary>
        /// <param name="disk">The disk to add.</param>
        /// <returns>The GUID the volume manager will use to identify the disk.</returns>
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
        /// <param name="content">The contents of the disk to add.</param>
        /// <returns>The GUID the volume manager will use to identify the disk.</returns>
        public string AddDisk(Stream content)
        {
            return AddDisk(new Disk(content, Ownership.None));
        }

        /// <summary>
        /// Gets the physical volumes from all disks added to this volume manager.
        /// </summary>
        /// <returns>An array of physical volumes.</returns>
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
        /// <returns>An array of logical volumes.</returns>
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
        /// <param name="identity">The volume's identity.</param>
        /// <returns>The volume information for the volume, or returns <c>null</c>.</returns>
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

        private static void MapPhysicalVolumes(IEnumerable<PhysicalVolumeInfo> physicalVols, Dictionary<string, LogicalVolumeInfo> result)
        {
            foreach (PhysicalVolumeInfo physicalVol in physicalVols)
            {
                LogicalVolumeInfo lvi = new LogicalVolumeInfo(
                    physicalVol.PartitionIdentity,
                    physicalVol,
                    physicalVol.Open,
                    physicalVol.Length,
                    physicalVol.BiosType,
                    LogicalVolumeStatus.Healthy);

                result.Add(lvi.Identity, lvi);
            }
        }

        /// <summary>
        /// Scans all of the disks for their physical and logical volumes.
        /// </summary>
        private void Scan()
        {
            Dictionary<string, PhysicalVolumeInfo> newPhysicalVolumes = ScanForPhysicalVolumes();
            Dictionary<string, LogicalVolumeInfo> newLogicalVolumes = ScanForLogicalVolumes(newPhysicalVolumes.Values);

            _physicalVolumes = newPhysicalVolumes;
            _logicalVolumes = newLogicalVolumes;

            _needScan = false;
        }

        private Dictionary<string, LogicalVolumeInfo> ScanForLogicalVolumes(IEnumerable<PhysicalVolumeInfo> physicalVols)
        {
            List<PhysicalVolumeInfo> unhandledPhysical = new List<PhysicalVolumeInfo>();
            Dictionary<string, LogicalVolumeInfo> result = new Dictionary<string, LogicalVolumeInfo>();

            foreach (PhysicalVolumeInfo pvi in physicalVols)
            {
                bool handled = false;
                foreach (LogicalVolumeFactory volFactory in LogicalVolumeFactories)
                {
                    if (volFactory.HandlesPhysicalVolume(pvi))
                    {
                        handled = true;
                        break;
                    }
                }

                if (!handled)
                {
                    unhandledPhysical.Add(pvi);
                }
            }

            MapPhysicalVolumes(unhandledPhysical, result);

            foreach (LogicalVolumeFactory volFactory in LogicalVolumeFactories)
            {
                volFactory.MapDisks(_disks, result);
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

                if (PartitionTable.IsPartitioned(disk.Content))
                {
                    foreach (PartitionTable table in PartitionTable.GetPartitionTables(disk))
                    {
                        foreach (PartitionInfo part in table.Partitions)
                        {
                            PhysicalVolumeInfo pvi = new PhysicalVolumeInfo(diskId, disk, part);
                            result.Add(pvi.Identity, pvi);
                        }
                    }
                }
                else
                {
                    PhysicalVolumeInfo pvi = new PhysicalVolumeInfo(diskId, disk);
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
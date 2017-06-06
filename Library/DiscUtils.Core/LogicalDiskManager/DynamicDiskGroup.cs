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
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils.LogicalDiskManager
{
    internal class DynamicDiskGroup : IDiagnosticTraceable
    {
        private readonly Database _database;
        private readonly Dictionary<Guid, DynamicDisk> _disks;
        private readonly DiskGroupRecord _record;

        internal DynamicDiskGroup(VirtualDisk disk)
        {
            _disks = new Dictionary<Guid, DynamicDisk>();

            DynamicDisk dynDisk = new DynamicDisk(disk);
            _database = dynDisk.Database;
            _disks.Add(dynDisk.Id, dynDisk);
            _record = dynDisk.Database.GetDiskGroup(dynDisk.GroupId);
        }

        #region IDiagnosticTraceable Members

        public void Dump(TextWriter writer, string linePrefix)
        {
            writer.WriteLine(linePrefix + "DISK GROUP (" + _record.Name + ")");
            writer.WriteLine(linePrefix + "  Name: " + _record.Name);
            writer.WriteLine(linePrefix + "  Flags: 0x" +
                             (_record.Flags & 0xFFF0).ToString("X4", CultureInfo.InvariantCulture));
            writer.WriteLine(linePrefix + "  Database Id: " + _record.Id);
            writer.WriteLine(linePrefix + "  Guid: " + _record.GroupGuidString);
            writer.WriteLine();

            writer.WriteLine(linePrefix + "  DISKS");
            foreach (DiskRecord disk in _database.Disks)
            {
                writer.WriteLine(linePrefix + "    DISK (" + disk.Name + ")");
                writer.WriteLine(linePrefix + "      Name: " + disk.Name);
                writer.WriteLine(linePrefix + "      Flags: 0x" +
                                 (disk.Flags & 0xFFF0).ToString("X4", CultureInfo.InvariantCulture));
                writer.WriteLine(linePrefix + "      Database Id: " + disk.Id);
                writer.WriteLine(linePrefix + "      Guid: " + disk.DiskGuidString);

                DynamicDisk dynDisk;
                if (_disks.TryGetValue(new Guid(disk.DiskGuidString), out dynDisk))
                {
                    writer.WriteLine(linePrefix + "      PRIVATE HEADER");
                    dynDisk.Dump(writer, linePrefix + "        ");
                }
            }

            writer.WriteLine(linePrefix + "  VOLUMES");
            foreach (VolumeRecord vol in _database.Volumes)
            {
                writer.WriteLine(linePrefix + "    VOLUME (" + vol.Name + ")");
                writer.WriteLine(linePrefix + "      Name: " + vol.Name);
                writer.WriteLine(linePrefix + "      BIOS Type: " +
                                 vol.BiosType.ToString("X2", CultureInfo.InvariantCulture) + " [" +
                                 BiosPartitionTypes.ToString(vol.BiosType) + "]");
                writer.WriteLine(linePrefix + "      Flags: 0x" +
                                 (vol.Flags & 0xFFF0).ToString("X4", CultureInfo.InvariantCulture));
                writer.WriteLine(linePrefix + "      Database Id: " + vol.Id);
                writer.WriteLine(linePrefix + "      Guid: " + vol.VolumeGuid);
                writer.WriteLine(linePrefix + "      State: " + vol.ActiveString);
                writer.WriteLine(linePrefix + "      Drive Hint: " + vol.MountHint);
                writer.WriteLine(linePrefix + "      Num Components: " + vol.ComponentCount);
                writer.WriteLine(linePrefix + "      Link Id: " + vol.PartitionComponentLink);

                writer.WriteLine(linePrefix + "      COMPONENTS");
                foreach (ComponentRecord cmpnt in _database.GetVolumeComponents(vol.Id))
                {
                    writer.WriteLine(linePrefix + "        COMPONENT (" + cmpnt.Name + ")");
                    writer.WriteLine(linePrefix + "          Name: " + cmpnt.Name);
                    writer.WriteLine(linePrefix + "          Flags: 0x" +
                                     (cmpnt.Flags & 0xFFF0).ToString("X4", CultureInfo.InvariantCulture));
                    writer.WriteLine(linePrefix + "          Database Id: " + cmpnt.Id);
                    writer.WriteLine(linePrefix + "          State: " + cmpnt.StatusString);
                    writer.WriteLine(linePrefix + "          Mode: " + cmpnt.MergeType);
                    writer.WriteLine(linePrefix + "          Num Extents: " + cmpnt.NumExtents);
                    writer.WriteLine(linePrefix + "          Link Id: " + cmpnt.LinkId);
                    writer.WriteLine(linePrefix + "          Stripe Size: " + cmpnt.StripeSizeSectors + " (Sectors)");
                    writer.WriteLine(linePrefix + "          Stripe Stride: " + cmpnt.StripeStride);

                    writer.WriteLine(linePrefix + "          EXTENTS");
                    foreach (ExtentRecord extent in _database.GetComponentExtents(cmpnt.Id))
                    {
                        writer.WriteLine(linePrefix + "            EXTENT (" + extent.Name + ")");
                        writer.WriteLine(linePrefix + "              Name: " + extent.Name);
                        writer.WriteLine(linePrefix + "              Flags: 0x" +
                                         (extent.Flags & 0xFFF0).ToString("X4", CultureInfo.InvariantCulture));
                        writer.WriteLine(linePrefix + "              Database Id: " + extent.Id);
                        writer.WriteLine(linePrefix + "              Disk Offset: " + extent.DiskOffsetLba +
                                         " (Sectors)");
                        writer.WriteLine(linePrefix + "              Volume Offset: " + extent.OffsetInVolumeLba +
                                         " (Sectors)");
                        writer.WriteLine(linePrefix + "              Size: " + extent.SizeLba + " (Sectors)");
                        writer.WriteLine(linePrefix + "              Component Id: " + extent.ComponentId);
                        writer.WriteLine(linePrefix + "              Disk Id: " + extent.DiskId);
                        writer.WriteLine(linePrefix + "              Link Id: " + extent.PartitionComponentLink);
                        writer.WriteLine(linePrefix + "              Interleave Order: " + extent.InterleaveOrder);
                    }
                }
            }
        }

        #endregion

        public void Add(VirtualDisk disk)
        {
            DynamicDisk dynDisk = new DynamicDisk(disk);
            _disks.Add(dynDisk.Id, dynDisk);
        }

        internal DynamicVolume[] GetVolumes()
        {
            List<DynamicVolume> vols = new List<DynamicVolume>();
            foreach (VolumeRecord record in _database.GetVolumes())
            {
                vols.Add(new DynamicVolume(this, record.VolumeGuid));
            }

            return vols.ToArray();
        }

        internal VolumeRecord GetVolume(Guid volume)
        {
            return _database.GetVolume(volume);
        }

        internal LogicalVolumeStatus GetVolumeStatus(ulong volumeId)
        {
            return GetVolumeStatus(_database.GetVolume(volumeId));
        }

        internal SparseStream OpenVolume(ulong volumeId)
        {
            return OpenVolume(_database.GetVolume(volumeId));
        }

        private static int CompareExtentOffsets(ExtentRecord x, ExtentRecord y)
        {
            if (x.OffsetInVolumeLba > y.OffsetInVolumeLba)
            {
                return 1;
            }
            if (x.OffsetInVolumeLba < y.OffsetInVolumeLba)
            {
                return -1;
            }

            return 0;
        }

        private static int CompareExtentInterleaveOrder(ExtentRecord x, ExtentRecord y)
        {
            if (x.InterleaveOrder > y.InterleaveOrder)
            {
                return 1;
            }
            if (x.InterleaveOrder < y.InterleaveOrder)
            {
                return -1;
            }

            return 0;
        }

        private static LogicalVolumeStatus WorstOf(LogicalVolumeStatus x, LogicalVolumeStatus y)
        {
            return (LogicalVolumeStatus)Math.Max((int)x, (int)y);
        }

        private LogicalVolumeStatus GetVolumeStatus(VolumeRecord volume)
        {
            int numFailed = 0;
            ulong numOK = 0;
            LogicalVolumeStatus worst = LogicalVolumeStatus.Healthy;
            foreach (ComponentRecord cmpnt in _database.GetVolumeComponents(volume.Id))
            {
                LogicalVolumeStatus cmpntStatus = GetComponentStatus(cmpnt);
                worst = WorstOf(worst, cmpntStatus);
                if (cmpntStatus == LogicalVolumeStatus.Failed)
                {
                    numFailed++;
                }
                else
                {
                    numOK++;
                }
            }

            if (numOK < 1)
            {
                return LogicalVolumeStatus.Failed;
            }
            if (numOK == volume.ComponentCount)
            {
                return worst;
            }
            return LogicalVolumeStatus.FailedRedundancy;
        }

        private LogicalVolumeStatus GetComponentStatus(ComponentRecord cmpnt)
        {
            // NOTE: no support for RAID, so either valid or failed...
            LogicalVolumeStatus status = LogicalVolumeStatus.Healthy;

            foreach (ExtentRecord extent in _database.GetComponentExtents(cmpnt.Id))
            {
                DiskRecord disk = _database.GetDisk(extent.DiskId);
                if (!_disks.ContainsKey(new Guid(disk.DiskGuidString)))
                {
                    status = LogicalVolumeStatus.Failed;
                    break;
                }
            }

            return status;
        }

        private SparseStream OpenExtent(ExtentRecord extent)
        {
            DiskRecord disk = _database.GetDisk(extent.DiskId);

            DynamicDisk diskObj = _disks[new Guid(disk.DiskGuidString)];

            return new SubStream(diskObj.Content, Ownership.None,
                (diskObj.DataOffset + extent.DiskOffsetLba) * Sizes.Sector, extent.SizeLba * Sizes.Sector);
        }

        private SparseStream OpenComponent(ComponentRecord component)
        {
            if (component.MergeType == ExtentMergeType.Concatenated)
            {
                List<ExtentRecord> extents = new List<ExtentRecord>(_database.GetComponentExtents(component.Id));
                extents.Sort(CompareExtentOffsets);

                // Sanity Check...
                long pos = 0;
                foreach (ExtentRecord extent in extents)
                {
                    if (extent.OffsetInVolumeLba != pos)
                    {
                        throw new IOException("Volume extents are non-contiguous");
                    }

                    pos += extent.SizeLba;
                }

                List<SparseStream> streams = new List<SparseStream>();
                foreach (ExtentRecord extent in extents)
                {
                    streams.Add(OpenExtent(extent));
                }

                return new ConcatStream(Ownership.Dispose, streams.ToArray());
            }
            if (component.MergeType == ExtentMergeType.Interleaved)
            {
                List<ExtentRecord> extents = new List<ExtentRecord>(_database.GetComponentExtents(component.Id));
                extents.Sort(CompareExtentInterleaveOrder);

                List<SparseStream> streams = new List<SparseStream>();
                foreach (ExtentRecord extent in extents)
                {
                    streams.Add(OpenExtent(extent));
                }

                return new StripedStream(component.StripeSizeSectors * Sizes.Sector, Ownership.Dispose, streams.ToArray());
            }
            throw new NotImplementedException("Unknown component mode: " + component.MergeType);
        }

        private SparseStream OpenVolume(VolumeRecord volume)
        {
            List<SparseStream> cmpntStreams = new List<SparseStream>();
            foreach (ComponentRecord component in _database.GetVolumeComponents(volume.Id))
            {
                if (GetComponentStatus(component) == LogicalVolumeStatus.Healthy)
                {
                    cmpntStreams.Add(OpenComponent(component));
                }
            }

            if (cmpntStreams.Count < 1)
            {
                throw new IOException("Volume with no associated or healthy components");
            }
            if (cmpntStreams.Count == 1)
            {
                return cmpntStreams[0];
            }
            return new MirrorStream(Ownership.Dispose, cmpntStreams.ToArray());
        }
    }
}
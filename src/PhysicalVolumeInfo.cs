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
using System.Globalization;
using DiscUtils.Partitions;

namespace DiscUtils
{
    /// <summary>
    /// Enumeration of possible types of physical volume.
    /// </summary>
    public enum PhysicalVolumeType
    {
        /// <summary>
        /// Unknown type.
        /// </summary>
        None,

        /// <summary>
        /// Physical volume encompasses the entire disk.
        /// </summary>
        EntireDisk,

        /// <summary>
        /// Physical volume is defined by a BIOS-style partition table.
        /// </summary>
        BiosPartition,

        /// <summary>
        /// Physical volume is defined by a GUID partition table.
        /// </summary>
        GptPartition
    }

    /// <summary>
    /// Information about a physical disk volume, which may be a partition or an entire disk.
    /// </summary>
    public sealed class PhysicalVolumeInfo : VolumeInfo
    {
        private string _diskId;
        private VirtualDisk _disk;
        private SparseStreamOpenDelegate _streamOpener;
        private PhysicalVolumeType _type;
        private PartitionInfo _partitionInfo;

        /// <summary>
        /// Creates an instance representing a (BIOS or GPT) partition.
        /// </summary>
        /// <param name="diskId">The containing disk's identity</param>
        /// <param name="disk">The disk containing the partition</param>
        /// <param name="partitionInfo">Information about the partition</param>
        internal PhysicalVolumeInfo(
            string diskId,
            VirtualDisk disk,
            PartitionInfo partitionInfo
            )
        {
            _diskId = diskId;
            _disk = disk;
            _streamOpener = partitionInfo.Open;
            _type = (partitionInfo is GuidPartitionInfo) ? PhysicalVolumeType.GptPartition : PhysicalVolumeType.BiosPartition;
            _partitionInfo = partitionInfo;
        }

        /// <summary>
        /// Creates an instance representing an entire disk as a single volume.
        /// </summary>
        /// <param name="diskId">The identity of the disk</param>
        /// <param name="disk">The disk itself</param>
        internal PhysicalVolumeInfo(
            string diskId,
            VirtualDisk disk
            )
        {
            _diskId = diskId;
            _disk = disk;
            _streamOpener = delegate() { return new SubStream(disk.Content, Ownership.None, 0, disk.Capacity); };
            _type = PhysicalVolumeType.EntireDisk;
        }

        /// <summary>
        /// The type of the volume.
        /// </summary>
        public PhysicalVolumeType VolumeType
        {
            get { return _type; }
        }

        /// <summary>
        /// The signature of the disk containing the volume (only valid for partition-type volumes).
        /// </summary>
        public int DiskSignature
        {
            get { return (_type != PhysicalVolumeType.EntireDisk) ? _disk.Signature : 0; }
        }

        /// <summary>
        /// The unique identity of the disk containing the volume, if known.
        /// </summary>
        public Guid DiskIdentity
        {
            get { return (_type != PhysicalVolumeType.EntireDisk) ? _disk.Partitions.DiskGuid : Guid.Empty; }
        }

        /// <summary>
        /// Opens the volume, providing access to its contents.
        /// </summary>
        /// <returns>A stream that can be used to access the volume.</returns>
        public override SparseStream Open()
        {
            return _streamOpener();
        }

        /// <summary>
        /// Gets the one-byte BIOS type for this volume, which indicates the content.
        /// </summary>
        public override byte BiosType
        {
            get { return (_partitionInfo == null) ? (byte)0 : _partitionInfo.BiosType; }
        }

        /// <summary>
        /// The size of the volume, in bytes.
        /// </summary>
        public override long Length
        {
            get { return (_partitionInfo == null) ? _disk.Capacity : _partitionInfo.SectorCount * Sizes.Sector; }
        }

        /// <summary>
        /// The stable identity for this physical volume.
        /// </summary>
        /// <remarks>The stability of the identity depends the disk structure.
        /// In some cases the identity may include a simple index, when no other information
        /// is available.  Best practice is to add disks to the Volume Manager in a stable 
        /// order, if the stability of this identity is paramount.</remarks>
        public override string Identity
        {
            get
            {
                if (_type == PhysicalVolumeType.GptPartition)
                {
                    return "VPG" + PartitionIdentity.ToString("B");
                }
                else
                {
                    string partId;
                    switch (_type)
                    {
                        case PhysicalVolumeType.EntireDisk:
                            partId = "PD";
                            break;
                        case PhysicalVolumeType.BiosPartition:
                            partId = "PO" + (_partitionInfo.FirstSector * Sizes.Sector).ToString("X", CultureInfo.InvariantCulture);
                            break;
                        default:
                            partId = "P*";
                            break;
                    }

                    return "VPD:" + _diskId + ":" + partId;
                }
            }
        }

        /// <summary>
        /// Gets the disk geometry of the underlying storage medium, if any (may be null).
        /// </summary>
        public override Geometry PhysicalGeometry
        {
            get { return _disk.Geometry; }
        }

        /// <summary>
        /// Gets the disk geometry of the underlying storage medium (as used in BIOS calls), may be null.
        /// </summary>
        public override Geometry BiosGeometry
        {
            get { return _disk.BiosGeometry; }
        }

        /// <summary>
        /// Gets the offset of this volume in the underlying storage medium, if any (may be Zero).
        /// </summary>
        public override long PhysicalStartSector
        {
            get { return _type == PhysicalVolumeType.EntireDisk ? 0 : _partitionInfo.FirstSector; }
        }

        /// <summary>
        /// The unique identity of the physical partition, if known.
        /// </summary>
        public Guid PartitionIdentity
        {
            get
            {
                GuidPartitionInfo gpi = _partitionInfo as GuidPartitionInfo;
                if (gpi != null)
                {
                    return gpi.Identity;
                }
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Gets the underlying partition (if any).
        /// </summary>
        internal PartitionInfo Partition
        {
            get { return _partitionInfo; }
        }
    }
}

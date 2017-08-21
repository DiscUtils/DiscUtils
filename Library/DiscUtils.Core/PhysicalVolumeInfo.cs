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
using System.Globalization;
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils
{
    /// <summary>
    /// Information about a physical disk volume, which may be a partition or an entire disk.
    /// </summary>
    public sealed class PhysicalVolumeInfo : VolumeInfo
    {
        private readonly VirtualDisk _disk;
        private readonly string _diskId;
        private readonly SparseStreamOpenDelegate _streamOpener;

        /// <summary>
        /// Initializes a new instance of the PhysicalVolumeInfo class.
        /// </summary>
        /// <param name="diskId">The containing disk's identity.</param>
        /// <param name="disk">The disk containing the partition.</param>
        /// <param name="partitionInfo">Information about the partition.</param>
        /// <remarks>Use this constructor to represent a (BIOS or GPT) partition.</remarks>
        internal PhysicalVolumeInfo(
            string diskId,
            VirtualDisk disk,
            PartitionInfo partitionInfo)
        {
            _diskId = diskId;
            _disk = disk;
            _streamOpener = partitionInfo.Open;
            VolumeType = partitionInfo.VolumeType;
            Partition = partitionInfo;
        }

        /// <summary>
        /// Initializes a new instance of the PhysicalVolumeInfo class.
        /// </summary>
        /// <param name="diskId">The identity of the disk.</param>
        /// <param name="disk">The disk itself.</param>
        /// <remarks>Use this constructor to represent an entire disk as a single volume.</remarks>
        internal PhysicalVolumeInfo(
            string diskId,
            VirtualDisk disk)
        {
            _diskId = diskId;
            _disk = disk;
            _streamOpener = delegate { return new SubStream(disk.Content, Ownership.None, 0, disk.Capacity); };
            VolumeType = PhysicalVolumeType.EntireDisk;
        }

        /// <summary>
        /// Gets the disk geometry of the underlying storage medium (as used in BIOS calls), may be null.
        /// </summary>
        public override Geometry BiosGeometry
        {
            get { return _disk.BiosGeometry; }
        }

        /// <summary>
        /// Gets the one-byte BIOS type for this volume, which indicates the content.
        /// </summary>
        public override byte BiosType
        {
            get { return Partition == null ? (byte)0 : Partition.BiosType; }
        }

        /// <summary>
        /// Gets the unique identity of the disk containing the volume, if known.
        /// </summary>
        public Guid DiskIdentity
        {
            get { return VolumeType != PhysicalVolumeType.EntireDisk ? _disk.Partitions.DiskGuid : Guid.Empty; }
        }

        /// <summary>
        /// Gets the signature of the disk containing the volume (only valid for partition-type volumes).
        /// </summary>
        public int DiskSignature
        {
            get { return VolumeType != PhysicalVolumeType.EntireDisk ? _disk.Signature : 0; }
        }

        /// <summary>
        /// Gets the stable identity for this physical volume.
        /// </summary>
        /// <remarks>The stability of the identity depends the disk structure.
        /// In some cases the identity may include a simple index, when no other information
        /// is available.  Best practice is to add disks to the Volume Manager in a stable 
        /// order, if the stability of this identity is paramount.</remarks>
        public override string Identity
        {
            get
            {
                if (VolumeType == PhysicalVolumeType.GptPartition)
                {
                    return "VPG" + PartitionIdentity.ToString("B");
                }
                string partId;
                switch (VolumeType)
                {
                    case PhysicalVolumeType.EntireDisk:
                        partId = "PD";
                        break;
                    case PhysicalVolumeType.BiosPartition:
                    case PhysicalVolumeType.ApplePartition:
                        partId = "PO" +
                                 (Partition.FirstSector * _disk.SectorSize).ToString("X",
                                     CultureInfo.InvariantCulture);
                        break;
                    default:
                        partId = "P*";
                        break;
                }

                return "VPD:" + _diskId + ":" + partId;
            }
        }

        /// <summary>
        /// Gets the size of the volume, in bytes.
        /// </summary>
        public override long Length
        {
            get { return Partition == null ? _disk.Capacity : Partition.SectorCount * _disk.SectorSize; }
        }

        /// <summary>
        /// Gets the underlying partition (if any).
        /// </summary>
        public PartitionInfo Partition { get; }

        /// <summary>
        /// Gets the unique identity of the physical partition, if known.
        /// </summary>
        public Guid PartitionIdentity
        {
            get
            {
                GuidPartitionInfo gpi = Partition as GuidPartitionInfo;
                if (gpi != null)
                {
                    return gpi.Identity;
                }

                return Guid.Empty;
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
        /// Gets the offset of this volume in the underlying storage medium, if any (may be Zero).
        /// </summary>
        public override long PhysicalStartSector
        {
            get { return VolumeType == PhysicalVolumeType.EntireDisk ? 0 : Partition.FirstSector; }
        }

        /// <summary>
        /// Gets the type of the volume.
        /// </summary>
        public PhysicalVolumeType VolumeType { get; }

        /// <summary>
        /// Opens the volume, providing access to its contents.
        /// </summary>
        /// <returns>A stream that can be used to access the volume.</returns>
        public override SparseStream Open()
        {
            return _streamOpener();
        }
    }
}
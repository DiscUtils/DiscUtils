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
using System.Globalization;

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
        private SparseStreamOpenDelegate _streamOpener;
        private string _diskId;
        private int _diskSignature;
        private Guid _diskGuid;
        private PhysicalVolumeType _type;
        private Guid _partitionGuid;
        private long _diskOffset;
        private long _length;

        internal PhysicalVolumeInfo(
            SparseStreamOpenDelegate opener,
            string diskId,
            int diskSignature,
            Guid diskGuid,
            PhysicalVolumeType type,
            Guid partitionGuid,
            long diskOffset,
            long length)
        {
            _streamOpener = opener;
            _diskId = diskId;
            _diskSignature = diskSignature;
            _diskGuid = diskGuid;
            _type = type;
            _partitionGuid = partitionGuid;
            _diskOffset = diskOffset;
            _length = length;
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
            get { return _diskSignature; }
        }

        /// <summary>
        /// The unique identity of the disk containing the volume, if known.
        /// </summary>
        public Guid DiskIdentity
        {
            get { return _diskGuid; }
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
        /// The size of the volume, in bytes.
        /// </summary>
        public override long Length
        {
            get { return _length; }
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
                    return "VPG" + _partitionGuid.ToString("B");
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
                            partId = "PO" + _diskOffset.ToString("X", CultureInfo.InvariantCulture);
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
        /// The unique identity of the physical partition, if known.
        /// </summary>
        public Guid PartitionIdentity
        {
            get { return _partitionGuid; }
        }
    }
}

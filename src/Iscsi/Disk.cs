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
using System.IO;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Represents a disk accessed via iSCSI.
    /// </summary>
    public class Disk : VirtualDisk
    {
        private Session _session;
        private long _lun;
        private FileAccess _access;
        private LunCapacity _capacity;

        private DiskStream _stream;

        internal Disk(Session session, long lun, FileAccess access)
        {
            _session = session;
            _lun = lun;
            _access = access;
        }

        /// <summary>
        /// The Geometry of the disk.
        /// </summary>
        public override Geometry Geometry
        {
            get
            {
                // We detect the geometry (which will return a sensible default if the disk has no partitions).
                // We don't rely on asking the iSCSI target for the geometry because frequently values are returned
                // that are not valid as BIOS disk geometries.
                Stream stream = Content;
                long pos = stream.Position;

                Geometry result = DiscUtils.Partitions.BiosPartitionTable.DetectGeometry(stream);

                stream.Position = pos;

                return result;
            }
        }

        /// <summary>
        /// The capacity of the disk.
        /// </summary>
        public override long Capacity
        {
            get
            {
                if (_capacity == null)
                {
                    _capacity = _session.GetCapacity(_lun);
                }
                return _capacity.BlockSize * _capacity.LogicalBlockCount;
            }
        }

        /// <summary>
        /// Gets the size of the disk's logical blocks (in bytes).
        /// </summary>
        public override int BlockSize
        {
            get
            {
                if (_capacity == null)
                {
                    _capacity = _session.GetCapacity(_lun);
                }
                return _capacity.BlockSize;
            }
        }

        /// <summary>
        /// Gets a stream that provides access to the disk's content.
        /// </summary>
        public override SparseStream Content
        {
            get
            {
                if (_stream == null)
                {
                    _stream = new DiskStream(_session, _lun, _access);
                }
                return _stream;
            }
        }

        /// <summary>
        /// Gets the disk layers that constitute the disk.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { yield break; }
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            throw new NotSupportedException("Differencing disks not supported for iSCSI disks");
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            throw new NotSupportedException("Differencing disks not supported for iSCSI disks");
        }
    }
}

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
using System.IO;
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils.Dmg
{
    /// <summary>
    /// Represents a DMG (aka UDIF) backed disk.
    /// </summary>
    public class Disk : VirtualDisk
    {
        private SparseStream _content;
        private DiskImageFile _file;

        /// <summary>
        /// Initializes a new instance of the Disk class.
        /// </summary>
        /// <param name="stream">The stream containing the disk.</param>
        /// <param name="ownsStream">Whether the new instance takes ownership of stream.</param>
        public Disk(Stream stream, Ownership ownsStream)
        {
            _file = new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Gets the capacity of the disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _file.Capacity; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        /// <remarks>
        /// Note the returned stream is not guaranteed to be at any particular position.  The actual position
        /// will depend on the last partition table/file system activity, since all access to the disk contents pass
        /// through a single stream instance.  Set the stream position before accessing the stream.
        /// </remarks>
        public override SparseStream Content
        {
            get
            {
                if (_content == null)
                {
                    _content = _file.OpenContent(null, Ownership.None);
                }

                return _content;
            }
        }

        /// <summary>
        /// Gets the type of disk represented by this object.
        /// </summary>
        public override VirtualDiskClass DiskClass
        {
            get { return VirtualDiskClass.HardDisk; }
        }

        /// <summary>
        /// Gets information about the type of disk.
        /// </summary>
        /// <remarks>This property provides access to meta-data about the disk format, for example whether the
        /// BIOS geometry is preserved in the disk file.</remarks>
        public override VirtualDiskTypeInfo DiskTypeInfo
        {
            get { return DiskFactory.MakeDiskTypeInfo(); }
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _file.Geometry; }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { return new VirtualDiskLayer[] { _file }; }
        }

        public override PartitionTable Partitions
        {
            get { return new UdifPartitionTable(this, _file.Buffer); }
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on.</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Disposes of this instance, freeing underlying resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> if called from Dispose.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_content != null)
                    {
                        _content.Dispose();
                        _content = null;
                    }

                    if (_file != null)
                    {
                        _file.Dispose();
                        _file = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
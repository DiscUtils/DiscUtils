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
using DiscUtils.Streams;

namespace DiscUtils.Xva
{
    /// <summary>
    /// Class representing a disk containing within an XVA file.
    /// </summary>
    public sealed class Disk : VirtualDisk
    {
        private readonly long _capacity;
        private readonly string _location;
        private readonly VirtualMachine _vm;

        private SparseStream _content;

        internal Disk(VirtualMachine vm, string id, string displayname, string location, long capacity)
        {
            _vm = vm;
            Uuid = id;
            DisplayName = displayname;
            _location = location;
            _capacity = capacity;
        }

        /// <summary>
        /// Gets the disk's capacity (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _capacity; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        public override SparseStream Content
        {
            get
            {
                if (_content == null)
                {
                    _content = new DiskStream(_vm.Archive, _capacity, _location);
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
        /// Gets the display name of the disk, as shown by XenServer.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the disk's geometry.
        /// </summary>
        /// <remarks>The geometry is not stored with the disk, so this is at best
        /// a guess of the actual geometry.</remarks>
        public override Geometry Geometry
        {
            get { return Geometry.FromCapacity(_capacity); }
        }

        /// <summary>
        /// Gets the (single) layer of an XVA disk.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { yield return new DiskLayer(_vm, _capacity, _location); }
        }

        /// <summary>
        /// Gets the Unique id of the disk, as known by XenServer.
        /// </summary>
        public string Uuid { get; }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on.</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            throw new NotSupportedException("Differencing disks not supported by XVA format");
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>The newly created disk.</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            throw new NotSupportedException("Differencing disks not supported by XVA format");
        }

        /// <summary>
        /// Disposes of this instance, freeing underlying resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> if running inside Dispose(), indicating
        /// graceful cleanup of all managed objects should be performed, or <c>false</c>
        /// if running inside destructor.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_content != null)
                {
                    _content.Dispose();
                    _content = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
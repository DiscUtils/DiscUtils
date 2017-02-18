//
// Copyright (c) 2008-2011, Kenneth Bell
// Adapted for EWF by Adam Bridge, 2013
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
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Partitions;

namespace DiscUtils.Ewf
{
    /// <summary>
    /// Represents a single EWF file.
    /// </summary>
    public sealed class DiskImageFile : VirtualDiskLayer
    {
        private SparseStream _content;
        private Ownership _ownsContent;
        private Geometry _geometry;        

        /// <summary>
        /// Represents a single EWF file.
        /// </summary>
        /// <param name="path">Path to the ewf file.</param>
        /// <param name="access">Desired access.</param>
        public DiskImageFile(string path, FileAccess access)
        {
            if (_content == null)
            {
                _content = new EWFStream(path);
            }
        }

        /// <summary>
        /// Gets the geometry of the file.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _geometry; }
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the file is a differencing disk. EWFs don't use differencing disks.
        /// </summary>
        public override bool NeedsParent
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the type of disk represented by this object.
        /// </summary>
        public VirtualDiskClass DiskType
        {
            get { return VirtualDiskClass.HardDisk; }
        }

        internal override long Capacity
        {
            get { return _content.Length; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return null; }
        }

        internal SparseStream Content
        {
            get { return _content; }
        }        

        /// <summary>
        /// Gets the content of this layer.
        /// </summary>
        /// <param name="parent">The parent stream (if any)</param>
        /// <param name="ownsParent">Controls ownership of the parent stream</param>
        /// <returns>The content as a stream</returns>
        public override SparseStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            if (ownsParent == Ownership.Dispose && parent != null)
            {
                parent.Dispose();
            }

            return SparseStream.FromStream(Content, Ownership.None);
        }

        /// <summary>
        /// Gets the possible locations of the parent file (if any).
        /// </summary>
        /// <returns>Array of strings, empty if no parent</returns>
        public override string[] GetParentLocations()
        {
            return new string[0];
        }

        /// <summary>
        /// Disposes of underlying resources.
        /// </summary>
        /// <param name="disposing">Set to <c>true</c> if called within Dispose(),
        /// else <c>false</c>.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_ownsContent == Ownership.Dispose && _content != null)
                    {
                        _content.Dispose();
                    }

                    _content = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Calculates the best guess geometry of a disk.
        /// </summary>
        /// <param name="disk">The disk to detect the geometry of</param>
        /// <returns>The geometry of the disk</returns>
        private static Geometry DetectGeometry(Stream disk)
        {
            long capacity = disk.Length;

            // First, check for floppy disk capacities - these have well-defined geometries
            if (capacity == Sizes.Sector * 1440)
            {
                return new Geometry(80, 2, 9);
            }
            else if (capacity == Sizes.Sector * 2880)
            {
                return new Geometry(80, 2, 18);
            }
            else if (capacity == Sizes.Sector * 5760)
            {
                return new Geometry(80, 2, 36);
            }

            // Failing that, try to detect the geometry from any partition table.
            // Note: this call falls back to guessing the geometry from the capacity
            return BiosPartitionTable.DetectGeometry(disk);
        }        
    }
}

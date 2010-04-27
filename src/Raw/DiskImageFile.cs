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
using System.IO;
using DiscUtils.Partitions;

namespace DiscUtils.Raw
{
    /// <summary>
    /// Represents a single raw disk image file.
    /// </summary>
    public sealed class DiskImageFile : VirtualDiskLayer
    {
        private SparseStream _content;
        private Ownership _ownsContent;
        private Geometry _geometry;

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream to interpret</param>
        public DiskImageFile(Stream stream)
            : this(stream, Ownership.None, null)
        {
        }

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream to interpret</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        /// <param name="geometry">The emulated geometry of the disk.</param>
        public DiskImageFile(Stream stream, Ownership ownsStream, Geometry geometry)
        {
            _content = stream as SparseStream;
            _ownsContent = ownsStream;

            if (_content == null)
            {
                _content = SparseStream.FromStream(stream, ownsStream);
                _ownsContent = Ownership.Dispose;
            }

            _geometry = geometry ?? DetectGeometry(_content);
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
        /// Initializes a stream as a raw disk image.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="geometry">The geometry of the new disk</param>
        /// <returns>An object that accesses the stream as a raw disk image</returns>
        public static DiskImageFile Initialize(Stream stream, Ownership ownsStream, long capacity, Geometry geometry)
        {
            stream.SetLength(Utilities.RoundUp(capacity, Sizes.Sector));

            // Wipe any pre-existing master boot record / BPB
            stream.Position = 0;
            stream.Write(new byte[Sizes.Sector], 0, Sizes.Sector);
            stream.Position = 0;

            return new DiskImageFile(stream, ownsStream, geometry);
        }

        /// <summary>
        /// Initializes a stream as an unformatted floppy disk.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="type">The type of floppy disk image to create</param>
        /// <returns>An object that accesses the stream as a disk</returns>
        public static DiskImageFile Initialize(Stream stream, Ownership ownsStream, FloppyDiskType type)
        {
            return Initialize(stream, ownsStream, FloppyCapacity(type), null);
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return false; }
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

        internal Geometry Geometry
        {
            get { return _geometry; }
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

        private static long FloppyCapacity(FloppyDiskType type)
        {
            switch (type)
            {
                case FloppyDiskType.DoubleDensity:
                    return Sizes.Sector * 1440;
                case FloppyDiskType.HighDensity:
                    return Sizes.Sector * 2880;
                case FloppyDiskType.Extended:
                    return Sizes.Sector * 5760;
                default:
                    throw new ArgumentException("Invalid floppy disk type", "type");
            }
        }
    }
}

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
using System.IO;
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils.Raw
{
    /// <summary>
    /// Represents a single raw disk image file.
    /// </summary>
    public sealed class DiskImageFile : VirtualDiskLayer
    {
        private readonly Ownership _ownsContent;

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        public DiskImageFile(Stream stream)
            : this(stream, Ownership.None, null) {}

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        /// <param name="geometry">The emulated geometry of the disk.</param>
        public DiskImageFile(Stream stream, Ownership ownsStream, Geometry geometry)
        {
            Content = stream as SparseStream;
            _ownsContent = ownsStream;

            if (Content == null)
            {
                Content = SparseStream.FromStream(stream, ownsStream);
                _ownsContent = Ownership.Dispose;
            }

            Geometry = geometry ?? DetectGeometry(Content);
        }

        internal override long Capacity
        {
            get { return Content.Length; }
        }

        internal SparseStream Content { get; private set; }

        /// <summary>
        /// Gets the type of disk represented by this object.
        /// </summary>
        public VirtualDiskClass DiskType
        {
            get { return DetectDiskType(Capacity); }
        }

        /// <summary>
        /// Gets the geometry of the file.
        /// </summary>
        public override Geometry Geometry { get; }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the file is a differencing disk.
        /// </summary>
        public override bool NeedsParent
        {
            get { return false; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return null; }
        }

        /// <summary>
        /// Initializes a stream as a raw disk image.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The geometry of the new disk.</param>
        /// <returns>An object that accesses the stream as a raw disk image.</returns>
        public static DiskImageFile Initialize(Stream stream, Ownership ownsStream, long capacity, Geometry geometry)
        {
            stream.SetLength(MathUtilities.RoundUp(capacity, Sizes.Sector));

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
        /// <param name="type">The type of floppy disk image to create.</param>
        /// <returns>An object that accesses the stream as a disk.</returns>
        public static DiskImageFile Initialize(Stream stream, Ownership ownsStream, FloppyDiskType type)
        {
            return Initialize(stream, ownsStream, FloppyCapacity(type), null);
        }

        /// <summary>
        /// Gets the content of this layer.
        /// </summary>
        /// <param name="parent">The parent stream (if any).</param>
        /// <param name="ownsParent">Controls ownership of the parent stream.</param>
        /// <returns>The content as a stream.</returns>
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
        /// <returns>Array of strings, empty if no parent.</returns>
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
                    if (_ownsContent == Ownership.Dispose && Content != null)
                    {
                        Content.Dispose();
                    }

                    Content = null;
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
        /// <param name="disk">The disk to detect the geometry of.</param>
        /// <returns>The geometry of the disk.</returns>
        private static Geometry DetectGeometry(Stream disk)
        {
            long capacity = disk.Length;

            // First, check for floppy disk capacities - these have well-defined geometries
            if (capacity == Sizes.Sector * 1440)
            {
                return new Geometry(80, 2, 9);
            }
            if (capacity == Sizes.Sector * 2880)
            {
                return new Geometry(80, 2, 18);
            }
            if (capacity == Sizes.Sector * 5760)
            {
                return new Geometry(80, 2, 36);
            }

            // Failing that, try to detect the geometry from any partition table.
            // Note: this call falls back to guessing the geometry from the capacity
            return BiosPartitionTable.DetectGeometry(disk);
        }

        /// <summary>
        /// Calculates the best guess disk type (i.e. floppy or hard disk).
        /// </summary>
        /// <param name="capacity">The capacity of the disk.</param>
        /// <returns>The disk type.</returns>
        private static VirtualDiskClass DetectDiskType(long capacity)
        {
            if (capacity == Sizes.Sector * 1440
                || capacity == Sizes.Sector * 2880
                || capacity == Sizes.Sector * 5760)
            {
                return VirtualDiskClass.FloppyDisk;
            }
            return VirtualDiskClass.HardDisk;
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
                    throw new ArgumentException("Invalid floppy disk type", nameof(type));
            }
        }
    }
}
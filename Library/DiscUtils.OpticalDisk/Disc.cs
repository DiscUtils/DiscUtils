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
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.OpticalDisk
{
    /// <summary>
    /// Represents an optical disc image.
    /// </summary>
    public sealed class Disc : VirtualDisk
    {
        private DiscImageFile _file;

        /// <summary>
        /// Initializes a new instance of the Disc class.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        public Disc(Stream stream, Ownership ownsStream)
            : this(stream, ownsStream, OpticalFormat.None) {}

        /// <summary>
        /// Initializes a new instance of the Disc class.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        /// <param name="format">The disk image format.</param>
        public Disc(Stream stream, Ownership ownsStream, OpticalFormat format)
        {
            _file = new DiscImageFile(stream, ownsStream, format);
        }

        /// <summary>
        /// Initializes a new instance of the Disc class.
        /// </summary>
        /// <param name="path">The path to the disc image.</param>
        public Disc(string path)
            :this(path, FileAccess.ReadWrite) {}

        /// <summary>
        /// Initializes a new instance of the Disc class.
        /// </summary>
        /// <param name="path">The path to the disc image.</param>
        /// <param name="access">The access requested to the disk.</param>
        public Disc(string path, FileAccess access)
        {
            FileShare share = access == FileAccess.Read ? FileShare.Read : FileShare.None;
            var locator = new LocalFileLocator(string.Empty);
            _file = new DiscImageFile(locator.Open(path, FileMode.Open, access, share), Ownership.Dispose,
                OpticalFormat.None);
        }

        /// <summary>
        /// Gets the sector size of the disk (2048 for optical discs).
        /// </summary>
        public override int BlockSize
        {
            get { return DiscImageFile.Mode1SectorSize; }
        }

        /// <summary>
        /// Gets the capacity of the disc (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _file.Capacity; }
        }

        /// <summary>
        /// Gets the content of the disc as a stream.
        /// </summary>
        /// <remarks>Note the returned stream is not guaranteed to be at any particular position.  The actual position
        /// will depend on the last partition table/file system activity, since all access to the disk contents pass
        /// through a single stream instance.  Set the stream position before accessing the stream.</remarks>
        public override SparseStream Content
        {
            get { return _file.Content; }
        }

        /// <summary>
        /// Gets the type of disk represented by this object.
        /// </summary>
        public override VirtualDiskClass DiskClass
        {
            get { return VirtualDiskClass.OpticalDisk; }
        }

        /// <summary>
        /// Gets information about the type of disk.
        /// </summary>
        /// <remarks>This property provides access to meta-data about the disk format, for example whether the
        /// BIOS geometry is preserved in the disk file.</remarks>
        public override VirtualDiskTypeInfo DiskTypeInfo
        {
            get { return DiscFactory.MakeDiskTypeInfo(); }
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _file.Geometry; }
        }

        /// <summary>
        /// Gets the layers that make up the disc.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { yield return _file; }
        }

        /// <summary>
        /// Not supported for Optical Discs.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disc on.</param>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>Not Applicable.</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            throw new NotSupportedException("Differencing disks not supported for optical disks");
        }

        /// <summary>
        /// Not supported for Optical Discs.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create.</param>
        /// <returns>Not Applicable.</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            throw new NotSupportedException("Differencing disks not supported for optical disks");
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
                    if (_file != null)
                    {
                        _file.Dispose();
                    }

                    _file = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
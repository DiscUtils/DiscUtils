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

namespace DiscUtils.Vdi
{
    /// <summary>
    /// Represents a disk stored in VirtualBox (Sun xVM) format.
    /// </summary>
    public sealed class Disk : VirtualDisk
    {
        private DiskImageFile _diskImage;

        private DiskStream _content;

        /// <summary>
        /// Creates a new instance from a file on disk.
        /// </summary>
        /// <param name="path">The path to the disk</param>
        /// <param name="access">The access requested to the disk</param>
        public Disk(string path, FileAccess access)
            : this(new FileStream(path, FileMode.Open, access), Ownership.Dispose)
        {
        }

        /// <summary>
        /// Creates a new instance from an existing disk file.
        /// </summary>
        /// <param name="file">The file containing the disk image.</param>
        public Disk(DiskImageFile file)
        {
            _diskImage = file;
        }

        /// <summary>
        /// Creates a new instance from an existing stream, differencing disks not supported.
        /// </summary>
        /// <param name="stream">The stream to read</param>
        public Disk(Stream stream)
        {
            _diskImage = new DiskImageFile(stream);
        }

        /// <summary>
        /// Creates a new instance from an existing stream, differencing disks not supported.
        /// </summary>
        /// <param name="stream">The stream to read</param>
        /// <param name="ownsStream">Indicates if the new disk should take ownership of <paramref name="stream"/> lifetime.</param>
        public Disk(Stream stream, Ownership ownsStream)
        {
            _diskImage = new DiskImageFile(stream, ownsStream);
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
                    if (_content != null)
                    {
                        _content.Dispose();
                        _content = null;
                    }

                    if (_diskImage != null)
                    {
                        _diskImage.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VDI file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VDI file</returns>
        public static Disk InitializeFixed(Stream stream, Ownership ownsStream, long capacity)
        {
            return new Disk(DiskImageFile.InitializeFixed(stream, ownsStream, capacity));
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VDI file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VDI file</returns>
        public static Disk InitializeDynamic(Stream stream, Ownership ownsStream, long capacity)
        {
            return new Disk(DiskImageFile.InitializeDynamic(stream, ownsStream, capacity));
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _diskImage.Geometry; }
        }

        /// <summary>
        /// Gets the capacity of the disk (in bytes).
        /// </summary>
        public override long Capacity
        {
            get { return _diskImage.Capacity; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        /// <remarks>Note the returned stream is not guaranteed to be at any particular position.  The actual position
        /// will depend on the last partition table/file system activity, since all access to the disk contents pass
        /// through a single stream instance.  Set the stream position before accessing the stream.</remarks>
        public override SparseStream Content
        {
            get
            {
                if (_content == null)
                {
                    _content = _diskImage.OpenContent(null, Ownership.None);
                }
                return _content;
            }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override IEnumerable<VirtualDiskLayer> Layers
        {
            get { yield return _diskImage; }
        }

        /// <summary>
        /// Create a new differencing disk, possibly within an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on</param>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(DiscFileSystem fileSystem, string path)
        {
            throw new NotImplementedException("Differencing disks not implemented for the VDI format");
        }

        /// <summary>
        /// Create a new differencing disk.
        /// </summary>
        /// <param name="path">The path (or URI) for the disk to create</param>
        /// <returns>The newly created disk</returns>
        public override VirtualDisk CreateDifferencingDisk(string path)
        {
            throw new NotImplementedException("Differencing disks not implemented for the VDI format");
        }
    }
}

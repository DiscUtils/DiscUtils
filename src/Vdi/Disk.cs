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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Vdi
{
    /// <summary>
    /// Represents a disk stored in VirtualBox (Sun xVM) format.
    /// </summary>
    public class Disk : VirtualDisk
    {
        private DiskImageFile _diskImage;

        private DiskStream _content;

        /// <summary>
        /// Creates a new instance from an existing disk file.
        /// </summary>
        /// <param name="file"></param>
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
        public Disk(Stream stream, bool ownsStream)
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
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VDI file</returns>
        public static Disk InitializeFixed(Stream stream, long capacity)
        {
            return InitializeFixed(stream, false, capacity);
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VDI file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VDI file</returns>
        public static Disk InitializeFixed(Stream stream, bool ownsStream, long capacity)
        {
            return new Disk(DiskImageFile.InitializeFixed(stream, ownsStream, capacity));
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VDI file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VDI file</returns>
        public static Disk InitializeDynamic(Stream stream, long capacity)
        {
            return InitializeDynamic(stream, false, capacity);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VDI file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VDI file</returns>
        public static Disk InitializeDynamic(Stream stream, bool ownsStream, long capacity)
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
        public override SparseStream Content
        {
            get
            {
                if (_content == null)
                {
                    _content = _diskImage.OpenContent(null, false);
                }
                return _content;
            }
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override ReadOnlyCollection<VirtualDiskLayer> Layers
        {
            get
            {
                List<VirtualDiskLayer> layers = new List<VirtualDiskLayer>();
                layers.Add(_diskImage);
                return new ReadOnlyCollection<VirtualDiskLayer>(layers);
            }
        }
    }
}

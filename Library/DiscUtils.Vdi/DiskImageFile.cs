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
using DiscUtils.Streams;

namespace DiscUtils.Vdi
{
    /// <summary>
    /// Represents a single VirtualBox disk (.vdi file).
    /// </summary>
    public sealed class DiskImageFile : VirtualDiskLayer
    {
        private HeaderRecord _header;

        /// <summary>
        /// Indicates if this object controls the lifetime of the stream.
        /// </summary>
        private readonly Ownership _ownsStream;

        private PreHeaderRecord _preHeader;
        private Stream _stream;

        /// <summary>
        /// Indicates if a write occurred, indicating the marker in the header needs
        /// to be updated.
        /// </summary>
        private bool _writeOccurred;

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        public DiskImageFile(Stream stream)
        {
            _stream = stream;

            ReadHeader();
        }

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        public DiskImageFile(Stream stream, Ownership ownsStream)
        {
            _stream = stream;
            _ownsStream = ownsStream;

            ReadHeader();
        }

        internal override long Capacity
        {
            get { return _header.DiskSize; }
        }

        /// <summary>
        /// Gets (a guess at) the geometry of the virtual disk.
        /// </summary>
        public override Geometry Geometry
        {
            get
            {
                if (_header.LChsGeometry != null && _header.LChsGeometry.Cylinders != 0)
                {
                    return _header.LChsGeometry.ToGeometry(_header.DiskSize);
                }
                if (_header.LegacyGeometry.Cylinders != 0)
                {
                    return _header.LegacyGeometry.ToGeometry(_header.DiskSize);
                }
                return GeometryRecord.FromCapacity(_header.DiskSize).ToGeometry(_header.DiskSize);
            }
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return _header.ImageType != ImageType.Fixed; }
        }

        /// <summary>
        /// Gets a value indicating whether the file is a differencing disk.
        /// </summary>
        public override bool NeedsParent
        {
            get { return _header.ImageType == ImageType.Differencing || _header.ImageType == ImageType.Undo; }
        }

        internal override FileLocator RelativeFileLocator
        {
            // Differencing disks not yet supported.
            get { return null; }
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VDI file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VDI file.</returns>
        public static DiskImageFile InitializeFixed(Stream stream, Ownership ownsStream, long capacity)
        {
            PreHeaderRecord preHeader = PreHeaderRecord.Initialized();
            HeaderRecord header = HeaderRecord.Initialized(ImageType.Fixed, ImageFlags.None, capacity, 1024 * 1024, 0);

            byte[] blockTable = new byte[header.BlockCount * 4];
            for (int i = 0; i < header.BlockCount; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian((uint)i, blockTable, i * 4);
            }

            header.BlocksAllocated = header.BlockCount;

            stream.Position = 0;
            preHeader.Write(stream);
            header.Write(stream);

            stream.Position = header.BlocksOffset;
            stream.Write(blockTable, 0, blockTable.Length);

            long totalSize = header.DataOffset + header.BlockSize * (long)header.BlockCount;
            if (stream.Length < totalSize)
            {
                stream.SetLength(totalSize);
            }

            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VDI file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VDI file.</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, Ownership ownsStream, long capacity)
        {
            PreHeaderRecord preHeader = PreHeaderRecord.Initialized();
            HeaderRecord header = HeaderRecord.Initialized(ImageType.Dynamic, ImageFlags.None, capacity, 1024 * 1024, 0);

            byte[] blockTable = new byte[header.BlockCount * 4];
            for (int i = 0; i < blockTable.Length; ++i)
            {
                blockTable[i] = 0xFF;
            }

            header.BlocksAllocated = 0;

            stream.Position = 0;
            preHeader.Write(stream);
            header.Write(stream);

            stream.Position = header.BlocksOffset;
            stream.Write(blockTable, 0, blockTable.Length);

            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Opens the content of the disk image file as a stream.
        /// </summary>
        /// <param name="parent">The parent file's content (if any).</param>
        /// <param name="ownsParent">Whether the created stream assumes ownership of parent stream.</param>
        /// <returns>The new content stream.</returns>
        public override SparseStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            if (parent != null && ownsParent == Ownership.Dispose)
            {
                // Not needed until differencing disks supported.
                parent.Dispose();
            }

            DiskStream stream = new DiskStream(_stream, Ownership.None, _header);
            stream.WriteOccurred += OnWriteOccurred;
            return stream;
        }

        /// <summary>
        /// Gets the possible locations of the parent file (if any).
        /// </summary>
        /// <returns>Array of strings, empty if no parent.</returns>
        public override string[] GetParentLocations()
        {
            // Until diff/undo supported
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
                    if (_writeOccurred && _stream != null)
                    {
                        _header.ModificationId = Guid.NewGuid();
                        _stream.Position = PreHeaderRecord.Size;
                        _header.Write(_stream);
                    }

                    if (_ownsStream == Ownership.Dispose && _stream != null)
                    {
                        _stream.Dispose();
                        _stream = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private void ReadHeader()
        {
            _stream.Position = 0;
            _preHeader = new PreHeaderRecord();
            _preHeader.Read(_stream);
            _header = new HeaderRecord();
            _header.Read(_preHeader.Version, _stream);
        }

        private void OnWriteOccurred(object sender, EventArgs e)
        {
            _writeOccurred = true;
        }
    }
}
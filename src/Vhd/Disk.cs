//
// Copyright (c) 2008, Kenneth Bell
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
using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Vhd
{
    /// <summary>
    /// Represents a VHD-backed disk.
    /// </summary>
    public class Disk : VirtualDisk
    {
        private DiskImageFile _vhdFile;
        private Stream _fileStream;

        /// <summary>
        /// Creates a new instance from an existing stream, differencing disks not supported.
        /// </summary>
        /// <param name="stream">The stream to read</param>
        public Disk(Stream stream)
        {
            _fileStream = stream;
            _vhdFile = new DiskImageFile(stream);

            if (_vhdFile.HasParent)
            {
                throw new NotSupportedException("Differencing disks cannot be opened from a stream");
            }
        }

        /// <summary>
        /// Initializes a stream as a VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="capacity">The desired capacity of the VHD file</param>
        /// <param name="type">The type of VHD file</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static Disk Initialize(Stream stream, long capacity, FileType type)
        {
            if (type != FileType.Fixed)
            {
                throw new NotImplementedException("Only fixed disks supported so far");
            }

            DiskGeometry geometry = DiskGeometry.FromCapacity(capacity);
            Footer footer = new Footer(geometry, type);
            footer.UpdateChecksum();

            byte[] sector = new byte[Utilities.SectorSize];
            footer.ToBytes(sector, 0);
            stream.Position = geometry.Capacity;
            stream.Write(sector, 0, sector.Length);
            stream.SetLength(stream.Position);

            stream.Position = 0;
            return new Disk(stream);
        }

        /// <summary>
        /// Gets the geometry of the disk.
        /// </summary>
        public override DiskGeometry Geometry
        {
            get { return _vhdFile.Geometry; }
        }

        /// <summary>
        /// Gets the content of the disk as a stream.
        /// </summary>
        public override Stream Content
        {
            get { return _vhdFile.GetContentStream(null); }
        }

        /// <summary>
        /// Reduces the amount of actual storage consumed, if possible, by the disk.
        /// </summary>
        public override void Compact()
        {
            _vhdFile.Compact();
        }

        /// <summary>
        /// Gets the layers that make up the disk.
        /// </summary>
        public override ReadOnlyCollection<VirtualDiskLayer> Layers
        {
            get { return new ReadOnlyCollection<VirtualDiskLayer>(new VirtualDiskLayer[] { _vhdFile }); }
        }
    }
}

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
using System.IO;

namespace DiscUtils.Vhd
{
    /// <summary>
    /// Represents a single .VHD file.
    /// </summary>
    public class DiskImageFile : VirtualDiskLayer
    {
        /// <summary>
        /// The stream containing the VHD file.
        /// </summary>
        private Stream _fileStream;

        /// <summary>
        /// The VHD file's footer.
        /// </summary>
        private Footer _footer;

        /// <summary>
        /// The VHD file's dynamic header (if not static)
        /// </summary>
        private DynamicHeader _dynamicHeader;

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream to interpret</param>
        public DiskImageFile(Stream stream)
        {
            _fileStream = stream;

            ReadFooter(true);

            ReadHeaders();

            if (_footer.DiskType == FileType.Differencing)
            {
                throw new NotImplementedException("Differencing disks not supported yet");
            }
        }

        /// <summary>
        /// Gets a value indicating if the VHD file is a differencing disk.
        /// </summary>
        public bool HasParent
        {
            get { return _footer.DiskType == FileType.Differencing; }
        }

        /// <summary>
        /// Gets the geometry of the virtual disk.
        /// </summary>
        public DiskGeometry Geometry
        {
            get { return _footer.Geometry; }
        }

        /// <summary>
        /// Reduces the amount of actual storage consumed, if possible, by the file.
        /// </summary>
        public void Compact()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return _footer.DiskType != FileType.Fixed; }
        }

        internal override Stream GetContentStream(Stream parent)
        {
            if (_footer.DiskType == FileType.Fixed)
            {
                return new SubStream(_fileStream, 0, _fileStream.Length - 512);
            }
            else if (_footer.DiskType == FileType.Dynamic)
            {
                return new DynamicStream(_fileStream, _dynamicHeader, _footer.CurrentSize);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

#if false
        /// <summary>
        /// Reads a logical disk sector, if available.
        /// </summary>
        /// <param name="sector">The logical block address (i.e. index) of the sector</param>
        /// <param name="buffer">The buffer to populate</param>
        /// <param name="offset">The offset in <paramref name="buffer"/> to place the first byte</param>
        /// <returns><code>true</code> if the sector is present, else <code>false</code></returns>
        public override bool TryReadSector(long sector, byte[] buffer, int offset)
        {
            if (_footer.DiskType != FileType.Fixed)
            {
                throw new NotImplementedException();
            }

            if (sector < 0)
            {
                throw new ArgumentOutOfRangeException("sector", sector, "Negative sector address");
            }

            long pos = sector * Utilities.SectorSize;
            if ((ulong)pos + Utilities.SectorSize >= _footer.CurrentSize)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Sector {0}: attempt to read beyond end of disk", sector));
            }

            _fileStream.Position = pos;
            int numRead = Utilities.ReadFully(_fileStream, buffer, offset, Utilities.SectorSize);
            if (numRead != Utilities.SectorSize)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Sector {0}: failed to read entire sector", sector));
            }

            return true;
        }

        /// <summary>
        /// Reads sectors up until there's one that is 'not stored'.
        /// </summary>
        /// <param name="first">The logical block address (i.e. index) of the first sector</param>
        /// <param name="buffer">The buffer to populate</param>
        /// <param name="offset">The offset within the buffer to fill from</param>
        /// <param name="count">The maximum number of sectors to read</param>
        /// <returns>The number of sectors read, zero if none</returns>
        public override int ReadSectors(long first, byte[] buffer, int offset, int count)
        {
            if (_footer.DiskType != FileType.Fixed)
            {
                throw new NotImplementedException();
            }

            if (first < 0)
            {
                throw new ArgumentOutOfRangeException("first", first, "Negative sector address");
            }

            if (count > int.MaxValue / Utilities.SectorSize)
            {
                throw new ArgumentOutOfRangeException("count", count, "Total bytes to read exceeds Int32.MaxValue");
            }

            long pos = first * Utilities.SectorSize;
            if ((ulong)(pos + count) >= _footer.CurrentSize)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Sector {0} (+{1}): attempt to read beyond end of disk", first, count));
            }

            _fileStream.Position = pos;
            int numRead = Utilities.ReadFully(_fileStream, buffer, offset, count * Utilities.SectorSize);
            if (numRead != count * Utilities.SectorSize)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Sector {0}: failed to read entire set of sectors", first));
            }

            return count;
        }

        /// <summary>
        /// Writes one or more sectors.
        /// </summary>
        /// <param name="first">The logical block address (i.e. index) of the first sector</param>
        /// <param name="buffer">The buffer containing the data to write</param>
        /// <param name="offset">The offset within the buffer of the data to write</param>
        /// <param name="count">The number of bytes to write</param>
        public override void WriteSectors(long first, byte[] buffer, int offset, int count)
        {
            if (_footer.DiskType != FileType.Fixed)
            {
                throw new NotImplementedException();
            }

            if (first < 0)
            {
                throw new ArgumentOutOfRangeException("first", first, "Negative sector address");
            }

            if (count > int.MaxValue / Utilities.SectorSize)
            {
                throw new ArgumentOutOfRangeException("count", count, "Total bytes to write exceeds Int32.MaxValue");
            }

            long pos = first * Utilities.SectorSize;
            if ((ulong)(pos + count) >= _footer.CurrentSize)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Sector {0} (+{1}): attempt to write beyond end of disk", first, count));
            }

            _fileStream.Position = pos;
            _fileStream.Write(buffer, offset, count * Utilities.SectorSize);
        }

        /// <summary>
        /// Deletes one or more sectors.
        /// </summary>
        /// <param name="first">The logical block address (i.e. index) of the first sector to delete</param>
        /// <param name="count">The number of sectors to delete</param>
        /// <exception cref="System.NotSupportedException">The layer doesn't support absent sectors</exception>
        public override void DeleteSectors(long first, int count)
        {
            if (_footer.DiskType != FileType.Fixed)
            {
                throw new NotImplementedException();
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Indicates if a particular sector is present in this layer.
        /// </summary>
        /// <param name="sector">The logical block address (i.e. index) of the sector</param>
        /// <returns><code>true</code> if the sector is present, else <code>false</code></returns>
        public override bool HasSector(long sector)
        {
            if (_footer.DiskType != FileType.Fixed)
            {
                throw new NotImplementedException();
            }

            if (sector < 0)
            {
                throw new ArgumentOutOfRangeException("sector", sector, "Negative sector address");
            }

            long pos = sector * Utilities.SectorSize;
            return ((ulong)((sector * Utilities.SectorSize) + Utilities.SectorSize) < _footer.CurrentSize);
        }

        /// <summary>
        /// Gets the Logical Block Address of the next sector stored in this layer.
        /// </summary>
        /// <param name="sector">The reference sector</param>
        /// <returns>The LBA of the next sector, or <code>-1</code> if none.</returns>
        public override long NextSector(long sector)
        {
            if (_footer.DiskType != FileType.Fixed)
            {
                throw new NotImplementedException();
            }

            if (sector < 0)
            {
                throw new ArgumentOutOfRangeException("sector", sector, "Negative sector address");
            }

            if ((ulong)(((sector + 1) * Utilities.SectorSize) + Utilities.SectorSize) < _footer.CurrentSize)
            {
                return sector + 1;
            }
            else
            {
                return -1;
            }
        }
#endif
        private void ReadFooter(bool fallbackToFront)
        {
            long length = _fileStream.Length;


            _fileStream.Position = _fileStream.Length - Utilities.SectorSize;
            byte[] sector = Utilities.ReadFully(_fileStream, Utilities.SectorSize);

            _footer = Footer.FromBytes(sector, 0);

            byte[] outSector = new byte[512];
            _footer.ToBytes(outSector, 0);
            for (int i = 0; i < sector.Length; ++i)
            {
                if (sector[i] != outSector[i])
                {
                    throw new IOException();
                }
            }


            if (!_footer.IsValid())
            {
                if (!fallbackToFront)
                {
                    throw new IOException("Corrupt VHD file - invalid footer at end (did not check front of file)");
                }

                _fileStream.Position = 0;
                Utilities.ReadFully(_fileStream, sector, 0, Utilities.SectorSize);

                _footer = Footer.FromBytes(sector, 0);
                if (!_footer.IsValid())
                {
                    throw new IOException("Failed to find a valid VHD footer at start or end of file - VHD file is corrupt");
                }
            }
        }

        private void ReadHeaders()
        {
            long pos = _footer.DataOffset;
            while (pos != -1)
            {
                _fileStream.Position = pos;
                Header hdr = Header.FromStream(_fileStream);
                if (hdr.Cookie == DynamicHeader.HeaderCookie)
                {
                    _fileStream.Position = pos;
                    _dynamicHeader = DynamicHeader.FromStream(_fileStream);
                    if (!_dynamicHeader.IsValid())
                    {
                        throw new IOException("Invalid Dynamic Disc Header");
                    }
                }
                pos = hdr.DataOffset;
            }
        }

    }
}

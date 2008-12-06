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
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        /// Indicates if this object controls the lifetime of the stream.
        /// </summary>
        private bool _ownsStream;

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream to interpret</param>
        public DiskImageFile(Stream stream)
        {
            _fileStream = stream;

            ReadFooter(true);

            ReadHeaders();
        }

        /// <summary>
        /// Creates a new instance from a stream.
        /// </summary>
        /// <param name="stream">The stream to interpret</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        public DiskImageFile(Stream stream, bool ownsStream)
        {
            _fileStream = stream;
            _ownsStream = ownsStream;

            ReadFooter(true);

            ReadHeaders();
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
                    if (_ownsStream)
                    {
                        _fileStream.Dispose();
                        _fileStream = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHD file, without taking ownership of the stream.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static DiskImageFile InitializeFixed(Stream stream, long capacity)
        {
            return InitializeFixed(stream, false, capacity);
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static DiskImageFile InitializeFixed(Stream stream, bool ownsStream, long capacity)
        {
            Geometry geometry = Geometry.FromCapacity(capacity);
            Footer footer = new Footer(geometry, FileType.Fixed);
            footer.UpdateChecksum();

            byte[] sector = new byte[Utilities.SectorSize];
            footer.ToBytes(sector, 0);
            stream.Position = geometry.Capacity;
            stream.Write(sector, 0, sector.Length);
            stream.SetLength(stream.Position);

            stream.Position = 0;
            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file, without taking ownership of the stream.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, long capacity)
        {
            return InitializeDynamic(stream, false, capacity);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, bool ownsStream, long capacity)
        {
            Geometry geometry = Geometry.FromCapacity(capacity);
            Footer footer = new Footer(geometry, FileType.Dynamic);
            footer.DataOffset = 512; // Offset of Dynamic Header
            footer.UpdateChecksum();
            byte[] footerBlock = new byte[512];
            footer.ToBytes(footerBlock, 0);

            DynamicHeader dynamicHeader = new DynamicHeader(-1, 1024 + 512, capacity);
            dynamicHeader.UpdateChecksum();
            byte[] dynamicHeaderBlock = new byte[1024];
            dynamicHeader.ToBytes(dynamicHeaderBlock, 0);

            int batSize = (((dynamicHeader.MaxTableEntries * 4) + Utilities.SectorSize - 1) / Utilities.SectorSize) * Utilities.SectorSize;
            byte[] bat = new byte[batSize];
            for (int i = 0; i < bat.Length; ++i)
            {
                bat[i] = 0xFF;
            }

            stream.Position = 0;
            stream.Write(footerBlock, 0, 512);
            stream.Write(dynamicHeaderBlock, 0, 1024);
            stream.Write(bat, 0, batSize);
            stream.Write(footerBlock, 0, 512);

            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Initializes a stream as a differencing disk VHD file, without taking ownership of the stream.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="parent">The disk this file is a different from.</param>
        /// <param name="parentAbsolutePath">The full path to the parent disk.</param>
        /// <param name="parentRelativePath">The relative path from the new disk to the parent disk.</param>
        /// <param name="parentModificationTimeUtc">The time the parent disk's file was last modified (from file system).</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static DiskImageFile InitializeDifferencing(
            Stream stream, DiskImageFile parent, string parentAbsolutePath,
            string parentRelativePath, DateTime parentModificationTimeUtc)
        {
            return InitializeDifferencing(stream, false, parent, parentAbsolutePath, parentRelativePath, parentModificationTimeUtc);
        }

        /// <summary>
        /// Initializes a stream as a differencing disk VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="parent">The disk this file is a different from.</param>
        /// <param name="parentAbsolutePath">The full path to the parent disk.</param>
        /// <param name="parentRelativePath">The relative path from the new disk to the parent disk.</param>
        /// <param name="parentModificationTimeUtc">The time the parent disk's file was last modified (from file system).</param>
        /// <returns>An object that accesses the stream as a VHD file</returns>
        public static DiskImageFile InitializeDifferencing(
            Stream stream, bool ownsStream, DiskImageFile parent,
            string parentAbsolutePath, string parentRelativePath, DateTime parentModificationTimeUtc)
        {
            Footer footer = new Footer(parent.Geometry, FileType.Differencing);
            footer.DataOffset = 512; // Offset of Dynamic Header
            footer.UpdateChecksum();
            byte[] footerBlock = new byte[512];
            footer.ToBytes(footerBlock, 0);

            long tableOffset = 512 + 1024; // Footer + Header

            DynamicHeader dynamicHeader = new DynamicHeader(-1, tableOffset, footer.CurrentSize);
            int batSize = (((dynamicHeader.MaxTableEntries * 4) + Utilities.SectorSize - 1) / Utilities.SectorSize) * Utilities.SectorSize;
            dynamicHeader.ParentUniqueId = parent.UniqueId;
            dynamicHeader.ParentTimeStamp = parentModificationTimeUtc;
            dynamicHeader.ParentUnicodeName = Utilities.GetFileFromPath(parentAbsolutePath);
            dynamicHeader.ParentLocators[7].PlatformCode = ParentLocator.PlatformCodeWindowsAbsoluteUnicode;
            dynamicHeader.ParentLocators[7].PlatformDataSpace = 512;
            dynamicHeader.ParentLocators[7].PlatformDataLength = parentAbsolutePath.Length * 2;
            dynamicHeader.ParentLocators[7].PlatformDataOffset = tableOffset + batSize;
            dynamicHeader.ParentLocators[6].PlatformCode = ParentLocator.PlatformCodeWindowsRelativeUnicode;
            dynamicHeader.ParentLocators[6].PlatformDataSpace = 512;
            dynamicHeader.ParentLocators[6].PlatformDataLength = parentRelativePath.Length * 2;
            dynamicHeader.ParentLocators[6].PlatformDataOffset = tableOffset + batSize + 512;
            dynamicHeader.UpdateChecksum();
            byte[] dynamicHeaderBlock = new byte[1024];
            dynamicHeader.ToBytes(dynamicHeaderBlock, 0);

            byte[] platformLocator1 = new byte[512];
            Encoding.Unicode.GetBytes(parentAbsolutePath, 0, parentAbsolutePath.Length, platformLocator1, 0);
            byte[] platformLocator2 = new byte[512];
            Encoding.Unicode.GetBytes(parentRelativePath, 0, parentRelativePath.Length, platformLocator2, 0);

            byte[] bat = new byte[batSize];
            for (int i = 0; i < bat.Length; ++i)
            {
                bat[i] = 0xFF;
            }

            stream.Position = 0;
            stream.Write(footerBlock, 0, 512);
            stream.Write(dynamicHeaderBlock, 0, 1024);
            stream.Write(bat, 0, batSize);
            stream.Write(platformLocator1, 0, 512);
            stream.Write(platformLocator2, 0, 512);
            stream.Write(footerBlock, 0, 512);

            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Gets a value indicating if the VHD file is a differencing disk.
        /// </summary>
        public bool NeedsParent
        {
            get { return _footer.DiskType == FileType.Differencing; }
        }

        /// <summary>
        /// Gets the location of the parent file, given a base path.
        /// </summary>
        /// <param name="basePath">The full path to this file</param>
        /// <returns>Array of candidate file locations</returns>
        public string[] GetParentLocations(string basePath)
        {
            List<string> absPaths = new List<string>(8);
            List<string> relPaths = new List<string>(8);
            foreach (var pl in _dynamicHeader.ParentLocators)
            {
                if (pl.PlatformCode == ParentLocator.PlatformCodeWindowsAbsoluteUnicode
                    || pl.PlatformCode == ParentLocator.PlatformCodeWindowsRelativeUnicode)
                {
                    _fileStream.Position = pl.PlatformDataOffset;
                    byte[] buffer = Utilities.ReadFully(_fileStream, pl.PlatformDataLength);
                    string locationVal = Encoding.Unicode.GetString(buffer);

                    if (pl.PlatformCode == ParentLocator.PlatformCodeWindowsAbsoluteUnicode)
                    {
                        absPaths.Add(locationVal);
                    }
                    else // Relative
                    {
                        relPaths.Add(Utilities.ResolveRelativePath(basePath, locationVal));
                    }
                }
            }

            // Order the paths to put absolute paths first
            List<string> paths = new List<string>(absPaths.Count + relPaths.Count + 1);
            paths.AddRange(absPaths);
            paths.AddRange(relPaths);

            // As a back-up, try to infer from the parent name...
            if (paths.Count == 0)
            {
                paths.Add(Utilities.ResolveRelativePath(basePath, _dynamicHeader.ParentUnicodeName));
            }

            return paths.ToArray();
        }

        /// <summary>
        /// Gets the geometry of the virtual disk.
        /// </summary>
        public Geometry Geometry
        {
            get { return _footer.Geometry; }
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return _footer.DiskType != FileType.Fixed; }
        }

        internal override Stream OpenContent(Stream parent, bool ownsParent)
        {
            if (_footer.DiskType == FileType.Fixed)
            {
                if (parent != null && ownsParent)
                {
                    parent.Dispose();
                }
                return new SubStream(_fileStream, 0, _fileStream.Length - 512);
            }
            else if (_footer.DiskType == FileType.Dynamic)
            {
                if (parent != null && ownsParent)
                {
                    parent.Dispose();
                }
                return new DynamicStream(_fileStream, _dynamicHeader, _footer.CurrentSize, new ZeroStream(_footer.CurrentSize), true);
            }
            else
            {
                return new DynamicStream(_fileStream, _dynamicHeader, _footer.CurrentSize, parent, ownsParent);
            }
        }

        /// <summary>
        /// Gets the unique id of this file.
        /// </summary>
        public Guid UniqueId
        {
            get { return _footer.UniqueId; }
        }

        /// <summary>
        /// Gets the timestamp for this file (when it was created).
        /// </summary>
        public DateTime CreationTimestamp
        {
            get { return _footer.TimeStamp; }
        }

        private void ReadFooter(bool fallbackToFront)
        {
            long length = _fileStream.Length;


            _fileStream.Position = _fileStream.Length - Utilities.SectorSize;
            byte[] sector = Utilities.ReadFully(_fileStream, Utilities.SectorSize);

            _footer = Footer.FromBytes(sector, 0);

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

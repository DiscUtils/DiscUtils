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
using System.Text;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Vhd
{
    /// <summary>
    /// Represents a single .VHD file.
    /// </summary>
    public sealed class DiskImageFile : VirtualDiskLayer
    {
        /// <summary>
        /// The VHD file's dynamic header (if not static).
        /// </summary>
        private DynamicHeader _dynamicHeader;

        /// <summary>
        /// The object that can be used to locate relative file paths.
        /// </summary>
        private readonly FileLocator _fileLocator;

        /// <summary>
        /// The file name of this VHD.
        /// </summary>
        private readonly string _fileName;

        /// <summary>
        /// The stream containing the VHD file.
        /// </summary>
        private Stream _fileStream;

        /// <summary>
        /// The VHD file's footer.
        /// </summary>
        private Footer _footer;

        /// <summary>
        /// Indicates if this object controls the lifetime of the stream.
        /// </summary>
        private readonly Ownership _ownsStream;

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        public DiskImageFile(Stream stream)
        {
            _fileStream = stream;

            ReadFooter(true);

            ReadHeaders();
        }

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        /// <param name="ownsStream">Indicates if the new instance should control the lifetime of the stream.</param>
        public DiskImageFile(Stream stream, Ownership ownsStream)
        {
            _fileStream = stream;
            _ownsStream = ownsStream;

            ReadFooter(true);

            ReadHeaders();
        }

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="path">The file path to open.</param>
        /// <param name="access">Controls how the file can be accessed.</param>
        public DiskImageFile(string path, FileAccess access)
            : this(new LocalFileLocator(Path.GetDirectoryName(path)), Path.GetFileName(path), access) {}

        internal DiskImageFile(FileLocator locator, string path, Stream stream, Ownership ownsStream)
            : this(stream, ownsStream)
        {
            _fileLocator = locator.GetRelativeLocator(locator.GetDirectoryFromPath(path));
            _fileName = locator.GetFileFromPath(path);
        }

        internal DiskImageFile(FileLocator locator, string path, FileAccess access)
        {
            FileShare share = access == FileAccess.Read ? FileShare.Read : FileShare.None;
            _fileStream = locator.Open(path, FileMode.Open, access, share);
            _ownsStream = Ownership.Dispose;

            try
            {
                _fileLocator = locator.GetRelativeLocator(locator.GetDirectoryFromPath(path));
                _fileName = locator.GetFileFromPath(path);

                ReadFooter(true);

                ReadHeaders();
            }
            catch
            {
                _fileStream.Dispose();
                throw;
            }
        }

        internal override long Capacity
        {
            get { return _footer.CurrentSize; }
        }

        /// <summary>
        /// Gets the timestamp for this file (when it was created).
        /// </summary>
        public DateTime CreationTimestamp
        {
            get { return _footer.Timestamp; }
        }

        /// <summary>
        /// Gets the extent that comprises this file.
        /// </summary>
        public override IList<VirtualDiskExtent> Extents
        {
            get
            {
                List<VirtualDiskExtent> result = new List<VirtualDiskExtent>();
                result.Add(new DiskExtent(this));
                return result;
            }
        }

        /// <summary>
        /// Gets the full path to this disk layer, or empty string.
        /// </summary>
        public override string FullPath
        {
            get
            {
                if (_fileLocator != null && _fileName != null)
                {
                    return _fileLocator.GetFullPath(_fileName);
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the geometry of the virtual disk.
        /// </summary>
        public override Geometry Geometry
        {
            get { return _footer.Geometry; }
        }

        /// <summary>
        /// Gets detailed information about the VHD file.
        /// </summary>
        public DiskImageFileInfo Information
        {
            get { return new DiskImageFileInfo(_footer, _dynamicHeader, _fileStream); }
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return _footer.DiskType != FileType.Fixed; }
        }

        /// <summary>
        /// Gets a value indicating whether the file is a differencing disk.
        /// </summary>
        public override bool NeedsParent
        {
            get { return _footer.DiskType == FileType.Differencing; }
        }

        /// <summary>
        /// Gets the unique id of the parent disk.
        /// </summary>
        public Guid ParentUniqueId
        {
            get { return _dynamicHeader == null ? Guid.Empty : _dynamicHeader.ParentUniqueId; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return _fileLocator; }
        }

        internal long StoredSize
        {
            get { return _fileStream.Length; }
        }

        /// <summary>
        /// Gets the unique id of this file.
        /// </summary>
        public Guid UniqueId
        {
            get { return _footer.UniqueId; }
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static DiskImageFile InitializeFixed(Stream stream, Ownership ownsStream, long capacity)
        {
            return InitializeFixed(stream, ownsStream, capacity, null);
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static DiskImageFile InitializeFixed(Stream stream, Ownership ownsStream, long capacity,
                                                    Geometry geometry)
        {
            InitializeFixedInternal(stream, capacity, geometry);
            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, Ownership ownsStream, long capacity)
        {
            return InitializeDynamic(stream, ownsStream, capacity, null, DynamicHeader.DefaultBlockSize);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, Ownership ownsStream, long capacity,
                                                      Geometry geometry)
        {
            return InitializeDynamic(stream, ownsStream, capacity, geometry, DynamicHeader.DefaultBlockSize);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="blockSize">The size of each block (unit of allocation).</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, Ownership ownsStream, long capacity, long blockSize)
        {
            return InitializeDynamic(stream, ownsStream, capacity, null, blockSize);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHD file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <param name="blockSize">The size of each block (unit of allocation).</param>
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, Ownership ownsStream, long capacity,
                                                      Geometry geometry, long blockSize)
        {
            InitializeDynamicInternal(stream, capacity, geometry, blockSize);

            return new DiskImageFile(stream, ownsStream);
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
        /// <returns>An object that accesses the stream as a VHD file.</returns>
        public static DiskImageFile InitializeDifferencing(
            Stream stream,
            Ownership ownsStream,
            DiskImageFile parent,
            string parentAbsolutePath,
            string parentRelativePath,
            DateTime parentModificationTimeUtc)
        {
            InitializeDifferencingInternal(stream, parent, parentAbsolutePath, parentRelativePath,
                parentModificationTimeUtc);

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
            return DoOpenContent(parent, ownsParent);
        }

        /// <summary>
        /// Gets the location of the parent file, given a base path.
        /// </summary>
        /// <returns>Array of candidate file locations.</returns>
        public override string[] GetParentLocations()
        {
            return GetParentLocations(_fileLocator);
        }

        /// <summary>
        /// Gets the location of the parent file, given a base path.
        /// </summary>
        /// <param name="basePath">The full path to this file.</param>
        /// <returns>Array of candidate file locations.</returns>
        [Obsolete("Use GetParentLocations() by preference")]
        public string[] GetParentLocations(string basePath)
        {
            return GetParentLocations(new LocalFileLocator(basePath));
        }

        internal static DiskImageFile InitializeFixed(FileLocator locator, string path, long capacity, Geometry geometry)
        {
            DiskImageFile result = null;
            Stream stream = locator.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            try
            {
                InitializeFixedInternal(stream, capacity, geometry);
                result = new DiskImageFile(locator, path, stream, Ownership.Dispose);
                stream = null;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            return result;
        }

        internal static DiskImageFile InitializeDynamic(FileLocator locator, string path, long capacity,
                                                        Geometry geometry, long blockSize)
        {
            DiskImageFile result = null;
            Stream stream = locator.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            try
            {
                InitializeDynamicInternal(stream, capacity, geometry, blockSize);
                result = new DiskImageFile(locator, path, stream, Ownership.Dispose);
                stream = null;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            return result;
        }

        internal DiskImageFile CreateDifferencing(FileLocator fileLocator, string path)
        {
            Stream stream = fileLocator.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            string fullPath = _fileLocator.GetFullPath(_fileName);
            string relativePath = fileLocator.MakeRelativePath(_fileLocator, _fileName);
            DateTime lastWriteTime = _fileLocator.GetLastWriteTimeUtc(_fileName);

            InitializeDifferencingInternal(stream, this, fullPath, relativePath, lastWriteTime);

            return new DiskImageFile(fileLocator, path, stream, Ownership.Dispose);
        }

        internal MappedStream DoOpenContent(SparseStream parent, Ownership ownsParent)
        {
            if (_footer.DiskType == FileType.Fixed)
            {
                if (parent != null && ownsParent == Ownership.Dispose)
                {
                    parent.Dispose();
                }

                return new SubStream(_fileStream, 0, _fileStream.Length - 512);
            }
            if (_footer.DiskType == FileType.Dynamic)
            {
                if (parent != null && ownsParent == Ownership.Dispose)
                {
                    parent.Dispose();
                }

                return new DynamicStream(_fileStream, _dynamicHeader, _footer.CurrentSize,
                    new ZeroStream(_footer.CurrentSize), Ownership.Dispose);
            }
            if (parent == null)
            {
                parent = new ZeroStream(_footer.CurrentSize);
                ownsParent = Ownership.Dispose;
            }

            return new DynamicStream(_fileStream, _dynamicHeader, _footer.CurrentSize, parent, ownsParent);
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
                    if (_ownsStream == Ownership.Dispose && _fileStream != null)
                    {
                        _fileStream.Dispose();
                    }

                    _fileStream = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private static void InitializeFixedInternal(Stream stream, long capacity, Geometry geometry)
        {
            if (geometry == null)
            {
                geometry = Geometry.FromCapacity(capacity);
            }

            Footer footer = new Footer(geometry, capacity, FileType.Fixed);
            footer.UpdateChecksum();

            byte[] sector = new byte[Sizes.Sector];
            footer.ToBytes(sector, 0);
            stream.Position = MathUtilities.RoundUp(capacity, Sizes.Sector);
            stream.Write(sector, 0, sector.Length);
            stream.SetLength(stream.Position);

            stream.Position = 0;
        }

        private static void InitializeDynamicInternal(Stream stream, long capacity, Geometry geometry, long blockSize)
        {
            if (blockSize > uint.MaxValue || blockSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockSize), "Must be in the range 0 to uint.MaxValue");
            }

            if (geometry == null)
            {
                geometry = Geometry.FromCapacity(capacity);
            }

            Footer footer = new Footer(geometry, capacity, FileType.Dynamic);
            footer.DataOffset = 512; // Offset of Dynamic Header
            footer.UpdateChecksum();
            byte[] footerBlock = new byte[512];
            footer.ToBytes(footerBlock, 0);

            DynamicHeader dynamicHeader = new DynamicHeader(-1, 1024 + 512, (uint)blockSize, capacity);
            dynamicHeader.UpdateChecksum();
            byte[] dynamicHeaderBlock = new byte[1024];
            dynamicHeader.ToBytes(dynamicHeaderBlock, 0);

            int batSize = (dynamicHeader.MaxTableEntries * 4 + Sizes.Sector - 1) / Sizes.Sector *
                          Sizes.Sector;
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
        }

        private static void InitializeDifferencingInternal(Stream stream, DiskImageFile parent,
                                                           string parentAbsolutePath, string parentRelativePath, DateTime parentModificationTimeUtc)
        {
            Footer footer = new Footer(parent.Geometry, parent._footer.CurrentSize, FileType.Differencing);
            footer.DataOffset = 512; // Offset of Dynamic Header
            footer.OriginalSize = parent._footer.OriginalSize;
            footer.UpdateChecksum();
            byte[] footerBlock = new byte[512];
            footer.ToBytes(footerBlock, 0);

            long tableOffset = 512 + 1024; // Footer + Header

            uint blockSize = parent._dynamicHeader == null
                ? DynamicHeader.DefaultBlockSize
                : parent._dynamicHeader.BlockSize;

            DynamicHeader dynamicHeader = new DynamicHeader(-1, tableOffset, blockSize, footer.CurrentSize);
            int batSize = (dynamicHeader.MaxTableEntries * 4 + Sizes.Sector - 1) / Sizes.Sector *
                          Sizes.Sector;
            dynamicHeader.ParentUniqueId = parent.UniqueId;
            dynamicHeader.ParentTimestamp = parentModificationTimeUtc;
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
        }

        /// <summary>
        /// Gets the locations of the parent file.
        /// </summary>
        /// <param name="fileLocator">The file locator to use.</param>
        /// <returns>Array of candidate file locations.</returns>
        private string[] GetParentLocations(FileLocator fileLocator)
        {
            if (!NeedsParent)
            {
                throw new InvalidOperationException("Only differencing disks contain parent locations");
            }

            if (fileLocator == null)
            {
                // Use working directory by default
                fileLocator = new LocalFileLocator(string.Empty);
            }

            List<string> absPaths = new List<string>(8);
            List<string> relPaths = new List<string>(8);
            foreach (ParentLocator pl in _dynamicHeader.ParentLocators)
            {
                if (pl.PlatformCode == ParentLocator.PlatformCodeWindowsAbsoluteUnicode
                    || pl.PlatformCode == ParentLocator.PlatformCodeWindowsRelativeUnicode)
                {
                    _fileStream.Position = pl.PlatformDataOffset;
                    byte[] buffer = StreamUtilities.ReadExact(_fileStream, pl.PlatformDataLength);
                    string locationVal = Encoding.Unicode.GetString(buffer);

                    if (pl.PlatformCode == ParentLocator.PlatformCodeWindowsAbsoluteUnicode)
                    {
                        absPaths.Add(locationVal);
                    }
                    else
                    {
                        relPaths.Add(fileLocator.ResolveRelativePath(locationVal));
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
                paths.Add(fileLocator.ResolveRelativePath(_dynamicHeader.ParentUnicodeName));
            }

            return paths.ToArray();
        }

        private void ReadFooter(bool fallbackToFront)
        {
            _fileStream.Position = _fileStream.Length - Sizes.Sector;
            byte[] sector = StreamUtilities.ReadExact(_fileStream, Sizes.Sector);

            _footer = Footer.FromBytes(sector, 0);

            if (!_footer.IsValid())
            {
                if (!fallbackToFront)
                {
                    throw new IOException("Corrupt VHD file - invalid footer at end (did not check front of file)");
                }

                _fileStream.Position = 0;
                StreamUtilities.ReadExact(_fileStream, sector, 0, Sizes.Sector);

                _footer = Footer.FromBytes(sector, 0);
                if (!_footer.IsValid())
                {
                    throw new IOException(
                        "Failed to find a valid VHD footer at start or end of file - VHD file is corrupt");
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
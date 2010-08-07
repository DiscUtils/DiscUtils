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
using System.Collections.Generic;
using System.Globalization;

namespace DiscUtils.Vmdk
{
    /// <summary>
    /// Represents a single VMDK file.
    /// </summary>
    public sealed class DiskImageFile : VirtualDiskLayer
    {
        private DescriptorFile _descriptor;
        private SparseStream _contentStream;
        private FileLocator _fileLocator;

        private FileAccess _access;

        /// <summary>
        /// The stream containing the VMDK disk, if this is a monolithic disk.
        /// </summary>
        private Stream _monolithicStream;

        /// <summary>
        /// Indicates if this instance controls lifetime of _monolithicStream.
        /// </summary>
        private Ownership _ownsMonolithicStream;

        private static Random _rng = new Random();

        /// <summary>
        /// Creates a new instance from a file on disk.
        /// </summary>
        /// <param name="path">The path to the disk</param>
        /// <param name="access">The desired access to the disk</param>
        public DiskImageFile(string path, FileAccess access)
        {
            _access = access;

            FileAccess fileAccess = FileAccess.Read;
            FileShare fileShare = FileShare.Read;
            if (_access != FileAccess.Read)
            {
                fileAccess = FileAccess.ReadWrite;
                fileShare = FileShare.None;
            }

            FileStream fileStream = null;
            try
            {
                fileStream = new FileStream(path, FileMode.Open, fileAccess, fileShare);
                LoadDescriptor(fileStream);

                // For monolithic disks, keep hold of the stream - we won't try to use the file name
                // from the embedded descriptor because the file may have been renamed, making the 
                // descriptor out of date.
                if (_descriptor.CreateType == DiskCreateType.StreamOptimized || _descriptor.CreateType == DiskCreateType.MonolithicSparse)
                {
                    _monolithicStream = fileStream;
                    _ownsMonolithicStream = Ownership.Dispose;
                    fileStream = null;
                }
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
            }

            _fileLocator = new LocalFileLocator(Path.GetDirectoryName(path));
        }

        /// <summary>
        /// Creates a new instance from a monolithic file on disk.
        /// </summary>
        /// <param name="stream">The stream containing the monolithic disk</param>
        /// <param name="ownsStream">Indicates if the created instance should own the stream</param>
        public DiskImageFile(Stream stream, Ownership ownsStream)
        {
            _access = stream.CanWrite ? FileAccess.ReadWrite : FileAccess.Read;

            LoadDescriptor(stream);

            bool createTypeIsSparse =
                _descriptor.CreateType == DiskCreateType.MonolithicSparse
                || _descriptor.CreateType == DiskCreateType.StreamOptimized;

            if (!createTypeIsSparse || _descriptor.Extents.Count != 1
                || _descriptor.Extents[0].Type != ExtentType.Sparse || _descriptor.ParentContentId != uint.MaxValue)
            {
                throw new ArgumentException("Only Monolithic Sparse and Streaming Optimized disks can be accessed via a stream", "stream");
            }

            _monolithicStream = stream;
            _ownsMonolithicStream = ownsStream;
        }

        /// <summary>
        /// Creates a new instance from a file.
        /// </summary>
        /// <param name="fileLocator">An object to open the file and any extents</param>
        /// <param name="file">The file name</param>
        /// <param name="access">The type of access desired</param>
        internal DiskImageFile(FileLocator fileLocator, string file, FileAccess access)
        {
            _access = access;

            FileAccess fileAccess = FileAccess.Read;
            FileShare fileShare = FileShare.Read;
            if (_access != FileAccess.Read)
            {
                fileAccess = FileAccess.ReadWrite;
                fileShare = FileShare.None;
            }

            Stream fileStream = null;
            try
            {
                fileStream = fileLocator.Open(file, FileMode.Open, fileAccess, fileShare);
                LoadDescriptor(fileStream);

                // For monolithic disks, keep hold of the stream - we won't try to use the file name
                // from the embedded descriptor because the file may have been renamed, making the 
                // descriptor out of date.
                if (_descriptor.CreateType == DiskCreateType.StreamOptimized || _descriptor.CreateType == DiskCreateType.MonolithicSparse)
                {
                    _monolithicStream = fileStream;
                    _ownsMonolithicStream = Ownership.Dispose;
                    fileStream = null;
                }
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
            }

            _fileLocator = fileLocator.GetRelativeLocator(Path.GetDirectoryName(file));
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, <c>false</c> if in destructor</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_contentStream != null)
                    {
                        _contentStream.Dispose();
                        _contentStream = null;
                    }

                    if (_ownsMonolithicStream == Ownership.Dispose && _monolithicStream != null)
                    {
                        _monolithicStream.Dispose();
                        _monolithicStream = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="parameters">The desired parameters for the new disk.</param>
        /// <returns>The newly created disk image</returns>
        public static DiskImageFile Initialize(string path, DiskParameters parameters)
        {
            FileLocator locator = new LocalFileLocator(Path.GetDirectoryName(path));
            return Initialize(locator, Path.GetFileName(path), parameters);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="fileSystem">The file system to create the disk on.</param>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="parameters">The desired parameters for the new disk.</param>
        /// <returns>The newly created disk image</returns>
        public static DiskImageFile Initialize(DiscFileSystem fileSystem, string path, DiskParameters parameters)
        {
            FileLocator locator = new DiscFileLocator(fileSystem, Path.GetDirectoryName(path));
            return Initialize(locator, Path.GetFileName(path), parameters);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="type">The type of virtual disk to create</param>
        /// <returns>The newly created disk image</returns>
        public static DiskImageFile Initialize(string path, long capacity, DiskCreateType type)
        {
            DiskParameters diskParams = new DiskParameters();
            diskParams.Capacity = capacity;
            diskParams.CreateType = type;

            return Initialize(path, diskParams);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default</param>
        /// <param name="createType">The type of virtual disk to create</param>
        /// <returns>The newly created disk image</returns>
        public static DiskImageFile Initialize(string path, long capacity, Geometry geometry, DiskCreateType createType)
        {
            DiskParameters diskParams = new DiskParameters();
            diskParams.Capacity = capacity;
            diskParams.Geometry = geometry;
            diskParams.CreateType = createType;

            return Initialize(path, diskParams);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default</param>
        /// <param name="createType">The type of virtual disk to create</param>
        /// <param name="adapterType">The type of disk adapter used with the disk</param>
        /// <returns>The newly created disk image</returns>
        public static DiskImageFile Initialize(string path, long capacity, Geometry geometry, DiskCreateType createType, DiskAdapterType adapterType)
        {
            DiskParameters diskParams = new DiskParameters();
            diskParams.Capacity = capacity;
            diskParams.Geometry = geometry;
            diskParams.CreateType = createType;
            diskParams.AdapterType = adapterType;

            return Initialize(path, diskParams);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="fileSystem">The file system to create the VMDK on</param>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="createType">The type of virtual disk to create</param>
        /// <returns>The newly created disk image</returns>
        public static DiskImageFile Initialize(DiscFileSystem fileSystem, string path, long capacity, DiskCreateType createType)
        {
            DiskParameters diskParams = new DiskParameters();
            diskParams.Capacity = capacity;
            diskParams.CreateType = createType;

            return Initialize(fileSystem, path, diskParams);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="fileSystem">The file system to create the VMDK on</param>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="capacity">The desired capacity of the new disk</param>
        /// <param name="createType">The type of virtual disk to create</param>
        /// <param name="adapterType">The type of disk adapter used with the disk</param>
        /// <returns>The newly created disk image</returns>
        public static DiskImageFile Initialize(DiscFileSystem fileSystem, string path, long capacity, DiskCreateType createType, DiskAdapterType adapterType)
        {
            DiskParameters diskParams = new DiskParameters();
            diskParams.Capacity = capacity;
            diskParams.CreateType = createType;
            diskParams.AdapterType = adapterType;

            return Initialize(fileSystem, path, diskParams);
        }

        /// <summary>
        /// Creates a new virtual disk at the specified path.
        /// </summary>
        /// <param name="fileLocator">The object used to locate / create the component files.</param>
        /// <param name="path">The name of the VMDK to create.</param>
        /// <param name="parameters">The desired parameters for the new disk.</param>
        /// <returns>The newly created disk image</returns>
        internal static DiskImageFile Initialize(FileLocator fileLocator, string path, DiskParameters parameters)
        {
            if (parameters.Capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than zero", "parameters");
            }

            Geometry geometry = parameters.Geometry ?? DefaultGeometry(parameters.Capacity);

            Geometry biosGeometry;
            if (parameters.BiosGeometry != null)
            {
                biosGeometry = parameters.BiosGeometry;
            }
            else
            {
                biosGeometry = Geometry.MakeBiosSafe(geometry, parameters.Capacity);
            }


            DiskAdapterType adapterType = (parameters.AdapterType == DiskAdapterType.None) ? DiskAdapterType.LsiLogicScsi : parameters.AdapterType;
            DiskCreateType createType = (parameters.CreateType == DiskCreateType.None) ? DiskCreateType.MonolithicSparse : parameters.CreateType;

            DescriptorFile baseDescriptor = CreateSimpleDiskDescriptor(geometry, biosGeometry, createType, adapterType);

            return DoInitialize(fileLocator, path, parameters.Capacity, createType, baseDescriptor);
        }

        /// <summary>
        /// Creates a new virtual disk that is a linked clone of an existing disk.
        /// </summary>
        /// <param name="path">The path to the new disk</param>
        /// <param name="type">The type of the new disk</param>
        /// <param name="parent">The disk to clone</param>
        /// <returns>The new virtual disk</returns>
        public static DiskImageFile InitializeDifferencing(string path, DiskCreateType type, string parent)
        {
            if (type != DiskCreateType.MonolithicSparse && type != DiskCreateType.TwoGbMaxExtentSparse && type != DiskCreateType.VmfsSparse)
            {
                throw new ArgumentException("Differencing disks must be sparse", "type");
            }

            using (DiskImageFile parentFile = new DiskImageFile(parent, FileAccess.Read))
            {
                DescriptorFile baseDescriptor = CreateDifferencingDiskDescriptor(type, parentFile, parent);

                FileLocator locator = new LocalFileLocator(Path.GetDirectoryName(path));
                return DoInitialize(locator, Path.GetFileName(path), parentFile.Capacity, type, baseDescriptor);
            }
        }

        /// <summary>
        /// Creates a new virtual disk that is a linked clone of an existing disk.
        /// </summary>
        /// <param name="fileSystem">The file system to create the VMDK on</param>
        /// <param name="path">The path to the new disk</param>
        /// <param name="type">The type of the new disk</param>
        /// <param name="parent">The disk to clone</param>
        /// <returns>The new virtual disk</returns>
        public static DiskImageFile InitializeDifferencing(DiscFileSystem fileSystem, string path, DiskCreateType type, string parent)
        {
            if (type != DiskCreateType.MonolithicSparse && type != DiskCreateType.TwoGbMaxExtentSparse && type != DiskCreateType.VmfsSparse)
            {
                throw new ArgumentException("Differencing disks must be sparse", "type");
            }

            string basePath = Path.GetDirectoryName(path);
            FileLocator locator = new DiscFileLocator(fileSystem, basePath);
            FileLocator parentLocator = locator.GetRelativeLocator(Path.GetDirectoryName(parent));

            using (DiskImageFile parentFile = new DiskImageFile(parentLocator, Path.GetFileName(parent), FileAccess.Read))
            {
                DescriptorFile baseDescriptor = CreateDifferencingDiskDescriptor(type, parentFile, parent);

                return DoInitialize(locator, Path.GetFileName(path), parentFile.Capacity, type, baseDescriptor);
            }
        }

        /// <summary>
        /// Gets an indication as to whether the disk file is sparse.
        /// </summary>
        public override bool IsSparse
        {
            get
            {
                return _descriptor.CreateType == DiskCreateType.MonolithicSparse
                    || _descriptor.CreateType == DiskCreateType.TwoGbMaxExtentSparse
                    || _descriptor.CreateType == DiskCreateType.VmfsSparse;
            }
        }

        /// <summary>
        /// Gets the relative paths to all of the disk's extents.
        /// </summary>
        public IEnumerable<string> ExtentPaths
        {
            get
            {
                foreach (var path in _descriptor.Extents)
                {
                    yield return path.FileName;
                }
            }
        }

        /// <summary>
        /// Indicates if this disk is a linked differencing disk.
        /// </summary>
        internal bool NeedsParent
        {
            get { return _descriptor.ParentContentId != uint.MaxValue; }
        }

        /// <summary>
        /// Gets the location of the parent.
        /// </summary>
        internal string ParentLocation
        {
            get { return _descriptor.ParentFileNameHint; }
        }

        internal uint ContentId
        {
            get { return _descriptor.ContentId; }
        }

        /// <summary>
        /// Gets the capacity of this disk (in bytes).
        /// </summary>
        internal override long Capacity
        {
            get
            {
                long result = 0;
                foreach (var extent in _descriptor.Extents)
                {
                    result += extent.SizeInSectors * Sizes.Sector;
                }
                return result;
            }
        }

        /// <summary>
        /// Gets a <c>FileLocator</c> that can resolve relative paths, or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Typically used to locate parent disks.
        /// </remarks>
        internal override FileLocator RelativeFileLocator
        {
            get { return _fileLocator; }
        }

        /// <summary>
        /// Gets the Geometry of this disk.
        /// </summary>
        internal Geometry Geometry
        {
            get { return _descriptor.DiskGeometry; }
        }

        /// <summary>
        /// Gets the BIOS geometry of this disk.
        /// </summary>
        internal Geometry BiosGeometry
        {
            get { return _descriptor.BiosGeometry; }
        }

        /// <summary>
        /// Gets the 'CreateType' of this disk.
        /// </summary>
        internal DiskCreateType CreateType
        {
            get { return _descriptor.CreateType; }
        }

        /// <summary>
        /// Gets the contents of this disk as a stream.
        /// </summary>
        internal SparseStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            if (_descriptor.ParentContentId == uint.MaxValue)
            {
                if (parent != null && ownsParent == Ownership.Dispose)
                {
                    parent.Dispose();
                }
                parent = null;
            }

            if (parent == null)
            {
                parent = new ZeroStream(Capacity);
                ownsParent = Ownership.Dispose;
            }

            if (_descriptor.Extents.Count == 1)
            {
                if (_monolithicStream != null)
                {
                    return new HostedSparseExtentStream(
                        _monolithicStream,
                        Ownership.None,
                        0,
                        parent,
                        ownsParent);
                }
                else
                {
                    return OpenExtent(_descriptor.Extents[0], 0, parent, ownsParent);
                }
            }
            else
            {
                long extentStart = 0;
                SparseStream[] streams = new SparseStream[_descriptor.Extents.Count];
                for (int i = 0; i < streams.Length; ++i)
                {
                    streams[i] = OpenExtent(_descriptor.Extents[i], extentStart, parent, (i == streams.Length - 1) ? ownsParent : Ownership.None);
                    extentStart += _descriptor.Extents[i].SizeInSectors * Sizes.Sector;
                }
                return new ConcatStream(Ownership.Dispose, streams);
            }
        }

        private static DiskImageFile DoInitialize(FileLocator fileLocator, string file, long capacity, DiskCreateType type, DescriptorFile baseDescriptor)
        {
            if (type == DiskCreateType.MonolithicSparse)
            {
                // MonolithicSparse is a special case, the descriptor is embedded in the file itself...
                using (Stream fs = fileLocator.Open(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    long descriptorStart;
                    CreateExtent(fs, capacity, ExtentType.Sparse, 10 * Sizes.OneKiB, out descriptorStart);

                    ExtentDescriptor extent = new ExtentDescriptor(ExtentAccess.ReadWrite, capacity / Sizes.Sector, ExtentType.Sparse, file, 0);
                    fs.Position = descriptorStart * Sizes.Sector;
                    baseDescriptor.Extents.Add(extent);
                    baseDescriptor.Write(fs);
                }
            }
            else
            {
                ExtentType extentType = CreateTypeToExtentType(type);
                long totalSize = 0;
                List<ExtentDescriptor> extents = new List<ExtentDescriptor>();
                if (type == DiskCreateType.MonolithicFlat || type == DiskCreateType.VmfsSparse || type == DiskCreateType.Vmfs)
                {
                    string adornment = "flat";
                    if(type == DiskCreateType.VmfsSparse)
                    {
                        adornment = string.IsNullOrEmpty(baseDescriptor.ParentFileNameHint) ? "sparse" : "delta";
                    }

                    string fileName = AdornFileName(file, adornment);

                    using(Stream fs = fileLocator.Open(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    {
                        CreateExtent(fs, capacity, extentType);
                        extents.Add(new ExtentDescriptor(ExtentAccess.ReadWrite, capacity / Sizes.Sector, extentType, fileName, 0));
                        totalSize = capacity;
                    }
                }
                else if (type == DiskCreateType.TwoGbMaxExtentFlat || type == DiskCreateType.TwoGbMaxExtentSparse)
                {
                    int i = 1;
                    while (totalSize < capacity)
                    {
                        string adornment;
                        if (type == DiskCreateType.TwoGbMaxExtentSparse)
                        {
                            adornment = string.Format(CultureInfo.InvariantCulture, "s{0:x3}", i);
                        }
                        else
                        {
                            adornment = string.Format(CultureInfo.InvariantCulture, "{0:x6}", i);
                        }

                        string fileName = AdornFileName(file, adornment);

                        using (Stream fs = fileLocator.Open(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                        {
                            long extentSize = Math.Min(2 * Sizes.OneGiB - Sizes.OneMiB, capacity - totalSize);
                            CreateExtent(fs, extentSize, extentType);
                            extents.Add(new ExtentDescriptor(ExtentAccess.ReadWrite, extentSize / Sizes.Sector, extentType, fileName, 0));
                            totalSize += extentSize;
                        }

                        ++i;
                    }
                }
                else
                {
                    throw new NotSupportedException("Creating disks of this type is not supported");
                }

                using (Stream fs = fileLocator.Open(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    baseDescriptor.Extents.AddRange(extents);
                    baseDescriptor.Write(fs);
                }
            }

            return new DiskImageFile(fileLocator, file, FileAccess.ReadWrite);
        }

        private SparseStream OpenExtent(ExtentDescriptor extent, long extentStart, SparseStream parent, Ownership ownsParent)
        {
            FileAccess access = FileAccess.Read;
            FileShare share = FileShare.Read;
            if(extent.Access == ExtentAccess.ReadWrite && _access != FileAccess.Read)
            {
                access = FileAccess.ReadWrite;
                share = FileShare.None;
            }

            if (extent.Type != ExtentType.Sparse && extent.Type != ExtentType.VmfsSparse)
            {
                if (ownsParent == Ownership.Dispose && parent != null)
                {
                    parent.Dispose();
                }
            }

            switch (extent.Type)
            {
                case ExtentType.Flat:
                case ExtentType.Vmfs:
                    return SparseStream.FromStream(
                        _fileLocator.Open(extent.FileName, FileMode.Open, access, share),
                        Ownership.Dispose);

                case ExtentType.Zero:
                    return new ZeroStream(extent.SizeInSectors * Utilities.SectorSize);

                case ExtentType.Sparse:
                    return new HostedSparseExtentStream(
                        _fileLocator.Open(extent.FileName, FileMode.Open, access, share),
                        Ownership.Dispose,
                        extentStart,
                        parent,
                        ownsParent);

                case ExtentType.VmfsSparse:
                    return new ServerSparseExtentStream(
                        _fileLocator.Open(extent.FileName, FileMode.Open, access, share),
                        Ownership.Dispose,
                        extentStart,
                        parent,
                        ownsParent);

                default:
                    throw new NotSupportedException();
            }
        }

        private static void CreateExtent(Stream extentStream, long size, ExtentType type)
        {
            long descriptorStart;
            CreateExtent(extentStream, size, type, 0, out descriptorStart);
        }

        private static void CreateExtent(Stream extentStream, long size, ExtentType type, long descriptorLength, out long descriptorStart)
        {
            if (type == ExtentType.Flat || type == ExtentType.Vmfs)
            {
                extentStream.SetLength(size);
                descriptorStart = 0;
                return;
            }

            if (type == ExtentType.Sparse)
            {
                CreateSparseExtent(extentStream, size, descriptorLength, out descriptorStart);
                return;
            }
            else if(type == ExtentType.VmfsSparse)
            {
                ServerSparseExtentHeader header = CreateServerSparseExtentHeader(size);

                extentStream.Position = 0;
                extentStream.Write(header.GetBytes(), 0, 4 * Sizes.Sector);

                byte[] blankGlobalDirectory = new byte[header.NumGdEntries * 4];
                extentStream.Write(blankGlobalDirectory, 0, blankGlobalDirectory.Length);

                descriptorStart = 0;
                return;
            }
            else
            {
                throw new NotImplementedException("Extent type not implemented");
            }
        }

        internal static ServerSparseExtentHeader CreateServerSparseExtentHeader(long size)
        {
            uint numSectors = (uint)Utilities.Ceil(size, Sizes.Sector);
            uint numGDEntries = (uint)Utilities.Ceil(numSectors * (long)Sizes.Sector, 2 * Sizes.OneMiB);

            ServerSparseExtentHeader header = new ServerSparseExtentHeader();
            header.Capacity = numSectors;
            header.GrainSize = 1;
            header.GdOffset = 4;
            header.NumGdEntries = numGDEntries;
            header.FreeSector = (uint)(header.GdOffset + Utilities.Ceil(numGDEntries * 4, Sizes.Sector));
            return header;
        }

        private static void CreateSparseExtent(Stream extentStream, long size, long descriptorLength, out long descriptorStart)
        {
            // Figure out grain size and number of grain tables, and adjust actual extent size to be a multiple
            // of grain size
            const int gtesPerGt = 512;
            long grainSize = 128;
            int numGrainTables = (int)Utilities.Ceil(size, grainSize * gtesPerGt * Sizes.Sector);

            descriptorLength = Utilities.RoundUp(descriptorLength, Sizes.Sector);
            descriptorStart = 0;
            if (descriptorLength != 0)
            {
                descriptorStart = 1;
            }

            long redundantGrainDirStart = Math.Max(descriptorStart, 1) + Utilities.Ceil(descriptorLength, Sizes.Sector);
            long redundantGrainDirLength = numGrainTables * 4;

            long redundantGrainTablesStart = redundantGrainDirStart + Utilities.Ceil(redundantGrainDirLength, Sizes.Sector);
            long redundantGrainTablesLength = numGrainTables * Utilities.RoundUp(gtesPerGt * 4, Sizes.Sector);

            long grainDirStart = redundantGrainTablesStart + Utilities.Ceil(redundantGrainTablesLength, Sizes.Sector);
            long grainDirLength = numGrainTables * 4;

            long grainTablesStart = grainDirStart + Utilities.Ceil(grainDirLength, Sizes.Sector);
            long grainTablesLength = numGrainTables * Utilities.RoundUp(gtesPerGt * 4, Sizes.Sector);

            long dataStart = Utilities.RoundUp(grainTablesStart + Utilities.Ceil(grainTablesLength, Sizes.Sector), grainSize);

            // Generate the header, and write it
            HostedSparseExtentHeader header = new HostedSparseExtentHeader();
            header.Flags = HostedSparseExtentFlags.ValidLineDetectionTest | HostedSparseExtentFlags.RedundantGrainTable;
            header.Capacity = Utilities.RoundUp(size, grainSize * Sizes.Sector) / Sizes.Sector;
            header.GrainSize = grainSize;
            header.DescriptorOffset = descriptorStart;
            header.DescriptorSize = descriptorLength / Sizes.Sector;
            header.NumGTEsPerGT = gtesPerGt;
            header.RgdOffset = redundantGrainDirStart;
            header.GdOffset = grainDirStart;
            header.Overhead = dataStart;

            extentStream.Position = 0;
            extentStream.Write(header.GetBytes(), 0, Sizes.Sector);


            // Zero-out the descriptor space
            if (descriptorLength > 0)
            {
                byte[] descriptor = new byte[descriptorLength];
                extentStream.Position = descriptorStart * Sizes.Sector;
                extentStream.Write(descriptor, 0, descriptor.Length);
            }


            // Generate the redundant grain dir, and write it
            byte[] grainDir = new byte[numGrainTables * 4];
            for (int i = 0; i < numGrainTables; ++i)
            {
                Utilities.WriteBytesLittleEndian((uint)(redundantGrainTablesStart + (i * Utilities.Ceil(gtesPerGt * 4, Sizes.Sector))), grainDir, i * 4);
            }
            extentStream.Position = redundantGrainDirStart * Sizes.Sector;
            extentStream.Write(grainDir, 0, grainDir.Length);


            // Write out the blank grain tables
            byte[] grainTable = new byte[gtesPerGt * 4];
            for (int i = 0; i < numGrainTables; ++i)
            {
                extentStream.Position = (redundantGrainTablesStart * Sizes.Sector) + (i * Utilities.RoundUp(gtesPerGt * 4, Sizes.Sector));
                extentStream.Write(grainTable, 0, grainTable.Length);
            }


            // Generate the main grain dir, and write it
            for (int i = 0; i < numGrainTables; ++i)
            {
                Utilities.WriteBytesLittleEndian((uint)(grainTablesStart + (i * Utilities.Ceil(gtesPerGt * 4, Sizes.Sector))), grainDir, i * 4);
            }
            extentStream.Position = grainDirStart * Sizes.Sector;
            extentStream.Write(grainDir, 0, grainDir.Length);


            // Write out the blank grain tables
            for (int i = 0; i < numGrainTables; ++i)
            {
                extentStream.Position = (grainTablesStart * Sizes.Sector) + (i * Utilities.RoundUp(gtesPerGt * 4, Sizes.Sector));
                extentStream.Write(grainTable, 0, grainTable.Length);
            }

            // Make sure stream is correct length
            if (extentStream.Length != dataStart * Sizes.Sector)
            {
                extentStream.SetLength(dataStart * Sizes.Sector);
            }
        }

        private void LoadDescriptor(Stream s)
        {
            s.Position = 0;
            byte[] header = Utilities.ReadFully(s, (int)Math.Min(Sizes.Sector, s.Length));
            if (header.Length < Sizes.Sector || Utilities.ToUInt32LittleEndian(header, 0) != HostedSparseExtentHeader.VmdkMagicNumber)
            {
                s.Position = 0;
                _descriptor = new DescriptorFile(s);
                if (_access != FileAccess.Read)
                {
                    _descriptor.ContentId = (uint)_rng.Next();
                    s.Position = 0;
                    _descriptor.Write(s);
                    s.SetLength(s.Position);
                }
            }
            else
            {
                // This is a sparse disk extent, hopefully with embedded descriptor...
                HostedSparseExtentHeader hdr = HostedSparseExtentHeader.Read(header, 0);
                if (hdr.DescriptorOffset != 0)
                {
                    Stream descriptorStream = new SubStream(s, hdr.DescriptorOffset * Sizes.Sector, hdr.DescriptorSize * Sizes.Sector);
                    _descriptor = new DescriptorFile(descriptorStream);
                    if (_access != FileAccess.Read)
                    {
                        _descriptor.ContentId = (uint)_rng.Next();
                        descriptorStream.Position = 0;
                        _descriptor.Write(descriptorStream);
                        byte[] blank = new byte[descriptorStream.Length - descriptorStream.Position];
                        descriptorStream.Write(blank, 0, blank.Length);
                    }
                }
            }
        }

        private static string AdornFileName(string name, string adornment)
        {
            if (!name.EndsWith(".vmdk", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("name must end in .vmdk to be adorned");
            }

            return name.Substring(0, name.Length - 5) + "-" + adornment + ".vmdk";
        }

        private static ExtentType CreateTypeToExtentType(DiskCreateType type)
        {
            switch (type)
            {
                case DiskCreateType.FullDevice:
                case DiskCreateType.MonolithicFlat:
                case DiskCreateType.PartitionedDevice:
                case DiskCreateType.TwoGbMaxExtentFlat:
                    return ExtentType.Flat;

                case DiskCreateType.MonolithicSparse:
                case DiskCreateType.StreamOptimized:
                case DiskCreateType.TwoGbMaxExtentSparse:
                    return ExtentType.Sparse;

                case DiskCreateType.Vmfs:
                    return ExtentType.Vmfs;

                case DiskCreateType.VmfsPassthroughRawDeviceMap:
                    return ExtentType.VmfsRdm;

                case DiskCreateType.VmfsRaw:
                case DiskCreateType.VmfsRawDeviceMap:
                    return ExtentType.VmfsRaw;

                case DiskCreateType.VmfsSparse:
                    return ExtentType.VmfsSparse;

                default:
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unable to convert {0}", type));
            }
        }

        internal static Geometry DefaultGeometry(long diskSize)
        {
            int heads;
            int sectors;

            if (diskSize < Sizes.OneGiB)
            {
                heads = 64;
                sectors = 32;
            }
            else if (diskSize < 2 * Sizes.OneGiB)
            {
                heads = 128;
                sectors = 32;
            }
            else
            {
                heads = 255;
                sectors = 63;
            }

            int cylinders = (int)(diskSize / (heads * sectors * Sizes.Sector));

            return new Geometry(cylinders, heads, sectors);
        }

        internal static DescriptorFile CreateSimpleDiskDescriptor(Geometry geometry, Geometry biosGeometery, DiskCreateType createType, DiskAdapterType adapterType)
        {
            DescriptorFile baseDescriptor = new DescriptorFile();
            baseDescriptor.DiskGeometry = geometry;
            baseDescriptor.BiosGeometry = biosGeometery;
            baseDescriptor.ContentId = (uint)_rng.Next();
            baseDescriptor.CreateType = createType;
            baseDescriptor.UniqueId = Guid.NewGuid();
            baseDescriptor.HardwareVersion = "4";
            baseDescriptor.AdapterType = adapterType;
            return baseDescriptor;
        }

        private static DescriptorFile CreateDifferencingDiskDescriptor(DiskCreateType type, DiskImageFile parent, string parentPath)
        {
            DescriptorFile baseDescriptor = new DescriptorFile();
            baseDescriptor.ContentId = (uint)_rng.Next();
            baseDescriptor.ParentContentId = parent.ContentId;
            baseDescriptor.ParentFileNameHint = parentPath;
            baseDescriptor.CreateType = type;
            return baseDescriptor;
        }

    }
}

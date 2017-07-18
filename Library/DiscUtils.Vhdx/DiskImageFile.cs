//
// Copyright (c) 2008-2012, Kenneth Bell
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Vhdx
{
    /// <summary>
    /// Represents a single .VHDX file.
    /// </summary>
    public sealed class DiskImageFile : VirtualDiskLayer
    {
        /// <summary>
        /// Which VHDX header is active.
        /// </summary>
        private int _activeHeader;

        /// <summary>
        /// Block Allocation Table for disk content.
        /// </summary>
        private Stream _batStream;

        /// <summary>
        /// The object that can be used to locate relative file paths.
        /// </summary>
        private readonly FileLocator _fileLocator;

        /// <summary>
        /// The file name of this VHDX.
        /// </summary>
        private readonly string _fileName;

        /// <summary>
        /// The stream containing the VHDX file.
        /// </summary>
        private Stream _fileStream;

        /// <summary>
        /// Table of all free space in the file.
        /// </summary>
        private FreeSpaceTable _freeSpace;

        /// <summary>
        /// Value of the active VHDX header.
        /// </summary>
        private VhdxHeader _header;

        /// <summary>
        /// The stream containing the logical VHDX content and metadata allowing for log replay.
        /// </summary>
        private Stream _logicalStream;

        /// <summary>
        /// VHDX metadata region content.
        /// </summary>
        private Metadata _metadata;

        /// <summary>
        /// Indicates if this object controls the lifetime of the stream.
        /// </summary>
        private readonly Ownership _ownsStream;

        /// <summary>
        /// The set of VHDX regions.
        /// </summary>
        private RegionTable _regionTable;

        /// <summary>
        /// Initializes a new instance of the DiskImageFile class.
        /// </summary>
        /// <param name="stream">The stream to interpret.</param>
        public DiskImageFile(Stream stream)
        {
            _fileStream = stream;

            Initialize();
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

            Initialize();
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

                Initialize();
            }
            catch
            {
                _fileStream.Dispose();
                throw;
            }
        }

        internal override long Capacity
        {
            get { return (long)_metadata.DiskSize; }
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
            get { return Geometry.FromCapacity(Capacity, (int)_metadata.LogicalSectorSize); }
        }

        /// <summary>
        /// Gets detailed information about the VHDX file.
        /// </summary>
        public DiskImageFileInfo Information
        {
            get
            {
                _fileStream.Position = 0;
                FileHeader fileHeader = StreamUtilities.ReadStruct<FileHeader>(_fileStream);

                _fileStream.Position = 64 * Sizes.OneKiB;
                VhdxHeader vhdxHeader1 = StreamUtilities.ReadStruct<VhdxHeader>(_fileStream);

                _fileStream.Position = 128 * Sizes.OneKiB;
                VhdxHeader vhdxHeader2 = StreamUtilities.ReadStruct<VhdxHeader>(_fileStream);

                LogSequence activeLogSequence = FindActiveLogSequence();

                return new DiskImageFileInfo(fileHeader, vhdxHeader1, vhdxHeader2, _regionTable, _metadata,
                    activeLogSequence);
            }
        }

        /// <summary>
        /// Gets a value indicating if the layer only stores meaningful sectors.
        /// </summary>
        public override bool IsSparse
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the logical sector size of the virtual disk.
        /// </summary>
        public long LogicalSectorSize
        {
            get { return _metadata.LogicalSectorSize; }
        }

        /// <summary>
        /// Gets a value indicating whether the file is a differencing disk.
        /// </summary>
        public override bool NeedsParent
        {
            get { return (_metadata.FileParameters.Flags & FileParametersFlags.HasParent) != 0; }
        }

        /// <summary>
        /// Gets the unique id of the parent disk.
        /// </summary>
        public Guid ParentUniqueId
        {
            get
            {
                if ((_metadata.FileParameters.Flags & FileParametersFlags.HasParent) == 0)
                {
                    return Guid.Empty;
                }

                string parentLinkage;
                if (_metadata.ParentLocator.Entries.TryGetValue("parent_linkage", out parentLinkage))
                {
                    return new Guid(parentLinkage);
                }
                return Guid.Empty;
            }
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
            get { return _header.DataWriteGuid; }
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHDX file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VHDX file.</returns>
        public static DiskImageFile InitializeFixed(Stream stream, Ownership ownsStream, long capacity)
        {
            return InitializeFixed(stream, ownsStream, capacity, null);
        }

        /// <summary>
        /// Initializes a stream as a fixed-sized VHDX file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="geometry">The desired geometry of the new disk, or <c>null</c> for default.</param>
        /// <returns>An object that accesses the stream as a VHDX file.</returns>
        public static DiskImageFile InitializeFixed(Stream stream, Ownership ownsStream, long capacity,
                                                    Geometry geometry)
        {
            InitializeFixedInternal(stream, capacity, geometry);
            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHDX file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <returns>An object that accesses the stream as a VHDX file.</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, Ownership ownsStream, long capacity)
        {
            InitializeDynamicInternal(stream, capacity, FileParameters.DefaultDynamicBlockSize);
            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Initializes a stream as a dynamically-sized VHDX file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="capacity">The desired capacity of the new disk.</param>
        /// <param name="blockSize">The size of each block (unit of allocation).</param>
        /// <returns>An object that accesses the stream as a VHDX file.</returns>
        public static DiskImageFile InitializeDynamic(Stream stream, Ownership ownsStream, long capacity, long blockSize)
        {
            InitializeDynamicInternal(stream, capacity, blockSize);

            return new DiskImageFile(stream, ownsStream);
        }

        /// <summary>
        /// Initializes a stream as a differencing disk VHDX file.
        /// </summary>
        /// <param name="stream">The stream to initialize.</param>
        /// <param name="ownsStream">Indicates if the new instance controls the lifetime of the stream.</param>
        /// <param name="parent">The disk this file is a different from.</param>
        /// <param name="parentAbsolutePath">The full path to the parent disk.</param>
        /// <param name="parentRelativePath">The relative path from the new disk to the parent disk.</param>
        /// <param name="parentModificationTimeUtc">The time the parent disk's file was last modified (from file system).</param>
        /// <returns>An object that accesses the stream as a VHDX file.</returns>
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
        /// Opens an existing region within the VHDX file.
        /// </summary>
        /// <param name="region">Identifier for the region to open.</param>
        /// <returns>A stream containing the region data.</returns>
        /// <remarks>Regions are an extension mechanism in VHDX - with some regions defined by
        /// the VHDX specification to hold metadata and the block allocation data.</remarks>
        public Stream OpenRegion(Guid region)
        {
            RegionEntry metadataRegion = _regionTable.Regions[region];
            return new SubStream(_logicalStream, metadataRegion.FileOffset, metadataRegion.Length);
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

        internal static DiskImageFile InitializeDynamic(FileLocator locator, string path, long capacity, long blockSize)
        {
            DiskImageFile result = null;
            Stream stream = locator.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            try
            {
                InitializeDynamicInternal(stream, capacity, blockSize);
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
            SparseStream theParent = parent;
            Ownership theOwnership = ownsParent;

            if (parent == null)
            {
                theParent = new ZeroStream(Capacity);
                theOwnership = Ownership.Dispose;
            }

            ContentStream contentStream = new ContentStream(SparseStream.FromStream(_logicalStream, Ownership.None),
                _fileStream.CanWrite, _batStream, _freeSpace, _metadata, Capacity, theParent, theOwnership);
            return new AligningStream(contentStream, Ownership.Dispose, (int)_metadata.LogicalSectorSize);
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
                    if (_logicalStream != _fileStream && _logicalStream != null)
                    {
                        _logicalStream.Dispose();
                    }

                    _logicalStream = null;

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
            throw new NotImplementedException();
        }

        private static void InitializeDynamicInternal(Stream stream, long capacity, long blockSize)
        {
            if (blockSize < Sizes.OneMiB || blockSize > Sizes.OneMiB * 256 || !Utilities.IsPowerOfTwo(blockSize))
            {
                throw new ArgumentOutOfRangeException(nameof(blockSize), blockSize,
                    "BlockSize must be a power of 2 between 1MB and 256MB");
            }

            int logicalSectorSize = 512;
            int physicalSectorSize = 4096;
            long chunkRatio = 0x800000L * logicalSectorSize / blockSize;
            long dataBlocksCount = MathUtilities.Ceil(capacity, blockSize);
            long sectorBitmapBlocksCount = MathUtilities.Ceil(dataBlocksCount, chunkRatio);
            long totalBatEntriesDynamic = dataBlocksCount + (dataBlocksCount - 1) / chunkRatio;

            FileHeader fileHeader = new FileHeader { Creator = ".NET DiscUtils" };

            long fileEnd = Sizes.OneMiB;

            VhdxHeader header1 = new VhdxHeader();
            header1.SequenceNumber = 0;
            header1.FileWriteGuid = Guid.NewGuid();
            header1.DataWriteGuid = Guid.NewGuid();
            header1.LogGuid = Guid.Empty;
            header1.LogVersion = 0;
            header1.Version = 1;
            header1.LogLength = (uint)Sizes.OneMiB;
            header1.LogOffset = (ulong)fileEnd;
            header1.CalcChecksum();

            fileEnd += header1.LogLength;

            VhdxHeader header2 = new VhdxHeader(header1);
            header2.SequenceNumber = 1;
            header2.CalcChecksum();

            RegionTable regionTable = new RegionTable();

            RegionEntry metadataRegion = new RegionEntry();
            metadataRegion.Guid = RegionEntry.MetadataRegionGuid;
            metadataRegion.FileOffset = fileEnd;
            metadataRegion.Length = (uint)Sizes.OneMiB;
            metadataRegion.Flags = RegionFlags.Required;
            regionTable.Regions.Add(metadataRegion.Guid, metadataRegion);

            fileEnd += metadataRegion.Length;

            RegionEntry batRegion = new RegionEntry();
            batRegion.Guid = RegionEntry.BatGuid;
            batRegion.FileOffset = 3 * Sizes.OneMiB;
            batRegion.Length = (uint)MathUtilities.RoundUp(totalBatEntriesDynamic * 8, Sizes.OneMiB);
            batRegion.Flags = RegionFlags.Required;
            regionTable.Regions.Add(batRegion.Guid, batRegion);

            fileEnd += batRegion.Length;

            stream.Position = 0;
            StreamUtilities.WriteStruct(stream, fileHeader);

            stream.Position = 64 * Sizes.OneKiB;
            StreamUtilities.WriteStruct(stream, header1);

            stream.Position = 128 * Sizes.OneKiB;
            StreamUtilities.WriteStruct(stream, header2);

            stream.Position = 192 * Sizes.OneKiB;
            StreamUtilities.WriteStruct(stream, regionTable);

            stream.Position = 256 * Sizes.OneKiB;
            StreamUtilities.WriteStruct(stream, regionTable);

            // Set stream to min size
            stream.Position = fileEnd - 1;
            stream.WriteByte(0);

            // Metadata
            FileParameters fileParams = new FileParameters
            {
                BlockSize = (uint)blockSize,
                Flags = FileParametersFlags.None
            };
            ParentLocator parentLocator = new ParentLocator();

            Stream metadataStream = new SubStream(stream, metadataRegion.FileOffset, metadataRegion.Length);
            Metadata metadata = Metadata.Initialize(metadataStream, fileParams, (ulong)capacity,
                (uint)logicalSectorSize, (uint)physicalSectorSize, null);
        }

        private static void InitializeDifferencingInternal(Stream stream, DiskImageFile parent,
                                                           string parentAbsolutePath, string parentRelativePath, DateTime parentModificationTimeUtc)
        {
            throw new NotImplementedException();
        }

        private void Initialize()
        {
            _fileStream.Position = 0;
            FileHeader fileHeader = StreamUtilities.ReadStruct<FileHeader>(_fileStream);
            if (!fileHeader.IsValid)
            {
                throw new IOException("Invalid VHDX file - file signature mismatch");
            }

            _freeSpace = new FreeSpaceTable(_fileStream.Length);

            ReadHeaders();

            ReplayLog();

            ReadRegionTable();

            ReadMetadata();

            _batStream = OpenRegion(RegionTable.BatGuid);
            _freeSpace.Reserve(BatControlledFileExtents());

            // Indicate the file is open for modification
            if (_fileStream.CanWrite)
            {
                _header.FileWriteGuid = Guid.NewGuid();
                WriteHeader();
            }
        }

        private IEnumerable<StreamExtent> BatControlledFileExtents()
        {
            _batStream.Position = 0;
            byte[] batData = StreamUtilities.ReadExact(_batStream, (int)_batStream.Length);

            uint blockSize = _metadata.FileParameters.BlockSize;
            long chunkSize = (1L << 23) * _metadata.LogicalSectorSize;
            int chunkRatio = (int)(chunkSize / _metadata.FileParameters.BlockSize);

            List<StreamExtent> extents = new List<StreamExtent>();
            for (int i = 0; i < batData.Length; i += 8)
            {
                ulong entry = EndianUtilities.ToUInt64LittleEndian(batData, i);
                long filePos = (long)((entry >> 20) & 0xFFFFFFFFFFF) * Sizes.OneMiB;
                if (filePos != 0)
                {
                    if (i % ((chunkRatio + 1) * 8) == chunkRatio * 8)
                    {
                        // This is a sector bitmap block (always 1MB in size)
                        extents.Add(new StreamExtent(filePos, Sizes.OneMiB));
                    }
                    else
                    {
                        extents.Add(new StreamExtent(filePos, blockSize));
                    }
                }
            }

            extents.Sort();

            return extents;
        }

        private void ReadMetadata()
        {
            Stream regionStream = OpenRegion(RegionTable.MetadataRegionGuid);
            _metadata = new Metadata(regionStream);
        }

        private void ReplayLog()
        {
            _freeSpace.Reserve((long)_header.LogOffset, _header.LogLength);

            _logicalStream = _fileStream;

            // If log is empty, skip.
            if (_header.LogGuid == Guid.Empty)
            {
                return;
            }

            LogSequence activeLogSequence = FindActiveLogSequence();

            if (activeLogSequence == null || activeLogSequence.Count == 0)
            {
                throw new IOException("Unable to replay VHDX log, suspected corrupt VHDX file");
            }

            if (activeLogSequence.Head.FlushedFileOffset > (ulong)_logicalStream.Length)
            {
                throw new IOException("truncated VHDX file found while replaying log");
            }

            if (activeLogSequence.Count > 1 || !activeLogSequence.Head.IsEmpty)
            {
                // However, have seen VHDX with a non-empty log with no data to replay.  These are
                // 'safe' to open.
                if (!_fileStream.CanWrite)
                {
                    SnapshotStream replayStream = new SnapshotStream(_fileStream, Ownership.None);
                    replayStream.Snapshot();
                    _logicalStream = replayStream;
                }
                foreach (LogEntry logEntry in activeLogSequence)
                {
                    if (logEntry.LogGuid != _header.LogGuid)
                        throw new IOException("Invalid log entry in VHDX log, suspected currupt VHDX file");
                    if (logEntry.IsEmpty) continue;
                    logEntry.Replay(_logicalStream);
                }
                _logicalStream.Seek((long)activeLogSequence.Head.LastFileOffset, SeekOrigin.Begin);
            }
        }

        private LogSequence FindActiveLogSequence()
        {
            using (
                Stream logStream =
                    new CircularStream(new SubStream(_fileStream, (long)_header.LogOffset, _header.LogLength),
                        Ownership.Dispose))
            {
                LogSequence candidateActiveSequence = new LogSequence();
                LogEntry logEntry = null;

                long oldTail;
                long currentTail = 0;

                do
                {
                    oldTail = currentTail;

                    logStream.Position = currentTail;
                    LogSequence currentSequence = new LogSequence();

                    while (LogEntry.TryRead(logStream, out logEntry)
                           && logEntry.LogGuid == _header.LogGuid
                           && (currentSequence.Count == 0
                               || logEntry.SequenceNumber == currentSequence.Head.SequenceNumber + 1))
                    {
                        currentSequence.Add(logEntry);
                        logEntry = null;
                    }

                    if (currentSequence.Count > 0
                        && currentSequence.Contains(currentSequence.Head.Tail)
                        && currentSequence.HigherSequenceThan(candidateActiveSequence))
                    {
                        candidateActiveSequence = currentSequence;
                    }

                    if (currentSequence.Count == 0)
                    {
                        currentTail += LogEntry.LogSectorSize;
                    }
                    else
                    {
                        currentTail = currentSequence.Head.Position + LogEntry.LogSectorSize;
                    }

                    currentTail = currentTail % logStream.Length;
                } while (currentTail > oldTail);

                return candidateActiveSequence;
            }
        }

        private void ReadRegionTable()
        {
            _fileStream.Position = 192 * Sizes.OneKiB;
            _regionTable = StreamUtilities.ReadStruct<RegionTable>(_fileStream);
            foreach (RegionEntry entry in _regionTable.Regions.Values)
            {
                if ((entry.Flags & RegionFlags.Required) != 0)
                {
                    if (entry.Guid != RegionTable.BatGuid && entry.Guid != RegionTable.MetadataRegionGuid)
                    {
                        throw new IOException("Invalid VHDX file - unrecognised required region");
                    }
                }

                _freeSpace.Reserve(entry.FileOffset, entry.Length);
            }
        }

        private void ReadHeaders()
        {
            _freeSpace.Reserve(0, Sizes.OneMiB);

            _activeHeader = 0;

            _fileStream.Position = 64 * Sizes.OneKiB;
            VhdxHeader vhdxHeader1 = StreamUtilities.ReadStruct<VhdxHeader>(_fileStream);
            if (vhdxHeader1.IsValid)
            {
                _header = vhdxHeader1;
                _activeHeader = 1;
            }

            _fileStream.Position = 128 * Sizes.OneKiB;
            VhdxHeader vhdxHeader2 = StreamUtilities.ReadStruct<VhdxHeader>(_fileStream);
            if (vhdxHeader2.IsValid && (_activeHeader == 0 || _header.SequenceNumber < vhdxHeader2.SequenceNumber))
            {
                _header = vhdxHeader2;
                _activeHeader = 2;
            }

            if (_activeHeader == 0)
            {
                throw new IOException("Invalid VHDX file - no valid VHDX headers found");
            }
        }

        private void WriteHeader()
        {
            long otherPos;

            _header.SequenceNumber++;
            _header.CalcChecksum();

            if (_activeHeader == 1)
            {
                _fileStream.Position = 128 * Sizes.OneKiB;
                otherPos = 64 * Sizes.OneKiB;
            }
            else
            {
                _fileStream.Position = 64 * Sizes.OneKiB;
                otherPos = 128 * Sizes.OneKiB;
            }

            StreamUtilities.WriteStruct(_fileStream, _header);
            _fileStream.Flush();

            _header.SequenceNumber++;
            _header.CalcChecksum();

            _fileStream.Position = otherPos;
            StreamUtilities.WriteStruct(_fileStream, _header);
            _fileStream.Flush();
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

            List<string> paths = new List<string>();

            ParentLocator locator = _metadata.ParentLocator;
            string value;

            if (locator.Entries.TryGetValue("relative_path", out value))
            {
                paths.Add(fileLocator.ResolveRelativePath(value));
            }

            if (locator.Entries.TryGetValue("volume_path", out value))
            {
                paths.Add(value);
            }

            if (locator.Entries.TryGetValue("absolute_win32_path", out value))
            {
                paths.Add(value);
            }

            return paths.ToArray();
        }
    }
}
//
// Copyright (c) 2008-2013, Kenneth Bell
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
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    /// <summary>
    /// Creates new VHD disks by wrapping existing streams.
    /// </summary>
    /// <remarks>Using this method for creating virtual disks avoids consuming
    /// large amounts of memory, or going via the local file system when the aim
    /// is simply to present a VHD version of an existing disk.</remarks>
    public sealed class DiskBuilder : DiskImageBuilder
    {
        private long _blockSize = 32 * Sizes.OneMiB;

        /// <summary>
        /// The VHDX block size, or <c>0</c> (indicating default).
        /// </summary>
        public long BlockSize
        {
            get { return _blockSize; }
            set
            {
                if (value % Sizes.OneMiB != 0)
                {
                    throw new ArgumentException("BlockSize must be a multiple of 1MB", nameof(value));
                }

                _blockSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the type of VHDX file to build.
        /// </summary>
        public DiskType DiskType { get; set; }

        /// <summary>
        /// Initiates the build process.
        /// </summary>
        /// <param name="baseName">The base name for the VHDX, for example 'foo' to create 'foo.vhdx'.</param>
        /// <returns>A set of one or more logical files that constitute the virtual disk.  The first file is
        /// the 'primary' file that is normally attached to VMs.</returns>
        public override DiskImageFileSpecification[] Build(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                throw new ArgumentException("Invalid base file name", nameof(baseName));
            }

            if (Content == null)
            {
                throw new InvalidOperationException("No content stream specified");
            }

            DiskImageFileSpecification fileSpec = new DiskImageFileSpecification(baseName + ".vhdx",
                new DiskStreamBuilder(Content, DiskType, BlockSize));

            return new[] { fileSpec };
        }

        private class DiskStreamBuilder : StreamBuilder
        {
            private readonly long _blockSize;
            private readonly SparseStream _content;
            private readonly DiskType _diskType;

            public DiskStreamBuilder(SparseStream content, DiskType diskType, long blockSize)
            {
                _content = content;
                _diskType = diskType;
                _blockSize = blockSize;
            }

            protected override List<BuilderExtent> FixExtents(out long totalLength)
            {
                if (_diskType != DiskType.Dynamic)
                {
                    throw new NotSupportedException("Creation of only dynamic disks currently implemented");
                }

                List<BuilderExtent> extents = new List<BuilderExtent>();

                int logicalSectorSize = 512;
                int physicalSectorSize = 4096;
                long chunkRatio = 0x800000L * logicalSectorSize / _blockSize;
                long dataBlocksCount = MathUtilities.Ceil(_content.Length, _blockSize);
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
                batRegion.FileOffset = fileEnd;
                batRegion.Length = (uint)MathUtilities.RoundUp(totalBatEntriesDynamic * 8, Sizes.OneMiB);
                batRegion.Flags = RegionFlags.Required;
                regionTable.Regions.Add(batRegion.Guid, batRegion);

                fileEnd += batRegion.Length;

                extents.Add(ExtentForStruct(fileHeader, 0));
                extents.Add(ExtentForStruct(header1, 64 * Sizes.OneKiB));
                extents.Add(ExtentForStruct(header2, 128 * Sizes.OneKiB));
                extents.Add(ExtentForStruct(regionTable, 192 * Sizes.OneKiB));
                extents.Add(ExtentForStruct(regionTable, 256 * Sizes.OneKiB));

                // Metadata
                FileParameters fileParams = new FileParameters
                {
                    BlockSize = (uint)_blockSize,
                    Flags = FileParametersFlags.None
                };
                ParentLocator parentLocator = new ParentLocator();

                byte[] metadataBuffer = new byte[metadataRegion.Length];
                MemoryStream metadataStream = new MemoryStream(metadataBuffer);
                Metadata.Initialize(metadataStream, fileParams, (ulong)_content.Length, (uint)logicalSectorSize,
                    (uint)physicalSectorSize, null);
                extents.Add(new BuilderBufferExtent(metadataRegion.FileOffset, metadataBuffer));

                List<Range<long, long>> presentBlocks =
                    new List<Range<long, long>>(StreamExtent.Blocks(_content.Extents, _blockSize));

                // BAT
                BlockAllocationTableBuilderExtent batExtent = new BlockAllocationTableBuilderExtent(
                    batRegion.FileOffset, batRegion.Length, presentBlocks, fileEnd, _blockSize, chunkRatio);
                extents.Add(batExtent);

                // Stream contents
                foreach (Range<long, long> range in presentBlocks)
                {
                    long substreamStart = range.Offset * _blockSize;
                    long substreamCount = Math.Min(_content.Length - substreamStart, range.Count * _blockSize);

                    SubStream dataSubStream = new SubStream(_content, substreamStart, substreamCount);
                    BuilderSparseStreamExtent dataExtent = new BuilderSparseStreamExtent(fileEnd, dataSubStream);
                    extents.Add(dataExtent);

                    fileEnd += range.Count * _blockSize;
                }

                totalLength = fileEnd;

                return extents;
            }

            private static BuilderExtent ExtentForStruct(IByteArraySerializable structure, long position)
            {
                byte[] buffer = new byte[structure.Size];
                structure.WriteTo(buffer, 0);
                return new BuilderBufferExtent(position, buffer);
            }
        }

        private class BlockAllocationTableBuilderExtent : BuilderExtent
        {
            private byte[] _batData;
            private readonly List<Range<long, long>> _blocks;
            private readonly long _blockSize;
            private readonly long _chunkRatio;
            private readonly long _dataStart;

            public BlockAllocationTableBuilderExtent(long start, long length, List<Range<long, long>> blocks,
                                                     long dataStart, long blockSize, long chunkRatio)
                : base(start, length)
            {
                _blocks = blocks;
                _dataStart = dataStart;
                _blockSize = blockSize;
                _chunkRatio = chunkRatio;
            }

            public override void Dispose()
            {
                _batData = null;
            }

            public override void PrepareForRead()
            {
                _batData = new byte[Length];

                long fileOffset = _dataStart;
                BatEntry entry = new BatEntry();

                foreach (Range<long, long> range in _blocks)
                {
                    for (long block = range.Offset; block < range.Offset + range.Count; ++block)
                    {
                        long chunk = block / _chunkRatio;
                        long chunkOffset = block % _chunkRatio;
                        long batIndex = chunk * (_chunkRatio + 1) + chunkOffset;

                        entry.FileOffsetMB = fileOffset / Sizes.OneMiB;
                        entry.PayloadBlockStatus = PayloadBlockStatus.FullyPresent;
                        entry.WriteTo(_batData, (int)(batIndex * 8));

                        fileOffset += _blockSize;
                    }
                }
            }

            public override int Read(long diskOffset, byte[] block, int offset, int count)
            {
                int start = (int)Math.Min(diskOffset - Start, _batData.Length);
                int numRead = Math.Min(count, _batData.Length - start);

                Array.Copy(_batData, start, block, offset, numRead);

                return numRead;
            }

            public override void DisposeReadState()
            {
                _batData = null;
            }
        }
    }
}
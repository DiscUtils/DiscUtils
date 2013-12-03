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

namespace DiscUtils.Vhdx
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Creates new VHD disks by wrapping existing streams.
    /// </summary>
    /// <remarks>Using this method for creating virtual disks avoids consuming
    /// large amounts of memory, or going via the local file system when the aim
    /// is simply to present a VHD version of an existing disk.</remarks>
    public sealed class DiskBuilder : DiskImageBuilder
    {
        /// <summary>
        /// Gets or sets the type of VHDX file to build.
        /// </summary>
        public DiskType DiskType { get; set; }

        public override DiskImageFileSpecification[] Build(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                throw new ArgumentException("Invalid base file name", "baseName");
            }

            if (Content == null)
            {
                throw new InvalidOperationException("No content stream specified");
            }

            DiskImageFileSpecification fileSpec = new DiskImageFileSpecification(baseName + ".vhdx", new DiskStreamBuilder(Content, DiskType));

            return new DiskImageFileSpecification[] { fileSpec };
        }

        private class DiskStreamBuilder : StreamBuilder
        {
            private SparseStream _content;
            private DiskType _diskType;

            public DiskStreamBuilder(SparseStream content, DiskType diskType)
            {
                _content = content;
                _diskType = diskType;
            }

            internal override List<BuilderExtent> FixExtents(out long totalLength)
            {
                if (_diskType != DiskType.Dynamic)
                {
                    throw new NotSupportedException("Creation of only dynamic disks currently implemented");
                }

                List<BuilderExtent> extents = new List<BuilderExtent>();

                long blockSize = 32 * Sizes.OneMiB;
                int logicalSectorSize = 512;
                int physicalSectorSize = 4096;
                long chunkRatio = (0x800000L * logicalSectorSize) / blockSize;
                long dataBlocksCount = Utilities.Ceil(_content.Length, blockSize);
                long sectorBitmapBlocksCount = Utilities.Ceil(dataBlocksCount, chunkRatio);
                long totalBatEntriesDynamic = dataBlocksCount + ((dataBlocksCount - 1) / chunkRatio);

                FileHeader fileHeader = new FileHeader() { Creator = ".NET DiscUtils" };

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
                batRegion.Length = (uint)Utilities.RoundUp(totalBatEntriesDynamic * 8, Sizes.OneMiB);
                batRegion.Flags = RegionFlags.Required;
                regionTable.Regions.Add(batRegion.Guid, batRegion);

                fileEnd += batRegion.Length;

                extents.Add(ExtentForStruct(fileHeader, 0));
                extents.Add(ExtentForStruct(header1, 64 * Sizes.OneKiB));
                extents.Add(ExtentForStruct(header2, 128 * Sizes.OneKiB));
                extents.Add(ExtentForStruct(regionTable, 192 * Sizes.OneKiB));
                extents.Add(ExtentForStruct(regionTable, 256 * Sizes.OneKiB));

                // Metadata
                FileParameters fileParams = new FileParameters() { BlockSize = (uint)blockSize, Flags = FileParametersFlags.None };
                ParentLocator parentLocator = new ParentLocator();

                byte[] metadataBuffer = new byte[metadataRegion.Length];
                MemoryStream metadataStream = new MemoryStream(metadataBuffer);
                Metadata.Initialize(metadataStream, fileParams, (ulong)_content.Length, (uint)logicalSectorSize, (uint)physicalSectorSize, null);
                extents.Add(new BuilderBufferExtent(metadataRegion.FileOffset, metadataBuffer));

                // TODO: BAT & Stream contents

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
    }
}

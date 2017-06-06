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
using DiscUtils.Streams;

namespace DiscUtils.Vmdk
{
    internal sealed class MonolithicSparseExtentBuilder : StreamBuilder
    {
        private readonly SparseStream _content;
        private readonly DescriptorFile _descriptor;

        public MonolithicSparseExtentBuilder(SparseStream content, DescriptorFile descriptor)
        {
            _content = content;
            _descriptor = descriptor;
        }

        protected override List<BuilderExtent> FixExtents(out long totalLength)
        {
            List<BuilderExtent> extents = new List<BuilderExtent>();

            MemoryStream descriptorStream = new MemoryStream();
            _descriptor.Write(descriptorStream);

            // Figure out grain size and number of grain tables, and adjust actual extent size to be a multiple
            // of grain size
            const int GtesPerGt = 512;
            long grainSize = 128;
            int numGrainTables = (int)MathUtilities.Ceil(_content.Length, grainSize * GtesPerGt * Sizes.Sector);

            long descriptorLength = 10 * Sizes.OneKiB; // MathUtilities.RoundUp(descriptorStream.Length, Sizes.Sector);
            long descriptorStart = 0;
            if (descriptorLength != 0)
            {
                descriptorStart = 1;
            }

            long redundantGrainDirStart = Math.Max(descriptorStart, 1) + MathUtilities.Ceil(descriptorLength, Sizes.Sector);
            long redundantGrainDirLength = numGrainTables * 4;

            long redundantGrainTablesStart = redundantGrainDirStart +
                                             MathUtilities.Ceil(redundantGrainDirLength, Sizes.Sector);
            long redundantGrainTablesLength = numGrainTables * MathUtilities.RoundUp(GtesPerGt * 4, Sizes.Sector);

            long grainDirStart = redundantGrainTablesStart + MathUtilities.Ceil(redundantGrainTablesLength, Sizes.Sector);
            long grainDirLength = numGrainTables * 4;

            long grainTablesStart = grainDirStart + MathUtilities.Ceil(grainDirLength, Sizes.Sector);
            long grainTablesLength = numGrainTables * MathUtilities.RoundUp(GtesPerGt * 4, Sizes.Sector);

            long dataStart = MathUtilities.RoundUp(grainTablesStart + MathUtilities.Ceil(grainTablesLength, Sizes.Sector),
                grainSize);

            // Generate the header, and write it
            HostedSparseExtentHeader header = new HostedSparseExtentHeader();
            header.Flags = HostedSparseExtentFlags.ValidLineDetectionTest | HostedSparseExtentFlags.RedundantGrainTable;
            header.Capacity = MathUtilities.RoundUp(_content.Length, grainSize * Sizes.Sector) / Sizes.Sector;
            header.GrainSize = grainSize;
            header.DescriptorOffset = descriptorStart;
            header.DescriptorSize = descriptorLength / Sizes.Sector;
            header.NumGTEsPerGT = GtesPerGt;
            header.RgdOffset = redundantGrainDirStart;
            header.GdOffset = grainDirStart;
            header.Overhead = dataStart;

            extents.Add(new BuilderBytesExtent(0, header.GetBytes()));

            // The descriptor extent
            if (descriptorLength > 0)
            {
                extents.Add(new BuilderStreamExtent(descriptorStart * Sizes.Sector, descriptorStream));
            }

            // The grain directory extents
            extents.Add(new GrainDirectoryExtent(redundantGrainDirStart * Sizes.Sector, redundantGrainTablesStart,
                numGrainTables, GtesPerGt));
            extents.Add(new GrainDirectoryExtent(grainDirStart * Sizes.Sector, grainTablesStart, numGrainTables, GtesPerGt));

            // For each graintable span that's present...
            long dataSectorsUsed = 0;
            long gtSpan = GtesPerGt * grainSize * Sizes.Sector;
            foreach (Range<long, long> gtRange in StreamExtent.Blocks(_content.Extents, grainSize * GtesPerGt * Sizes.Sector))
            {
                for (long i = 0; i < gtRange.Count; ++i)
                {
                    int gt = (int)(gtRange.Offset + i);

                    SubStream gtStream = new SubStream(_content, gt * gtSpan,
                        Math.Min(gtSpan, _content.Length - gt * gtSpan));

                    GrainTableDataExtent dataExtent =
                        new GrainTableDataExtent((dataStart + dataSectorsUsed) * Sizes.Sector, gtStream, grainSize);
                    extents.Add(dataExtent);

                    extents.Add(new GrainTableExtent(GrainTablePosition(redundantGrainTablesStart, gt, GtesPerGt),
                        gtStream, dataStart + dataSectorsUsed, GtesPerGt, grainSize));
                    extents.Add(new GrainTableExtent(GrainTablePosition(grainTablesStart, gt, GtesPerGt), gtStream,
                        dataStart + dataSectorsUsed, GtesPerGt, grainSize));

                    dataSectorsUsed += dataExtent.Length / Sizes.Sector;
                }
            }

            totalLength = (dataStart + dataSectorsUsed) * Sizes.Sector;
            return extents;
        }

        private static long GrainTablePosition(long grainTablesStart, long grainTable, int gtesPerGt)
        {
            return grainTablesStart * Sizes.Sector + grainTable * MathUtilities.RoundUp(gtesPerGt * 4, Sizes.Sector);
        }

        private class GrainDirectoryExtent : BuilderBytesExtent
        {
            private readonly long _grainTablesStart;
            private readonly int _gtesPerGt;
            private readonly int _numGrainTables;

            public GrainDirectoryExtent(long start, long grainTablesStart, int numGrainTables, int gtesPerGt)
                : base(start, MathUtilities.RoundUp(numGrainTables * 4, Sizes.Sector))
            {
                _grainTablesStart = grainTablesStart;
                _numGrainTables = numGrainTables;
                _gtesPerGt = gtesPerGt;
            }

            public override void PrepareForRead()
            {
                _data = new byte[Length];
                for (int i = 0; i < _numGrainTables; ++i)
                {
                    EndianUtilities.WriteBytesLittleEndian(
                        (uint)(_grainTablesStart + i * MathUtilities.Ceil(_gtesPerGt * 4, Sizes.Sector)), _data, i * 4);
                }
            }

            public override void DisposeReadState()
            {
                _data = null;
            }
        }

        private class GrainTableExtent : BuilderBytesExtent
        {
            private readonly SparseStream _content;
            private readonly long _dataStart;
            private readonly long _grainSize;
            private readonly int _gtesPerGt;

            public GrainTableExtent(long start, SparseStream content, long dataStart, int gtesPerGt, long grainSize)
                : base(start, gtesPerGt * 4)
            {
                _content = content;
                _grainSize = grainSize;
                _gtesPerGt = gtesPerGt;
                _dataStart = dataStart;
            }

            public override void PrepareForRead()
            {
                _data = new byte[_gtesPerGt * 4];

                long gtSpan = _gtesPerGt * _grainSize * Sizes.Sector;
                long sectorsAllocated = 0;

                foreach (Range<long, long> block in StreamExtent.Blocks(_content.Extents, _grainSize * Sizes.Sector))
                {
                    for (int i = 0; i < block.Count; ++i)
                    {
                        EndianUtilities.WriteBytesLittleEndian((uint)(_dataStart + sectorsAllocated), _data,
                            (int)((block.Offset + i) * 4));
                        sectorsAllocated += _grainSize;
                    }
                }
            }

            public override void DisposeReadState()
            {
                _data = null;
            }
        }

        private class GrainTableDataExtent : BuilderExtent
        {
            private SparseStream _content;
            private readonly Ownership _contentOwnership;
            private int[] _grainMapOffsets;
            private Range<long, long>[] _grainMapRanges;
            private readonly long _grainSize;

            public GrainTableDataExtent(long start, SparseStream content, long grainSize)
                : this(start, content, Ownership.None, grainSize) {}

            public GrainTableDataExtent(long start, SparseStream content, Ownership contentOwnership, long grainSize)
                : base(start, SectorsPresent(content, grainSize) * Sizes.Sector)
            {
                _content = content;
                _contentOwnership = contentOwnership;
                _grainSize = grainSize;
            }

            public override void Dispose()
            {
                if (_content != null && _contentOwnership != Ownership.Dispose)
                {
                    _content.Dispose();
                    _content = null;
                }
            }

            public override void PrepareForRead()
            {
                long outputGrain = 0;

                _grainMapOffsets = new int[Length / (_grainSize * Sizes.Sector)];
                _grainMapRanges = new Range<long, long>[_grainMapOffsets.Length];
                foreach (Range<long, long> grainRange in StreamExtent.Blocks(_content.Extents, _grainSize * Sizes.Sector))
                {
                    for (int i = 0; i < grainRange.Count; ++i)
                    {
                        _grainMapOffsets[outputGrain] = i;
                        _grainMapRanges[outputGrain] = grainRange;
                        outputGrain++;
                    }
                }
            }

            public override int Read(long diskOffset, byte[] block, int offset, int count)
            {
                long start = diskOffset - Start;
                long grainSizeBytes = _grainSize * Sizes.Sector;

                long outputGrain = start / grainSizeBytes;
                long outputGrainOffset = start % grainSizeBytes;

                long grainStart = (_grainMapRanges[outputGrain].Offset + _grainMapOffsets[outputGrain]) * grainSizeBytes;
                long maxRead = (_grainMapRanges[outputGrain].Count - _grainMapOffsets[outputGrain]) * grainSizeBytes;

                long readStart = grainStart + outputGrainOffset;
                int toRead = (int)Math.Min(count, maxRead - outputGrainOffset);

                if (readStart > _content.Length)
                {
                    Array.Clear(block, offset, toRead);
                    return toRead;
                }
                _content.Position = readStart;
                return _content.Read(block, offset, toRead);
            }

            public override void DisposeReadState()
            {
                _grainMapOffsets = null;
                _grainMapRanges = null;
            }

            private static long SectorsPresent(SparseStream content, long grainSize)
            {
                long total = 0;

                foreach (Range<long, long> grainRange in StreamExtent.Blocks(content.Extents, grainSize * Sizes.Sector))
                {
                    total += grainRange.Count * grainSize;
                }

                return total;
            }
        }
    }
}
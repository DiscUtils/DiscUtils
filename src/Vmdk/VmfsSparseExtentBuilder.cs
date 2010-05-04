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
using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Vmdk
{
    internal sealed class VmfsSparseExtentBuilder : StreamBuilder
    {
        private SparseStream _content;

        public VmfsSparseExtentBuilder(SparseStream content)
        {
            _content = content;
        }

        internal override List<BuilderExtent> FixExtents(out long totalLength)
        {
            List<BuilderExtent> extents = new List<BuilderExtent>();


            ServerSparseExtentHeader header = DiskImageFile.CreateServerSparseExtentHeader(_content.Length);
            GlobalDirectoryExtent gdExtent = new GlobalDirectoryExtent(header);

            long grainTableStart = (header.GdOffset * Sizes.Sector) + gdExtent.Length;
            long grainTableCoverage = header.NumGTEsPerGT * header.GrainSize * Sizes.Sector;

            foreach (var grainTableRange in StreamExtent.Blocks(_content.Extents, grainTableCoverage))
            {
                for (int i = 0; i < grainTableRange.Count; ++i)
                {
                    long grainTable = grainTableRange.Offset + i;
                    long dataStart = grainTable * grainTableCoverage;
                    GrainTableExtent gtExtent = new GrainTableExtent(grainTableStart, new SubStream(_content, dataStart, Math.Min(grainTableCoverage, _content.Length - dataStart)), header);
                    extents.Add(gtExtent);
                    gdExtent.SetEntry((int)grainTable, (uint)(grainTableStart / Sizes.Sector));

                    grainTableStart += gtExtent.Length;
                }
            }

            extents.Insert(0, gdExtent);

            header.FreeSector = (uint)(grainTableStart / Sizes.Sector);

            byte[] buffer = header.GetBytes();
            extents.Insert(0, new BuilderBufferExtent(0, buffer));

            totalLength = grainTableStart;

            return extents;
        }


        private class GlobalDirectoryExtent : BuilderExtent
        {
            private byte[] _buffer;
            private MemoryStream _streamView;

            public GlobalDirectoryExtent(ServerSparseExtentHeader header)
                : base(header.GdOffset * Sizes.Sector, Utilities.RoundUp(header.NumGdEntries * 4, Sizes.Sector))
            {
                _buffer = new byte[Length];
            }

            public void SetEntry(int index, uint grainTableSector)
            {
                Utilities.WriteBytesLittleEndian(grainTableSector, _buffer, index * 4);
            }

            internal override void PrepareForRead()
            {
                _streamView = new MemoryStream(_buffer, 0, _buffer.Length, false);
            }

            internal override int Read(long diskOffset, byte[] block, int offset, int count)
            {
                _streamView.Position = diskOffset - Start;
                return _streamView.Read(block, offset, count);
            }

            internal override void DisposeReadState()
            {
                _streamView = null;
            }
        }

        private class GrainTableExtent : BuilderExtent
        {
            private SparseStream _content;
            private ServerSparseExtentHeader _header;

            private MemoryStream _grainTableStream;
            private List<long> _grainMapping;
            private List<long> _grainContiguousRangeMapping;

            public GrainTableExtent(long outputStart, SparseStream content, ServerSparseExtentHeader header)
                : base(outputStart, CalcSize(content, header))
            {
                _content = content;
                _header = header;
            }

            internal override void PrepareForRead()
            {
                byte[] grainTable = new byte[Utilities.RoundUp(_header.NumGTEsPerGT * 4, Sizes.Sector)];

                long dataSector = (Start + grainTable.Length) / Sizes.Sector;

                _grainMapping = new List<long>();
                _grainContiguousRangeMapping = new List<long>();
                foreach (var grainRange in StreamExtent.Blocks(_content.Extents, _header.GrainSize * Sizes.Sector))
                {
                    for (int i = 0; i < grainRange.Count; ++i)
                    {
                        Utilities.WriteBytesLittleEndian((uint)dataSector, grainTable, (int)(4 * (grainRange.Offset + i)));
                        dataSector += _header.GrainSize;
                        _grainMapping.Add(grainRange.Offset + i);
                        _grainContiguousRangeMapping.Add(grainRange.Count - i);
                    }
                }

                _grainTableStream = new MemoryStream(grainTable, 0, grainTable.Length, false);
            }

            internal override int Read(long diskOffset, byte[] block, int offset, int count)
            {
                long relOffset = diskOffset - Start;

                if (relOffset < _grainTableStream.Length)
                {
                    _grainTableStream.Position = relOffset;
                    return _grainTableStream.Read(block, offset, count);
                }
                else
                {
                    long grainSize = (_header.GrainSize * Sizes.Sector);
                    int grainIdx = (int)((relOffset - _grainTableStream.Length) / grainSize);
                    long grainOffset = (relOffset - _grainTableStream.Length) - (grainIdx * grainSize);

                    int maxToRead = (int)Math.Min(count, grainSize * _grainContiguousRangeMapping[grainIdx] - grainOffset);

                    _content.Position = _grainMapping[grainIdx] * grainSize + grainOffset;
                    return _content.Read(block, offset, maxToRead);
                }
            }

            internal override void DisposeReadState()
            {
                _grainTableStream = null;
                _grainMapping = null;
                _grainContiguousRangeMapping = null;
            }

            private static long CalcSize(SparseStream content, ServerSparseExtentHeader header)
            {
                long numDataGrains = StreamExtent.BlockCount(content.Extents, header.GrainSize * Sizes.Sector);
                long grainTableSectors = Utilities.Ceil(header.NumGTEsPerGT * 4, Sizes.Sector);

                return (grainTableSectors + (numDataGrains * header.GrainSize)) * Sizes.Sector;
            }

        }
    }
}

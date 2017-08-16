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
using DiscUtils.Compression;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class CompressedClusterStream : ClusterStream
    {
        private readonly NtfsAttribute _attr;
        private readonly int _bytesPerCluster;

        private readonly byte[] _cacheBuffer;
        private long _cacheBufferVcn = -1;
        private readonly INtfsContext _context;
        private readonly byte[] _ioBuffer;
        private readonly RawClusterStream _rawStream;

        public CompressedClusterStream(INtfsContext context, NtfsAttribute attr, RawClusterStream rawStream)
        {
            _context = context;
            _attr = attr;
            _rawStream = rawStream;
            _bytesPerCluster = _context.BiosParameterBlock.BytesPerCluster;

            _cacheBuffer = new byte[_attr.CompressionUnitSize * context.BiosParameterBlock.BytesPerCluster];
            _ioBuffer = new byte[_attr.CompressionUnitSize * context.BiosParameterBlock.BytesPerCluster];
        }

        public override long AllocatedClusterCount
        {
            get { return _rawStream.AllocatedClusterCount; }
        }

        public override IEnumerable<Range<long, long>> StoredClusters
        {
            get { return Range<long, long>.Chunked(_rawStream.StoredClusters, _attr.CompressionUnitSize); }
        }

        public override bool IsClusterStored(long vcn)
        {
            return _rawStream.IsClusterStored(CompressionStart(vcn));
        }

        public override void ExpandToClusters(long numVirtualClusters, NonResidentAttributeRecord extent, bool allocate)
        {
            _rawStream.ExpandToClusters(MathUtilities.RoundUp(numVirtualClusters, _attr.CompressionUnitSize), extent, false);
        }

        public override void TruncateToClusters(long numVirtualClusters)
        {
            long alignedNum = MathUtilities.RoundUp(numVirtualClusters, _attr.CompressionUnitSize);
            _rawStream.TruncateToClusters(alignedNum);
            if (alignedNum != numVirtualClusters)
            {
                _rawStream.ReleaseClusters(numVirtualClusters, (int)(alignedNum - numVirtualClusters));
            }
        }

        public override void ReadClusters(long startVcn, int count, byte[] buffer, int offset)
        {
            if (buffer.Length < count * _bytesPerCluster + offset)
            {
                throw new ArgumentException("Cluster buffer too small", nameof(buffer));
            }

            int totalRead = 0;
            while (totalRead < count)
            {
                long focusVcn = startVcn + totalRead;
                LoadCache(focusVcn);

                int cacheOffset = (int)(focusVcn - _cacheBufferVcn);
                int toCopy = Math.Min(_attr.CompressionUnitSize - cacheOffset, count - totalRead);

                Array.Copy(_cacheBuffer, cacheOffset * _bytesPerCluster, buffer, offset + totalRead * _bytesPerCluster,
                    toCopy * _bytesPerCluster);

                totalRead += toCopy;
            }
        }

        public override int WriteClusters(long startVcn, int count, byte[] buffer, int offset)
        {
            if (buffer.Length < count * _bytesPerCluster + offset)
            {
                throw new ArgumentException("Cluster buffer too small", nameof(buffer));
            }

            int totalAllocated = 0;

            int totalWritten = 0;
            while (totalWritten < count)
            {
                long focusVcn = startVcn + totalWritten;
                long cuStart = CompressionStart(focusVcn);

                if (cuStart == focusVcn && count - totalWritten >= _attr.CompressionUnitSize)
                {
                    // Aligned write...
                    totalAllocated += CompressAndWriteClusters(focusVcn, _attr.CompressionUnitSize, buffer,
                        offset + totalWritten * _bytesPerCluster);

                    totalWritten += _attr.CompressionUnitSize;
                }
                else
                {
                    // Unaligned, so go through cache
                    LoadCache(focusVcn);

                    int cacheOffset = (int)(focusVcn - _cacheBufferVcn);
                    int toCopy = Math.Min(count - totalWritten, _attr.CompressionUnitSize - cacheOffset);

                    Array.Copy(buffer, offset + totalWritten * _bytesPerCluster, _cacheBuffer,
                        cacheOffset * _bytesPerCluster, toCopy * _bytesPerCluster);

                    totalAllocated += CompressAndWriteClusters(_cacheBufferVcn, _attr.CompressionUnitSize, _cacheBuffer,
                        0);

                    totalWritten += toCopy;
                }
            }

            return totalAllocated;
        }

        public override int ClearClusters(long startVcn, int count)
        {
            int totalReleased = 0;
            int totalCleared = 0;
            while (totalCleared < count)
            {
                long focusVcn = startVcn + totalCleared;
                if (CompressionStart(focusVcn) == focusVcn && count - totalCleared >= _attr.CompressionUnitSize)
                {
                    // Aligned - so it's a sparse compression unit...
                    totalReleased += _rawStream.ReleaseClusters(startVcn, _attr.CompressionUnitSize);
                    totalCleared += _attr.CompressionUnitSize;
                }
                else
                {
                    int toZero =
                        (int)
                        Math.Min(count - totalCleared,
                            _attr.CompressionUnitSize - (focusVcn - CompressionStart(focusVcn)));
                    totalReleased -= WriteZeroClusters(focusVcn, toZero);
                    totalCleared += toZero;
                }
            }

            return totalReleased;
        }

        private int WriteZeroClusters(long focusVcn, int count)
        {
            int allocatedClusters = 0;

            byte[] zeroBuffer = new byte[16 * _bytesPerCluster];
            int numWritten = 0;
            while (numWritten < count)
            {
                int toWrite = Math.Min(count - numWritten, 16);

                allocatedClusters += WriteClusters(focusVcn + numWritten, toWrite, zeroBuffer, 0);

                numWritten += toWrite;
            }

            return allocatedClusters;
        }

        private int CompressAndWriteClusters(long focusVcn, int count, byte[] buffer, int offset)
        {
            BlockCompressor compressor = _context.Options.Compressor;
            compressor.BlockSize = _bytesPerCluster;

            int totalAllocated = 0;

            int compressedLength = _ioBuffer.Length;
            CompressionResult result = compressor.Compress(buffer, offset, _attr.CompressionUnitSize * _bytesPerCluster, _ioBuffer, 0,
                ref compressedLength);
            if (result == CompressionResult.AllZeros)
            {
                totalAllocated -= _rawStream.ReleaseClusters(focusVcn, count);
            }
            else if (result == CompressionResult.Compressed &&
                     _attr.CompressionUnitSize * _bytesPerCluster - compressedLength > _bytesPerCluster)
            {
                int compClusters = MathUtilities.Ceil(compressedLength, _bytesPerCluster);
                totalAllocated += _rawStream.AllocateClusters(focusVcn, compClusters);
                totalAllocated += _rawStream.WriteClusters(focusVcn, compClusters, _ioBuffer, 0);
                totalAllocated -= _rawStream.ReleaseClusters(focusVcn + compClusters,
                    _attr.CompressionUnitSize - compClusters);
            }
            else
            {
                totalAllocated += _rawStream.AllocateClusters(focusVcn, _attr.CompressionUnitSize);
                totalAllocated += _rawStream.WriteClusters(focusVcn, _attr.CompressionUnitSize, buffer, offset);
            }

            return totalAllocated;
        }

        private long CompressionStart(long vcn)
        {
            return MathUtilities.RoundDown(vcn, _attr.CompressionUnitSize);
        }

        private void LoadCache(long vcn)
        {
            long cuStart = CompressionStart(vcn);
            if (_cacheBufferVcn != cuStart)
            {
                if (_rawStream.AreAllClustersStored(cuStart, _attr.CompressionUnitSize))
                {
                    // Uncompressed data - read straight into cache buffer
                    _rawStream.ReadClusters(cuStart, _attr.CompressionUnitSize, _cacheBuffer, 0);
                }
                else if (_rawStream.IsClusterStored(cuStart))
                {
                    // Compressed data - read via IO buffer
                    _rawStream.ReadClusters(cuStart, _attr.CompressionUnitSize, _ioBuffer, 0);

                    int expected =
                        (int)
                        Math.Min(_attr.Length - vcn * _bytesPerCluster, _attr.CompressionUnitSize * _bytesPerCluster);

                    int decomp = _context.Options.Compressor.Decompress(_ioBuffer, 0, _ioBuffer.Length, _cacheBuffer, 0);
                    if (decomp < expected)
                    {
                        throw new IOException("Decompression returned too little data");
                    }
                }
                else
                {
                    // Sparse, wipe cache buffer directly
                    Array.Clear(_cacheBuffer, 0, _cacheBuffer.Length);
                }

                _cacheBufferVcn = cuStart;
            }
        }
    }
}
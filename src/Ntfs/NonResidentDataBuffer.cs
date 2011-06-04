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

namespace DiscUtils.Ntfs
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class NonResidentDataBuffer : DiscUtils.Buffer, IMappedBuffer
    {
        protected Stream _fsStream;
        protected long _bytesPerCluster;

        protected CookedDataRuns _cookedRuns;
        protected AttributeFlags _flags;
        protected int _compressionUnitSize;

        protected byte[] _cachedDecompressedBlock;
        protected long _cachedBlockStartVcn;

        public NonResidentDataBuffer(INtfsContext context, NonResidentAttributeRecord record)
            : this(context.RawStream, context.BiosParameterBlock.BytesPerCluster, new CookedDataRuns(record.DataRuns), record.Flags, record.CompressionUnitSize)
        {
        }

        public NonResidentDataBuffer(Stream fsStream, long bytesPerCluster, CookedDataRuns cookedRuns, AttributeFlags flags, int compressionUnitSize)
        {
            _fsStream = fsStream;
            _bytesPerCluster = bytesPerCluster;
            _cookedRuns = cookedRuns;
            _flags = flags;
            _compressionUnitSize = compressionUnitSize;
        }

        public override bool CanRead
        {
            get { return _fsStream.CanRead; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Capacity
        {
            get { return VirtualClusterCount * _bytesPerCluster; }
        }

        public long VirtualClusterCount
        {
            get { return _cookedRuns.NextVirtualCluster; }
        }

        public long LastLogicalCluster
        {
            get { return _cookedRuns.LastLogicalCluster; }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                StreamExtent lastExtent = null;
                List<StreamExtent> extents = new List<StreamExtent>();

                if ((_flags & (AttributeFlags.Compressed | AttributeFlags.Encrypted)) == AttributeFlags.Compressed)
                {
                    long lastByte = (_cookedRuns.Last.StartVcn + _cookedRuns.Last.Length) * _bytesPerCluster;

                    int runIdx = 0;
                    int cluster = 0;
                    while (cluster < _cookedRuns.NextVirtualCluster)
                    {
                        runIdx = FindDataRun(cluster, runIdx);
                        CookedDataRun cookedRun = _cookedRuns[runIdx];

                        if (!cookedRun.IsSparse)
                        {
                            long startPos = cookedRun.StartVcn * _bytesPerCluster;
                            if (lastExtent != null && lastExtent.Start + lastExtent.Length == startPos)
                            {
                                lastExtent = new StreamExtent(lastExtent.Start, Math.Min(lastByte - lastExtent.Start, lastExtent.Length + (_compressionUnitSize * _bytesPerCluster)));
                                extents[extents.Count - 1] = lastExtent;
                            }
                            else
                            {
                                lastExtent = new StreamExtent(cookedRun.StartVcn * _bytesPerCluster, Math.Min(lastByte - (cookedRun.StartVcn * _bytesPerCluster), _compressionUnitSize * _bytesPerCluster));
                                extents.Add(lastExtent);
                            }
                        }

                        cluster += _compressionUnitSize;
                    }
                }
                else
                {
                    int runCount = _cookedRuns.Count;
                    for (int i = 0; i < runCount; i++)
                    {
                        CookedDataRun cookedRun = _cookedRuns[i];
                        if (!cookedRun.IsSparse)
                        {
                            long startPos = cookedRun.StartVcn * _bytesPerCluster;
                            if (lastExtent != null && lastExtent.Start + lastExtent.Length == startPos)
                            {
                                lastExtent = new StreamExtent(lastExtent.Start, lastExtent.Length + (cookedRun.Length * _bytesPerCluster));
                                extents[extents.Count - 1] = lastExtent;
                            }
                            else
                            {
                                lastExtent = new StreamExtent(cookedRun.StartVcn * _bytesPerCluster, cookedRun.Length * _bytesPerCluster);
                                extents.Add(lastExtent);
                            }
                        }
                    }
                }

                return extents;
            }
        }

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return StreamExtent.Intersect(Extents, new StreamExtent(start, count));
        }

        public long MapPosition(long pos)
        {
            long vcn = pos / _bytesPerCluster;
            int dataRunIdx = FindDataRun(vcn);

            if (_cookedRuns[dataRunIdx].IsSparse)
            {
                return -1;
            }
            else
            {
                return (_cookedRuns[dataRunIdx].StartLcn * _bytesPerCluster) + (pos - (_cookedRuns[dataRunIdx].StartVcn * _bytesPerCluster));
            }
        }

        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new IOException("Attempt to read from file not opened for read");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to read negative number of bytes");
            }

            // Limit read to length of attribute
            int toRead = (int)Math.Min(count, Capacity - pos);
            if (toRead == 0)
            {
                return 0;
            }

            switch (_flags & (AttributeFlags.Compressed | AttributeFlags.Encrypted))
            {
                case AttributeFlags.Compressed:
                    return DoReadCompressed(pos, buffer, offset, toRead);
                case AttributeFlags.Sparse:
                    return DoReadSparse(pos, buffer, offset, toRead);
                default:
                    return DoReadNormal(pos, buffer, offset, toRead);
            }
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void SetCapacity(long value)
        {
            throw new NotSupportedException();
        }

        protected int FindDataRun(long vcn)
        {
            return _cookedRuns.FindDataRun(vcn, 0);
        }

        protected int FindDataRun(long vcn, int startIdx)
        {
            return _cookedRuns.FindDataRun(vcn, startIdx);
        }

        private static byte[] Decompress(byte[] compBuffer)
        {
            const ushort SubBlockIsCompressedFlag = 0x8000;
            const ushort SubBlockSizeMask = 0x0fff;
            const ushort SubBlockSize = 0x1000;

            byte[] resultBuffer = new byte[compBuffer.Length];

            int sourceIdx = 0;
            int destIdx = 0;

            while (destIdx < resultBuffer.Length)
            {
                ushort header = Utilities.ToUInt16LittleEndian(compBuffer, sourceIdx);
                sourceIdx += 2;

                // Look for null-terminating sub-block header
                if (header == 0)
                {
                    break;
                }

                if ((header & SubBlockIsCompressedFlag) == 0)
                {
                    // not compressed
                    if ((header & SubBlockSizeMask) + 1 != SubBlockSize)
                    {
                        throw new IOException("Found short non-compressed sub-block");
                    }

                    Array.Copy(compBuffer, sourceIdx, resultBuffer, destIdx, SubBlockSize);
                    sourceIdx += SubBlockSize;
                    destIdx += SubBlockSize;
                }
                else
                {
                    // compressed
                    int destSubBlockStart = destIdx;
                    int srcSubBlockEnd = sourceIdx + (header & SubBlockSizeMask) + 1;
                    while (sourceIdx < srcSubBlockEnd)
                    {
                        byte tag = compBuffer[sourceIdx];
                        ++sourceIdx;

                        for (int token = 0; token < 8; ++token)
                        {
                            // We might have hit the end of the sub block whilst still working though
                            // a tag - abort if we have...
                            if (sourceIdx >= srcSubBlockEnd)
                            {
                                break;
                            }

                            if ((tag & 1) == 0)
                            {
                                resultBuffer[destIdx] = compBuffer[sourceIdx];
                                ++destIdx;
                                ++sourceIdx;
                            }
                            else
                            {
                                ushort lengthMask = 0xFFF;
                                ushort deltaShift = 12;
                                for (int i = (destIdx - destSubBlockStart) - 1; i >= 0x10; i >>= 1)
                                {
                                    lengthMask >>= 1;
                                    --deltaShift;
                                }

                                ushort phraseToken = Utilities.ToUInt16LittleEndian(compBuffer, sourceIdx);
                                sourceIdx += 2;

                                int destBackAddr = destIdx - (phraseToken >> deltaShift) - 1;
                                int length = (phraseToken & lengthMask) + 3;
                                for (int i = 0; i < length; ++i)
                                {
                                    resultBuffer[destIdx++] = resultBuffer[destBackAddr++];
                                }
                            }

                            tag >>= 1;
                        }
                    }

                    if (destIdx < destSubBlockStart + SubBlockSize)
                    {
                        // Zero buffer here in future, if not known to be zero's - this handles the case
                        // of a decompressed sub-block not being full-length
                        destIdx = destSubBlockStart + SubBlockSize;
                    }
                }
            }

            return resultBuffer;
        }

        private int DoReadNormal(long pos, byte[] buffer, int offset, int count)
        {
            long vcn = pos / _bytesPerCluster;
            int dataRunIdx = FindDataRun(vcn);
            RawRead(dataRunIdx, pos - (_cookedRuns[dataRunIdx].StartVcn * _bytesPerCluster), buffer, offset, count, true);
            return count;
        }

        private int DoReadSparse(long pos, byte[] buffer, int offset, int count)
        {
            long vcn = pos / _bytesPerCluster;
            int dataRunIdx = FindDataRun(vcn);
            long runOffset = pos - (_cookedRuns[dataRunIdx].StartVcn * _bytesPerCluster);

            if (_cookedRuns[dataRunIdx].IsSparse)
            {
                int numBytes = (int)Math.Min(count, (_cookedRuns[dataRunIdx].Length * _bytesPerCluster) - runOffset);
                Array.Clear(buffer, offset, numBytes);
                return numBytes;
            }
            else
            {
                RawRead(dataRunIdx, runOffset, buffer, offset, count, true);
                return count;
            }
        }

        private int DoReadCompressed(long pos, byte[] buffer, int offset, int count)
        {
            long compressionUnitLength = _compressionUnitSize * _bytesPerCluster;

            long startVcn = (pos / compressionUnitLength) * _compressionUnitSize;
            long targetCluster = pos / _bytesPerCluster;
            long blockOffset = pos - (startVcn * _bytesPerCluster);

            int dataRunIdx = FindDataRun(startVcn);
            if (_cookedRuns[dataRunIdx].IsSparse)
            {
                int numBytes = (int)Math.Min(count, compressionUnitLength - blockOffset);
                Array.Clear(buffer, offset, numBytes);
                return numBytes;
            }
            else if (IsBlockCompressed(startVcn, _compressionUnitSize))
            {
                byte[] decompBuffer;
                if (_cachedDecompressedBlock != null && _cachedBlockStartVcn == dataRunIdx)
                {
                    decompBuffer = _cachedDecompressedBlock;
                }
                else
                {
                    byte[] compBuffer = new byte[compressionUnitLength];

                    RawRead(dataRunIdx, (startVcn - _cookedRuns[dataRunIdx].StartVcn) * _bytesPerCluster, compBuffer, 0, (int)compressionUnitLength, false);

                    decompBuffer = Decompress(compBuffer);

                    _cachedDecompressedBlock = decompBuffer;
                    _cachedBlockStartVcn = dataRunIdx;
                }

                int numBytes = (int)Math.Min(count, decompBuffer.Length - blockOffset);
                Array.Copy(decompBuffer, blockOffset, buffer, offset, numBytes);
                return numBytes;
            }
            else
            {
                // Whole block is uncompressed.

                // Skip forward to the data run containing the first cluster we need to read
                dataRunIdx = FindDataRun(targetCluster, dataRunIdx);

                // Read to the end of the compression cluster
                int numBytes = (int)Math.Min(count, compressionUnitLength - blockOffset);
                RawRead(dataRunIdx, pos - (_cookedRuns[dataRunIdx].StartVcn * _bytesPerCluster), buffer, offset, numBytes, true);
                return numBytes;
            }
        }

        /// <summary>
        /// Read data from one or more runs.
        /// </summary>
        /// <param name="startRunIdx">The start run index</param>
        /// <param name="startRunOffset">The first byte in the run to read (as byte offset)</param>
        /// <param name="data">The buffer to fill</param>
        /// <param name="dataOffset">Offset to first byte in buffer to fill</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="clearHoles">Whether to zero-out sparse runs, or to just skip</param>
        private void RawRead(int startRunIdx, long startRunOffset, byte[] data, int dataOffset, int count, bool clearHoles)
        {
            int totalRead = 0;
            int runIdx = startRunIdx;
            long runOffset = startRunOffset;

            while (totalRead < count)
            {
                int toRead = (int)Math.Min(count - totalRead, (_cookedRuns[runIdx].Length * _bytesPerCluster) - runOffset);

                if (_cookedRuns[runIdx].IsSparse)
                {
                    if (clearHoles)
                    {
                        Array.Clear(data, dataOffset + totalRead, toRead);
                    }

                    totalRead += toRead;
                    runOffset = 0;
                    runIdx++;
                }
                else
                {
                    _fsStream.Position = (_cookedRuns[runIdx].StartLcn * _bytesPerCluster) + runOffset;
                    int numRead = _fsStream.Read(data, dataOffset + totalRead, toRead);
                    totalRead += numRead;
                    runOffset += numRead;

                    if (runOffset >= _cookedRuns[runIdx].Length * _bytesPerCluster)
                    {
                        runOffset = 0;
                        runIdx++;
                    }
                }
            }
        }

        private bool IsBlockCompressed(long startVcn, int compressionUnitSize)
        {
            int clustersRemaining = compressionUnitSize;
            int dataRunIdx = FindDataRun(startVcn);
            long dataRunOffset = startVcn - _cookedRuns[dataRunIdx].StartVcn;

            while (clustersRemaining > 0)
            {
                // We're looking for this - a sparse record within compressionUnit Virtual Clusters
                // from the start of the compression unit.  If we don't find it, then the compression
                // unit is not actually compressed.
                if (_cookedRuns[dataRunIdx].IsSparse)
                {
                    return true;
                }

                int vcnContrib = (int)(_cookedRuns[dataRunIdx].Length - dataRunOffset);
                clustersRemaining -= vcnContrib;
                dataRunOffset = 0;
                dataRunIdx++;
            }

            return false;
        }
    }
}

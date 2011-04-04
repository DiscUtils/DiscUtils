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

    internal class NonResidentAttributeBuffer : DiscUtils.Buffer, IMappedBuffer
    {
        private File _file;
        private NtfsAttribute _attribute;
        private Stream _fsStream;
        private long _bytesPerCluster;

        private CookedDataRuns _cookedRuns;
        private AttributeFlags _flags;
        private int _compressionUnitSize;
        private byte[] _cachedDecompressedBlock;
        private long _cachedBlockStartVcn;

        public NonResidentAttributeBuffer(File file, NtfsAttribute attribute)
        {
            _file = file;
            _attribute = attribute;
            _fsStream = _file.Context.RawStream;
            _bytesPerCluster = file.Context.BiosParameterBlock.BytesPerCluster;
            _flags = attribute.Flags;
            _compressionUnitSize = attribute.CompressionUnitSize;

            _cookedRuns = new CookedDataRuns();
            foreach (NonResidentAttributeRecord record in attribute.Records)
            {
                if (record.StartVcn != _cookedRuns.NextVirtualCluster)
                {
                    throw new IOException("Invalid NTFS attribute - non-contiguous data runs");
                }

                _cookedRuns.Append(record.DataRuns);
            }
        }

        internal NonResidentAttributeBuffer(INtfsContext context, NonResidentAttributeRecord record)
        {
            _fsStream = context.RawStream;
            _bytesPerCluster = context.BiosParameterBlock.BytesPerCluster;
            _flags = record.Flags;
            _compressionUnitSize = record.CompressionUnitSize;

            _cookedRuns = new CookedDataRuns(record.DataRuns);
        }

        public override bool CanRead
        {
            get { return _fsStream.CanRead; }
        }

        public override bool CanWrite
        {
            get { return _fsStream.CanWrite && _file != null; }
        }

        public override long Capacity
        {
            get { return PrimaryAttributeRecord.DataLength; }
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
            get { return new StreamExtent[] { new StreamExtent(0, Capacity) }; }
        }

        private NonResidentAttributeRecord PrimaryAttributeRecord
        {
            get { return _attribute.PrimaryRecord as NonResidentAttributeRecord; }
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

            int numRead;
            if (_flags == AttributeFlags.None)
            {
                numRead = DoReadNormal(pos, buffer, offset, toRead);
            }
            else if (_flags == AttributeFlags.Compressed)
            {
                numRead = DoReadCompressed(pos, buffer, offset, toRead);
            }
            else
            {
                numRead = DoReadSparse(pos, buffer, offset, toRead);
            }

            return numRead;
        }

        public override void SetCapacity(long value)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to change length of file not opened for write");
            }

            if (value == Capacity)
            {
                return;
            }

            _file.MarkMftRecordDirty();

            long newClusterCount = Utilities.Ceil(value, _bytesPerCluster);

            if (newClusterCount != VirtualClusterCount)
            {
                if (newClusterCount < VirtualClusterCount)
                {
                    Truncate(newClusterCount);
                }
                else
                {
                    if (value > Capacity)
                    {
                        long numToAllocate = newClusterCount - VirtualClusterCount;
                        long proposedStart = Capacity == 0 ? -1 : LastLogicalCluster + 1;
                        DiscUtils.Tuple<long, long>[] runs = _file.Context.ClusterBitmap.AllocateClusters(numToAllocate, proposedStart, _file.IndexInMft == MasterFileTable.MftIndex, newClusterCount);
                        foreach (var run in runs)
                        {
                            AddDataRun(run.First, run.Second);
                        }
                    }
                }
            }

            PrimaryAttributeRecord.DataLength = value;

            if (PrimaryAttributeRecord.InitializedDataLength > value)
            {
                PrimaryAttributeRecord.InitializedDataLength = value;
            }

            _file.MarkMftRecordDirty();
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to write to file not opened for write");
            }

            if (_flags != AttributeFlags.None)
            {
                throw new NotImplementedException("Writing to compressed / sparse attributes");
            }

            if (count == 0)
            {
                return;
            }

            if (pos + count > Capacity)
            {
                SetCapacity(pos + count);
            }

            // Write zeros from end of current initialized data to the start of the new write
            if (pos > PrimaryAttributeRecord.InitializedDataLength)
            {
                _file.MarkMftRecordDirty();

                byte[] wipeBuffer = new byte[_bytesPerCluster * 4];

                long wipePos = PrimaryAttributeRecord.InitializedDataLength;
                while (wipePos < pos)
                {
                    int numToWrite = (int)Math.Min(wipeBuffer.Length, pos - wipePos);
                    RawWrite(wipePos, wipeBuffer, 0, numToWrite);
                    wipePos += numToWrite;
                }
            }

            RawWrite(pos, buffer, offset, count);

            if (pos + count > PrimaryAttributeRecord.InitializedDataLength)
            {
                _file.MarkMftRecordDirty();

                PrimaryAttributeRecord.InitializedDataLength = pos + count;
            }

            if (pos + count > PrimaryAttributeRecord.DataLength)
            {
                _file.MarkMftRecordDirty();

                PrimaryAttributeRecord.DataLength = pos + count;
            }
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

        private void Truncate(long value)
        {
            long endVcn = Utilities.Ceil(value, _bytesPerCluster);

            // First, remove any extents that are now redundant.
            Dictionary<AttributeReference, AttributeRecord> extentCache = new Dictionary<AttributeReference, AttributeRecord>(_attribute.Extents);
            foreach (var extent in extentCache)
            {
                if (extent.Value.StartVcn >= endVcn)
                {
                    RemoveAndFreeExtent(extent);
                }
            }

            // Second, truncate the last extent.
            NonResidentAttributeRecord lastExtent = (NonResidentAttributeRecord)_attribute.LastExtent;
            if (lastExtent != null)
            {
                TruncateAndFreeExtent(lastExtent, value - (lastExtent.StartVcn * _bytesPerCluster));
            }

            // Updated our cache of cooked runs.
            _cookedRuns.Truncate(endVcn);

            PrimaryAttributeRecord.LastVcn = Math.Max(0, endVcn - 1);
            PrimaryAttributeRecord.AllocatedLength = endVcn * _bytesPerCluster;
            PrimaryAttributeRecord.DataLength = value;
            PrimaryAttributeRecord.InitializedDataLength = Math.Min(PrimaryAttributeRecord.InitializedDataLength, value);

            _file.MarkMftRecordDirty();
        }

        private void TruncateAndFreeExtent(NonResidentAttributeRecord extent, long clusterLength)
        {
            long virtualCluster = 0;
            long logicalCluster = 0;

            int index = 0;
            while (index < extent.DataRuns.Count)
            {
                DataRun run = extent.DataRuns[index];

                logicalCluster += run.RunOffset;

                if (virtualCluster >= clusterLength)
                {
                    // Whole run is redundant.
                    if (!run.IsSparse)
                    {
                        _file.Context.ClusterBitmap.FreeClusters(new Range<long, long>(logicalCluster, run.RunLength));
                    }

                    extent.DataRuns.RemoveAt(index);
                }
                else if (virtualCluster + run.RunLength > clusterLength)
                {
                    long toKeep = clusterLength - virtualCluster;

                    // Need to truncate this run.
                    if (!run.IsSparse)
                    {
                        _file.Context.ClusterBitmap.FreeClusters(new Range<long, long>(logicalCluster + toKeep, run.RunLength - toKeep));
                    }

                    run.RunLength = toKeep;
                }

                virtualCluster += run.RunLength;
            }

            extent.LastVcn = extent.StartVcn + clusterLength - 1;
        }

        private KeyValuePair<AttributeReference, AttributeRecord> RemoveAndFreeExtent(KeyValuePair<AttributeReference, AttributeRecord> extent)
        {
            NonResidentAttributeRecord record = (NonResidentAttributeRecord)extent.Value;
            _file.RemoveAttributeExtent(extent.Key);
            _attribute.RemoveExtentCacheSafe(extent.Key);

            _file.Context.ClusterBitmap.FreeClusters(record.GetClusters());

            return extent;
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

        /// <summary>
        /// Write data to one or more runs.
        /// </summary>
        /// <param name="position">Logical position within stream to start writing</param>
        /// <param name="data">The buffer to write</param>
        /// <param name="dataOffset">Offset to first byte in buffer to write</param>
        /// <param name="count">Number of bytes to write</param>
        private void RawWrite(long position, byte[] data, int dataOffset, int count)
        {
            long vcn = position / _bytesPerCluster;
            int runIdx = FindDataRun(vcn);
            long runOffset = position - (_cookedRuns[runIdx].StartVcn * _bytesPerCluster);

            int totalWritten = 0;
            while (totalWritten < count)
            {
                if (_cookedRuns[runIdx].IsSparse)
                {
                    throw new NotImplementedException("Writing to sparse dataruns");
                }

                int toWrite = (int)Math.Min(count - totalWritten, (_cookedRuns[runIdx].Length * _bytesPerCluster) - runOffset);

                _fsStream.Position = (_cookedRuns[runIdx].StartLcn * _bytesPerCluster) + runOffset;
                _fsStream.Write(data, dataOffset + totalWritten, toWrite);
                totalWritten += toWrite;
                runOffset += toWrite;

                if (runOffset >= _cookedRuns[runIdx].Length * _bytesPerCluster)
                {
                    runOffset = 0;
                    runIdx++;
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

        private int FindDataRun(long vcn)
        {
            return _cookedRuns.FindDataRun(vcn, 0);
        }

        private int FindDataRun(long vcn, int startIdx)
        {
            return _cookedRuns.FindDataRun(vcn, startIdx);
        }

        private void AddDataRun(long startLcn, long length)
        {
            CookedDataRun lastRun = _cookedRuns.Last;
            NonResidentAttributeRecord lastExtent = (NonResidentAttributeRecord)_attribute.LastExtent;

            if (lastRun != null && lastRun.StartLcn + lastRun.Length == startLcn)
            {
                lastRun.Length += length;
            }
            else
            {
                long lastStartLcn = lastRun != null ? lastRun.StartLcn : 0;
                DataRun newRun = new DataRun(startLcn - lastStartLcn, length);
                lastExtent.DataRuns.Add(newRun);
                _cookedRuns.Append(newRun);
            }

            lastExtent.LastVcn = _cookedRuns.NextVirtualCluster - 1;
            PrimaryAttributeRecord.AllocatedLength += length * _bytesPerCluster;
        }
    }
}

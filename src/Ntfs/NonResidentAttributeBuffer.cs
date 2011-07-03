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

    internal class NonResidentAttributeBuffer : NonResidentDataBuffer
    {
        private File _file;
        private NtfsAttribute _attribute;

        public NonResidentAttributeBuffer(File file, NtfsAttribute attribute)
            : base(file.Context.RawStream, file.Context.BiosParameterBlock.BytesPerCluster, null, attribute.Flags, attribute.CompressionUnitSize, file.Context.Options.Compressor)
        {
            _file = file;
            _attribute = attribute;

            _cookedRuns = new CookedDataRuns();
            foreach (NonResidentAttributeRecord record in attribute.Records)
            {
                if (record.StartVcn != _cookedRuns.NextVirtualCluster)
                {
                    throw new IOException("Invalid NTFS attribute - non-contiguous data runs");
                }

                _cookedRuns.Append(record.DataRuns, record);
            }
        }

        public override bool CanWrite
        {
            get { return _fsStream.CanWrite && _file != null; }
        }

        public override long Capacity
        {
            get { return PrimaryAttributeRecord.DataLength; }
        }

        private NonResidentAttributeRecord PrimaryAttributeRecord
        {
            get { return _attribute.PrimaryRecord as NonResidentAttributeRecord; }
        }

        public void AlignVirtualClusterCount()
        {
            long desiredCount = Utilities.RoundUp(VirtualClusterCount, _compressionUnitSize);
            if (desiredCount != VirtualClusterCount)
            {
                _file.MarkMftRecordDirty();
                AddSparseDataRun(desiredCount - VirtualClusterCount);
            }
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

            long newClusterCount = Utilities.RoundUp(Utilities.Ceil(value, _bytesPerCluster), _compressionUnitSize);

            if (newClusterCount < VirtualClusterCount)
            {
                Truncate(newClusterCount);
            }
            else if (newClusterCount > VirtualClusterCount)
            {
                if (_flags == AttributeFlags.Sparse)
                {
                    AddSparseDataRun(Utilities.RoundUp(newClusterCount, _compressionUnitSize) - VirtualClusterCount);
                }
                else
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

            PrimaryAttributeRecord.DataLength = value;

            if (PrimaryAttributeRecord.InitializedDataLength > value)
            {
                PrimaryAttributeRecord.InitializedDataLength = value;
            }
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to write to file not opened for write");
            }

            if (_flags != AttributeFlags.None && _flags != AttributeFlags.Sparse)
            {
                throw new NotImplementedException("Writing to compressed attributes");
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

                RawWriteZeros(PrimaryAttributeRecord.InitializedDataLength, pos - PrimaryAttributeRecord.InitializedDataLength);
                PrimaryAttributeRecord.InitializedDataLength = pos;
            }

            switch (_flags & (AttributeFlags.Compressed | AttributeFlags.Sparse))
            {
                case AttributeFlags.Compressed:
                    throw new NotImplementedException("Writing to sparse streams");
                case AttributeFlags.Sparse:
                    DoWriteSparse(pos, buffer, offset, count);
                    break;
                case AttributeFlags.None:
                    DoWriteNormal(pos, buffer, offset, count);
                    break;
                default:
                    throw new IOException("Unrecognized combination of attribute flags");
            }

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

        public override void Clear(long pos, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to erase bytes from file not opened for write");
            }

            if ((_flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != AttributeFlags.Sparse)
            {
                base.Clear(pos, count);
            }
            else
            {
                if (count == 0)
                {
                    return;
                }

                if (pos + count > Capacity)
                {
                    SetCapacity(pos + count);
                }

                _file.MarkMftRecordDirty();

                // Write zeros from end of current initialized data to the start of the new write
                if (pos > PrimaryAttributeRecord.InitializedDataLength)
                {
                    RawWriteZeros(PrimaryAttributeRecord.InitializedDataLength, pos - PrimaryAttributeRecord.InitializedDataLength);
                    PrimaryAttributeRecord.InitializedDataLength = pos;
                }

                RawWriteZeros(pos, count);

                if (pos + count > PrimaryAttributeRecord.InitializedDataLength)
                {
                    PrimaryAttributeRecord.InitializedDataLength = pos + count;
                }

                if (pos + count > PrimaryAttributeRecord.DataLength)
                {
                    PrimaryAttributeRecord.DataLength = pos + count;
                }
            }
        }

        private void DoWriteSparse(long pos, byte[] buffer, int offset, int count)
        {
            long vcn = pos / _bytesPerCluster;
            long endVcn = Utilities.Ceil(pos + count, _bytesPerCluster);

            int runIdx = FindDataRun(vcn);
            long clustersRemaining = endVcn - vcn;
            while (clustersRemaining > 0)
            {
                if (_cookedRuns[runIdx].IsSparse)
                {
                    if (vcn > _cookedRuns[runIdx].StartVcn)
                    {
                        _cookedRuns.SplitRun(runIdx, vcn);
                        runIdx++;
                    }

                    if (endVcn < _cookedRuns[runIdx].StartVcn + _cookedRuns[runIdx].Length)
                    {
                        _cookedRuns.SplitRun(runIdx, endVcn);
                    }

                    DiscUtils.Tuple<long, long>[] allocs = _file.Context.ClusterBitmap.AllocateClusters(_cookedRuns[runIdx].Length, _cookedRuns[runIdx].StartLcn, false, endVcn);
                    byte[] clusterBuffer = new byte[_bytesPerCluster];

                    // Wipe the first and last clusters if they correspond to the edges of the write, and the write is unaligned.
                    long startWipeCluster = -1;
                    if (clustersRemaining == endVcn - vcn && pos % _bytesPerCluster != 0)
                    {
                        startWipeCluster = allocs[0].First;
                        _fsStream.Position = startWipeCluster * _bytesPerCluster;
                        _fsStream.Write(clusterBuffer, 0, (int)_bytesPerCluster);
                    }

                    if (_cookedRuns[runIdx].Length == clustersRemaining && (pos + count) % _bytesPerCluster != 0)
                    {
                        long endWipeCluster = allocs[allocs.Length - 1].First + allocs[allocs.Length - 1].Second - 1;
                        if (startWipeCluster != endWipeCluster)
                        {
                            _fsStream.Position = endWipeCluster * _bytesPerCluster;
                            _fsStream.Write(clusterBuffer, 0, (int)_bytesPerCluster);
                        }
                    }

                    List<DataRun> runs = new List<DataRun>();

                    long lcn = runIdx == 0 ? 0 : _cookedRuns[runIdx - 1].StartLcn;
                    foreach (var allocation in allocs)
                    {
                        runs.Add(new DataRun(allocation.First - lcn, allocation.Second, false));
                    }

                    _cookedRuns.MakeNonSparse(runIdx, runs);
                }

                clustersRemaining -= _cookedRuns[runIdx].Length;
                runIdx++;
            }

            _cookedRuns.CollapseRuns();

            RawWrite(pos, buffer, offset, count);
        }

        private void DoWriteNormal(long pos, byte[] buffer, int offset, int count)
        {
            RawWrite(pos, buffer, offset, count);
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

        private void RawWriteZeros(long pos, long count)
        {
            byte[] wipeBuffer = new byte[_bytesPerCluster * 4];

            if ((_flags & AttributeFlags.Sparse) != 0)
            {
                long vcn = pos / _bytesPerCluster;
                int runIdx = FindDataRun(vcn);
                long runOffset = pos - (_cookedRuns[runIdx].StartVcn * _bytesPerCluster);

                long totalWritten = 0;
                while (totalWritten < count)
                {
                    if (_cookedRuns[runIdx].IsSparse)
                    {
                        // If we're in a sparse run, then skip it without writing any data
                        totalWritten += (_cookedRuns[runIdx].Length * _bytesPerCluster) - runOffset;
                        runOffset = 0;
                        runIdx++;
                    }
                    else if (runOffset % _bytesPerCluster != 0 || count - totalWritten < _bytesPerCluster)
                    {
                        // Partial cluster to write
                        int toWrite = (int)Math.Min(wipeBuffer.Length, Math.Min(count - totalWritten, _bytesPerCluster - (runOffset % _bytesPerCluster)));

                        _fsStream.Position = (_cookedRuns[runIdx].StartLcn * _bytesPerCluster) + runOffset;
                        _fsStream.Write(wipeBuffer, 0, toWrite);
                        totalWritten += toWrite;
                        runOffset += toWrite;

                        if (runOffset >= _cookedRuns[runIdx].Length * _bytesPerCluster)
                        {
                            runOffset = 0;
                            runIdx++;
                        }
                    }
                    else
                    {
                        // Convert clusters to sparse
                        vcn = (pos + totalWritten) / _bytesPerCluster;

                        if (_cookedRuns[runIdx].StartVcn != vcn)
                        {
                            _cookedRuns.SplitRun(runIdx, vcn);
                            runIdx++;
                        }

                        long clustersToFree = Math.Min(((count - totalWritten) / _bytesPerCluster), _cookedRuns[runIdx].Length);

                        if (clustersToFree != _cookedRuns[runIdx].Length)
                        {
                            _cookedRuns.SplitRun(runIdx, vcn + clustersToFree);
                        }

                        _file.Context.ClusterBitmap.FreeClusters(new Range<long, long>(_cookedRuns[runIdx].StartLcn, _cookedRuns[runIdx].Length));
                        _cookedRuns.MakeSparse(runIdx);
                        PrimaryAttributeRecord.CompressedDataSize -= clustersToFree * _bytesPerCluster;

                        totalWritten += clustersToFree * _bytesPerCluster;
                        runIdx++;
                        runOffset = 0;
                    }
                }

                _cookedRuns.CollapseRuns();
            }
            else
            {
                long wipePos = pos;
                while (wipePos < pos + count)
                {
                    int numToWrite = (int)Math.Min(wipeBuffer.Length, count - (wipePos - pos));
                    RawWrite(wipePos, wipeBuffer, 0, numToWrite);
                    wipePos += numToWrite;
                }
            }
        }

        private void AddDataRun(long startLcn, long length)
        {
            CookedDataRun lastRun = _cookedRuns.Last;
            NonResidentAttributeRecord lastExtent = (NonResidentAttributeRecord)_attribute.LastExtent;

            if (lastRun != null && !lastRun.IsSparse && lastRun.StartLcn + lastRun.Length == startLcn)
            {
                lastRun.Length += length;
            }
            else
            {
                long lastStartLcn = lastRun != null ? lastRun.StartLcn : 0;
                DataRun newRun = new DataRun(startLcn - lastStartLcn, length, false);
                lastExtent.DataRuns.Add(newRun);
                _cookedRuns.Append(newRun, lastExtent);
            }

            lastExtent.LastVcn = _cookedRuns.NextVirtualCluster - 1;
            PrimaryAttributeRecord.AllocatedLength += length * _bytesPerCluster;
        }

        private void AddSparseDataRun(long length)
        {
            CookedDataRun lastRun = _cookedRuns.Last;
            NonResidentAttributeRecord lastExtent = (NonResidentAttributeRecord)_attribute.LastExtent;

            if (lastRun != null && lastRun.IsSparse)
            {
                lastRun.Length += length;
            }
            else
            {
                DataRun newRun = new DataRun(0, length, true);
                lastExtent.DataRuns.Add(newRun);
                _cookedRuns.Append(newRun, lastExtent);
            }

            lastExtent.LastVcn = _cookedRuns.NextVirtualCluster - 1;
            PrimaryAttributeRecord.AllocatedLength += length * _bytesPerCluster;
        }
    }
}

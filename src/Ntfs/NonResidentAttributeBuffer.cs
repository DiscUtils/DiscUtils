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
            : base(file.Context.RawStream, file.Context.BiosParameterBlock.BytesPerCluster, null, attribute.Flags, attribute.CompressionUnitSize)
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

                _cookedRuns.Append(record.DataRuns);
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

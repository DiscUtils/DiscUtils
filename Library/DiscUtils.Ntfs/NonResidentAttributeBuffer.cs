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

namespace DiscUtils.Ntfs
{
    internal class NonResidentAttributeBuffer : NonResidentDataBuffer
    {
        private readonly NtfsAttribute _attribute;
        private readonly File _file;

        public NonResidentAttributeBuffer(File file, NtfsAttribute attribute)
            : base(file.Context, CookRuns(attribute), file.IndexInMft == MasterFileTable.MftIndex)
        {
            _file = file;
            _attribute = attribute;

            switch (attribute.Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse))
            {
                case AttributeFlags.Sparse:
                    _activeStream = new SparseClusterStream(_attribute, _rawStream);
                    break;

                case AttributeFlags.Compressed:
                    _activeStream = new CompressedClusterStream(_context, _attribute, _rawStream);
                    break;

                case AttributeFlags.None:
                    _activeStream = _rawStream;
                    break;

                default:
                    throw new NotImplementedException("Unhandled attribute type '" + attribute.Flags + "'");
            }
        }

        public override bool CanWrite
        {
            get { return _context.RawStream.CanWrite && _file != null; }
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
            _file.MarkMftRecordDirty();
            _activeStream.ExpandToClusters(MathUtilities.Ceil(_attribute.Length, _bytesPerCluster),
                (NonResidentAttributeRecord)_attribute.LastExtent, false);
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

            long newClusterCount = MathUtilities.Ceil(value, _bytesPerCluster);

            if (value < Capacity)
            {
                Truncate(value);
            }
            else
            {
                _activeStream.ExpandToClusters(newClusterCount, (NonResidentAttributeRecord)_attribute.LastExtent, true);

                PrimaryAttributeRecord.AllocatedLength = _cookedRuns.NextVirtualCluster * _bytesPerCluster;
            }

            PrimaryAttributeRecord.DataLength = value;

            if (PrimaryAttributeRecord.InitializedDataLength > value)
            {
                PrimaryAttributeRecord.InitializedDataLength = value;
            }

            _cookedRuns.CollapseRuns();
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to write to file not opened for write");
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
                InitializeData(pos);
            }

            int allocatedClusters = 0;

            long focusPos = pos;
            while (focusPos < pos + count)
            {
                long vcn = focusPos / _bytesPerCluster;
                long remaining = pos + count - focusPos;
                long clusterOffset = focusPos - vcn * _bytesPerCluster;

                if (vcn * _bytesPerCluster != focusPos || remaining < _bytesPerCluster)
                {
                    // Unaligned or short write
                    int toWrite = (int)Math.Min(remaining, _bytesPerCluster - clusterOffset);

                    _activeStream.ReadClusters(vcn, 1, _ioBuffer, 0);
                    Array.Copy(buffer, (int)(offset + (focusPos - pos)), _ioBuffer, (int)clusterOffset, toWrite);
                    allocatedClusters += _activeStream.WriteClusters(vcn, 1, _ioBuffer, 0);

                    focusPos += toWrite;
                }
                else
                {
                    // Aligned, full cluster writes...
                    int fullClusters = (int)(remaining / _bytesPerCluster);
                    allocatedClusters += _activeStream.WriteClusters(vcn, fullClusters, buffer,
                        (int)(offset + (focusPos - pos)));

                    focusPos += fullClusters * _bytesPerCluster;
                }
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

            if ((_attribute.Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                PrimaryAttributeRecord.CompressedDataSize += allocatedClusters * _bytesPerCluster;
            }

            _cookedRuns.CollapseRuns();
        }

        public override void Clear(long pos, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to erase bytes from file not opened for write");
            }

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
                InitializeData(pos);
            }

            int releasedClusters = 0;

            long focusPos = pos;
            while (focusPos < pos + count)
            {
                long vcn = focusPos / _bytesPerCluster;
                long remaining = pos + count - focusPos;
                long clusterOffset = focusPos - vcn * _bytesPerCluster;

                if (vcn * _bytesPerCluster != focusPos || remaining < _bytesPerCluster)
                {
                    // Unaligned or short write
                    int toClear = (int)Math.Min(remaining, _bytesPerCluster - clusterOffset);

                    if (_activeStream.IsClusterStored(vcn))
                    {
                        _activeStream.ReadClusters(vcn, 1, _ioBuffer, 0);
                        Array.Clear(_ioBuffer, (int)clusterOffset, toClear);
                        releasedClusters -= _activeStream.WriteClusters(vcn, 1, _ioBuffer, 0);
                    }

                    focusPos += toClear;
                }
                else
                {
                    // Aligned, full cluster clears...
                    int fullClusters = (int)(remaining / _bytesPerCluster);
                    releasedClusters += _activeStream.ClearClusters(vcn, fullClusters);

                    focusPos += fullClusters * _bytesPerCluster;
                }
            }

            if (pos + count > PrimaryAttributeRecord.InitializedDataLength)
            {
                PrimaryAttributeRecord.InitializedDataLength = pos + count;
            }

            if (pos + count > PrimaryAttributeRecord.DataLength)
            {
                PrimaryAttributeRecord.DataLength = pos + count;
            }

            if ((_attribute.Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                PrimaryAttributeRecord.CompressedDataSize -= releasedClusters * _bytesPerCluster;
            }

            _cookedRuns.CollapseRuns();
        }

        private static CookedDataRuns CookRuns(NtfsAttribute attribute)
        {
            CookedDataRuns result = new CookedDataRuns();

            foreach (NonResidentAttributeRecord record in attribute.Records)
            {
                if (record.StartVcn != result.NextVirtualCluster)
                {
                    throw new IOException("Invalid NTFS attribute - non-contiguous data runs");
                }

                result.Append(record.DataRuns, record);
            }

            return result;
        }

        private void InitializeData(long pos)
        {
            long initDataLen = PrimaryAttributeRecord.InitializedDataLength;
            _file.MarkMftRecordDirty();

            int clustersAllocated = 0;

            while (initDataLen < pos)
            {
                long vcn = initDataLen / _bytesPerCluster;
                if (initDataLen % _bytesPerCluster != 0 || pos - initDataLen < _bytesPerCluster)
                {
                    int clusterOffset = (int)(initDataLen - vcn * _bytesPerCluster);
                    int toClear = (int)Math.Min(_bytesPerCluster - clusterOffset, pos - initDataLen);

                    if (_activeStream.IsClusterStored(vcn))
                    {
                        _activeStream.ReadClusters(vcn, 1, _ioBuffer, 0);
                        Array.Clear(_ioBuffer, clusterOffset, toClear);
                        clustersAllocated += _activeStream.WriteClusters(vcn, 1, _ioBuffer, 0);
                    }

                    initDataLen += toClear;
                }
                else
                {
                    int numClusters = (int)(pos / _bytesPerCluster - vcn);
                    clustersAllocated -= _activeStream.ClearClusters(vcn, numClusters);

                    initDataLen += numClusters * _bytesPerCluster;
                }
            }

            PrimaryAttributeRecord.InitializedDataLength = pos;

            if ((_attribute.Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                PrimaryAttributeRecord.CompressedDataSize += clustersAllocated * _bytesPerCluster;
            }
        }

        private void Truncate(long value)
        {
            long endVcn = MathUtilities.Ceil(value, _bytesPerCluster);

            // Release the clusters
            _activeStream.TruncateToClusters(endVcn);

            // First, remove any extents that are now redundant.
            Dictionary<AttributeReference, AttributeRecord> extentCache =
                new Dictionary<AttributeReference, AttributeRecord>(_attribute.Extents);
            foreach (KeyValuePair<AttributeReference, AttributeRecord> extent in extentCache)
            {
                if (extent.Value.StartVcn >= endVcn)
                {
                    NonResidentAttributeRecord record = (NonResidentAttributeRecord)extent.Value;
                    _file.RemoveAttributeExtent(extent.Key);
                    _attribute.RemoveExtentCacheSafe(extent.Key);
                }
            }

            PrimaryAttributeRecord.LastVcn = Math.Max(0, endVcn - 1);
            PrimaryAttributeRecord.AllocatedLength = endVcn * _bytesPerCluster;
            PrimaryAttributeRecord.DataLength = value;
            PrimaryAttributeRecord.InitializedDataLength = Math.Min(PrimaryAttributeRecord.InitializedDataLength, value);

            _file.MarkMftRecordDirty();
        }
    }
}
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
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class NonResidentAttributeRecord : AttributeRecord
    {
        private const ushort DefaultCompressionUnitSize = 4;
        private ulong _compressedSize;
        private ushort _compressionUnitSize;
        private ulong _dataAllocatedSize;
        private ulong _dataRealSize;

        private ushort _dataRunsOffset;
        private ulong _initializedDataSize;
        private ulong _lastVCN;

        private ulong _startingVCN;

        public NonResidentAttributeRecord(byte[] buffer, int offset, out int length)
        {
            Read(buffer, offset, out length);
        }

        public NonResidentAttributeRecord(AttributeType type, string name, ushort id, AttributeFlags flags,
                                          long firstCluster, ulong numClusters, uint bytesPerCluster)
            : base(type, name, id, flags)
        {
            _nonResidentFlag = 1;
            DataRuns = new List<DataRun>();
            DataRuns.Add(new DataRun(firstCluster, (long)numClusters, false));
            _lastVCN = numClusters - 1;
            _dataAllocatedSize = bytesPerCluster * numClusters;
            _dataRealSize = bytesPerCluster * numClusters;
            _initializedDataSize = bytesPerCluster * numClusters;

            if ((flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                _compressionUnitSize = DefaultCompressionUnitSize;
            }
        }

        public NonResidentAttributeRecord(AttributeType type, string name, ushort id, AttributeFlags flags,
                                          long startVcn, List<DataRun> dataRuns)
            : base(type, name, id, flags)
        {
            _nonResidentFlag = 1;
            DataRuns = dataRuns;
            _startingVCN = (ulong)startVcn;

            if ((flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                _compressionUnitSize = DefaultCompressionUnitSize;
            }

            if (dataRuns != null && dataRuns.Count != 0)
            {
                _lastVCN = _startingVCN;
                foreach (DataRun run in dataRuns)
                {
                    _lastVCN += (ulong)run.RunLength;
                }

                _lastVCN -= 1;
            }
        }

        /// <summary>
        /// The amount of space occupied by the attribute (in bytes).
        /// </summary>
        public override long AllocatedLength
        {
            get { return (long)_dataAllocatedSize; }
            set { _dataAllocatedSize = (ulong)value; }
        }

        public long CompressedDataSize
        {
            get { return (long)_compressedSize; }
            set { _compressedSize = (ulong)value; }
        }

        /// <summary>
        /// Gets or sets the size of a compression unit (in clusters).
        /// </summary>
        public int CompressionUnitSize
        {
            get { return 1 << _compressionUnitSize; }
            set { _compressionUnitSize = (ushort)MathUtilities.Log2(value); }
        }

        /// <summary>
        /// The amount of data in the attribute (in bytes).
        /// </summary>
        public override long DataLength
        {
            get { return (long)_dataRealSize; }
            set { _dataRealSize = (ulong)value; }
        }

        public List<DataRun> DataRuns { get; private set; }

        /// <summary>
        /// The amount of initialized data in the attribute (in bytes).
        /// </summary>
        public override long InitializedDataLength
        {
            get { return (long)_initializedDataSize; }
            set { _initializedDataSize = (ulong)value; }
        }

        public long LastVcn
        {
            get { return (long)_lastVCN; }
            set { _lastVCN = (ulong)value; }
        }

        public override int Size
        {
            get
            {
                byte nameLength = 0;
                ushort nameOffset =
                    (ushort)((Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0 ? 0x48 : 0x40);
                if (Name != null)
                {
                    nameLength = (byte)Name.Length;
                }

                ushort dataOffset = (ushort)MathUtilities.RoundUp(nameOffset + nameLength * 2, 8);

                // Write out data first, since we know where it goes...
                int dataLen = 0;
                foreach (DataRun run in DataRuns)
                {
                    dataLen += run.Size;
                }

                dataLen++; // NULL terminator

                return MathUtilities.RoundUp(dataOffset + dataLen, 8);
            }
        }

        public override long StartVcn
        {
            get { return (long)_startingVCN; }
        }

        public void ReplaceRun(DataRun oldRun, DataRun newRun)
        {
            int idx = DataRuns.IndexOf(oldRun);
            if (idx < 0)
            {
                throw new ArgumentException("Attempt to replace non-existant run", nameof(oldRun));
            }

            DataRuns[idx] = newRun;
        }

        public int RemoveRun(DataRun run)
        {
            int idx = DataRuns.IndexOf(run);
            if (idx < 0)
            {
                throw new ArgumentException("Attempt to remove non-existant run", nameof(run));
            }

            DataRuns.RemoveAt(idx);
            return idx;
        }

        public void InsertRun(DataRun existingRun, DataRun newRun)
        {
            int idx = DataRuns.IndexOf(existingRun);
            if (idx < 0)
            {
                throw new ArgumentException("Attempt to replace non-existant run", nameof(existingRun));
            }

            DataRuns.Insert(idx + 1, newRun);
        }

        public void InsertRun(int index, DataRun newRun)
        {
            DataRuns.Insert(index, newRun);
        }

        public override Range<long, long>[] GetClusters()
        {
            List<DataRun> cookedRuns = DataRuns;

            long start = 0;
            List<Range<long, long>> result = new List<Range<long, long>>(DataRuns.Count);
            foreach (DataRun run in cookedRuns)
            {
                if (!run.IsSparse)
                {
                    start += run.RunOffset;
                    result.Add(new Range<long, long>(start, run.RunLength));
                }
            }

            return result.ToArray();
        }

        public override IBuffer GetReadOnlyDataBuffer(INtfsContext context)
        {
            return new NonResidentDataBuffer(context, this);
        }

        public override int Write(byte[] buffer, int offset)
        {
            ushort headerLength = 0x40;
            if ((Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                headerLength += 0x08;
            }

            byte nameLength = 0;
            ushort nameOffset = headerLength;
            if (Name != null)
            {
                nameLength = (byte)Name.Length;
            }

            ushort dataOffset = (ushort)MathUtilities.RoundUp(headerLength + nameLength * 2, 8);

            // Write out data first, since we know where it goes...
            int dataLen = 0;
            foreach (DataRun run in DataRuns)
            {
                dataLen += run.Write(buffer, offset + dataOffset + dataLen);
            }

            buffer[offset + dataOffset + dataLen] = 0; // NULL terminator
            dataLen++;

            int length = MathUtilities.RoundUp(dataOffset + dataLen, 8);

            EndianUtilities.WriteBytesLittleEndian((uint)_type, buffer, offset + 0x00);
            EndianUtilities.WriteBytesLittleEndian(length, buffer, offset + 0x04);
            buffer[offset + 0x08] = _nonResidentFlag;
            buffer[offset + 0x09] = nameLength;
            EndianUtilities.WriteBytesLittleEndian(nameOffset, buffer, offset + 0x0A);
            EndianUtilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x0C);
            EndianUtilities.WriteBytesLittleEndian(_attributeId, buffer, offset + 0x0E);

            EndianUtilities.WriteBytesLittleEndian(_startingVCN, buffer, offset + 0x10);
            EndianUtilities.WriteBytesLittleEndian(_lastVCN, buffer, offset + 0x18);
            EndianUtilities.WriteBytesLittleEndian(dataOffset, buffer, offset + 0x20);
            EndianUtilities.WriteBytesLittleEndian(_compressionUnitSize, buffer, offset + 0x22);
            EndianUtilities.WriteBytesLittleEndian((uint)0, buffer, offset + 0x24); // Padding
            EndianUtilities.WriteBytesLittleEndian(_dataAllocatedSize, buffer, offset + 0x28);
            EndianUtilities.WriteBytesLittleEndian(_dataRealSize, buffer, offset + 0x30);
            EndianUtilities.WriteBytesLittleEndian(_initializedDataSize, buffer, offset + 0x38);
            if ((Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                EndianUtilities.WriteBytesLittleEndian(_compressedSize, buffer, offset + 0x40);
            }

            if (Name != null)
            {
                Array.Copy(Encoding.Unicode.GetBytes(Name), 0, buffer, offset + nameOffset, nameLength * 2);
            }

            return length;
        }

        public AttributeRecord Split(int suggestedSplitIdx)
        {
            int splitIdx;
            if (suggestedSplitIdx <= 0 || suggestedSplitIdx >= DataRuns.Count)
            {
                splitIdx = DataRuns.Count / 2;
            }
            else
            {
                splitIdx = suggestedSplitIdx;
            }

            long splitVcn = (long)_startingVCN;
            long splitLcn = 0;
            for (int i = 0; i < splitIdx; ++i)
            {
                splitVcn += DataRuns[i].RunLength;
                splitLcn += DataRuns[i].RunOffset;
            }

            List<DataRun> newRecordRuns = new List<DataRun>();
            while (DataRuns.Count > splitIdx)
            {
                DataRun run = DataRuns[splitIdx];

                DataRuns.RemoveAt(splitIdx);
                newRecordRuns.Add(run);
            }

            // Each extent has implicit start LCN=0, so have to make stored runs match reality.
            // However, take care not to stomp on 'sparse' runs that may be at the start of the
            // new extent (indicated by Zero run offset).
            for (int i = 0; i < newRecordRuns.Count; ++i)
            {
                if (!newRecordRuns[i].IsSparse)
                {
                    newRecordRuns[i].RunOffset += splitLcn;
                    break;
                }
            }

            _lastVCN = (ulong)splitVcn - 1;

            return new NonResidentAttributeRecord(_type, _name, 0, _flags, splitVcn, newRecordRuns);
        }

        public override void Dump(TextWriter writer, string indent)
        {
            base.Dump(writer, indent);
            writer.WriteLine(indent + "     Starting VCN: " + _startingVCN);
            writer.WriteLine(indent + "         Last VCN: " + _lastVCN);
            writer.WriteLine(indent + "   Comp Unit Size: " + _compressionUnitSize);
            writer.WriteLine(indent + "   Allocated Size: " + _dataAllocatedSize);
            writer.WriteLine(indent + "        Real Size: " + _dataRealSize);
            writer.WriteLine(indent + "   Init Data Size: " + _initializedDataSize);
            if ((Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0)
            {
                writer.WriteLine(indent + "  Compressed Size: " + _compressedSize);
            }

            string runStr = string.Empty;

            foreach (DataRun run in DataRuns)
            {
                runStr += " " + run;
            }

            writer.WriteLine(indent + "        Data Runs:" + runStr);
        }

        protected override void Read(byte[] buffer, int offset, out int length)
        {
            DataRuns = null;

            base.Read(buffer, offset, out length);

            _startingVCN = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            _lastVCN = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            _dataRunsOffset = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x20);
            _compressionUnitSize = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x22);
            _dataAllocatedSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x28);
            _dataRealSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x30);
            _initializedDataSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x38);
            if ((Flags & (AttributeFlags.Compressed | AttributeFlags.Sparse)) != 0 && _dataRunsOffset > 0x40)
            {
                _compressedSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x40);
            }

            DataRuns = new List<DataRun>();
            int pos = _dataRunsOffset;
            while (pos < length)
            {
                DataRun run = new DataRun();
                int len = run.Read(buffer, offset + pos);

                // Length 1 means there was only a header byte (i.e. terminator)
                if (len == 1)
                {
                    break;
                }

                DataRuns.Add(run);
                pos += len;
            }
        }
    }
}
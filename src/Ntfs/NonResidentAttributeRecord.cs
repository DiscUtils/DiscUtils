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

namespace DiscUtils.Ntfs
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal sealed class NonResidentAttributeRecord : AttributeRecord
    {
        private ulong _startingVCN;
        private ulong _lastVCN;
        private ushort _dataRunsOffset;
        private ushort _compressionUnitSize;
        private ulong _dataAllocatedSize;
        private ulong _dataRealSize;
        private ulong _initializedDataSize;
        private ulong _compressedSize;

        private List<CookedDataRun> _cookedDataRuns;
        private NonResidentAttributeBuffer _dataBuffer;

        public NonResidentAttributeRecord(byte[] buffer, int offset, out int length)
        {
            Read(buffer, offset, out length);
        }

        public NonResidentAttributeRecord(AttributeType type, string name, ushort id, AttributeFlags flags, long firstCluster, ulong numClusters, uint bytesPerCluster)
            : base(type, name, id, flags)
        {
            _nonResidentFlag = 1;
            _cookedDataRuns = new List<CookedDataRun>();
            _cookedDataRuns.Add(new CookedDataRun(new DataRun(firstCluster, (long)numClusters), 0, 0));
            _lastVCN = numClusters - 1;
            _dataAllocatedSize = bytesPerCluster * numClusters;
            _dataRealSize = bytesPerCluster * numClusters;
            _initializedDataSize = bytesPerCluster * numClusters;
        }

        public NonResidentAttributeRecord(AttributeType type, string name, ushort id, AttributeFlags flags, List<CookedDataRun> dataRuns)
            : base(type, name, id, flags)
        {
            _nonResidentFlag = 1;
            _cookedDataRuns = dataRuns;

            if (dataRuns != null && dataRuns.Count != 0)
            {
                CookedDataRun lastRun = dataRuns[dataRuns.Count - 1];

                _startingVCN = (ulong)dataRuns[0].StartVcn;
                _lastVCN = (ulong)(lastRun.StartVcn + lastRun.Length - 1);
            }
        }

        /// <summary>
        /// The amount of space occupied by the attribute (in bytes)
        /// </summary>
        public override long AllocatedLength
        {
            get { return (long)_dataAllocatedSize; }
            set { _dataAllocatedSize = (ulong)value; }
        }

        /// <summary>
        /// The amount of data in the attribute (in bytes)
        /// </summary>
        public override long DataLength
        {
            get { return (long)_dataRealSize; }
            set { _dataRealSize = (ulong)value; }
        }

        /// <summary>
        /// The amount of initialized data in the attribute (in bytes)
        /// </summary>
        public override long InitializedDataLength
        {
            get { return (long)_initializedDataSize; }
            set { _initializedDataSize = (ulong)value; }
        }

        public override long StartVcn
        {
            get { return (long)_startingVCN; }
        }

        public long LastVcn
        {
            get { return (long)_lastVCN; }
            set { _lastVCN = (ulong)value; }
        }

        /// <summary>
        /// Gets the size of a compression unit (in clusters)
        /// </summary>
        public int CompressionUnitSize
        {
            get { return 1 << _compressionUnitSize; }
        }

        public List<CookedDataRun> CookedDataRuns
        {
            get { return _cookedDataRuns; }
        }

        public long NextCluster
        {
            get
            {
                var cookedRuns = CookedDataRuns;
                if (cookedRuns.Count > 0)
                {
                    CookedDataRun lastRun = cookedRuns[cookedRuns.Count - 1];
                    return lastRun.StartLcn + lastRun.Length;
                }
                else
                {
                    return -1;
                }
            }
        }

        public override int Size
        {
            get
            {
                byte nameLength = 0;
                ushort nameOffset = (ushort)(((Flags & AttributeFlags.Compressed) != 0) ? 0x48 : 0x40);
                if (Name != null)
                {
                    nameLength = (byte)Name.Length;
                }

                ushort dataOffset = (ushort)Utilities.RoundUp(nameOffset + (nameLength * 2), 8);

                // Write out data first, since we know where it goes...
                int dataLen = 0;
                foreach (var run in _cookedDataRuns)
                {
                    dataLen += run.DataRun.Size;
                }

                dataLen++; // NULL terminator

                return Utilities.RoundUp(dataOffset + dataLen, 8);
            }
        }

        public override Range<long, long>[] GetClusters()
        {
            var cookedRuns = CookedDataRuns;

            List<Range<long, long>> result = new List<Range<long, long>>(cookedRuns.Count);
            foreach (var run in cookedRuns)
            {
                if (!run.IsSparse)
                {
                    result.Add(new Range<long, long>(run.StartLcn, run.Length));
                }
            }

            return result.ToArray();
        }

        public override IBuffer GetDataBuffer(File file)
        {
            if (_dataBuffer == null)
            {
                _dataBuffer = new NonResidentAttributeBuffer(file, this);
            }

            return _dataBuffer;
        }

        public override IBuffer GetReadOnlyDataBuffer(INtfsContext context)
        {
            return new NonResidentAttributeBuffer(context, this);
        }

        public override long OffsetToAbsolutePos(long offset, long recordStart, int bytesPerCluster)
        {
            var cookedRuns = CookedDataRuns;

            int i = 0;
            while (i < cookedRuns[i].Length)
            {
                if (cookedRuns[i].StartVcn * bytesPerCluster > offset)
                {
                    return -1;
                }
                else if ((cookedRuns[i].StartVcn + cookedRuns[i].Length) * bytesPerCluster > offset)
                {
                    return (offset - (cookedRuns[i].StartVcn * bytesPerCluster)) + (cookedRuns[i].StartLcn * bytesPerCluster);
                }

                ++i;
            }

            return -1;
        }

        public override int Write(byte[] buffer, int offset)
        {
            ushort headerLength = 0x40;
            if ((Flags & AttributeFlags.Compressed) != 0)
            {
                headerLength += 0x08;
            }

            byte nameLength = 0;
            ushort nameOffset = headerLength;
            if (Name != null)
            {
                nameLength = (byte)Name.Length;
            }

            ushort dataOffset = (ushort)Utilities.RoundUp(headerLength + (nameLength * 2), 8);

            // Write out data first, since we know where it goes...
            int dataLen = 0;
            foreach (var run in _cookedDataRuns)
            {
                dataLen += run.DataRun.Write(buffer, offset + dataOffset + dataLen);
            }

            buffer[offset + dataOffset + dataLen] = 0; // NULL terminator
            dataLen++;

            int length = (int)Utilities.RoundUp(dataOffset + dataLen, 8);

            Utilities.WriteBytesLittleEndian((uint)_type, buffer, offset + 0x00);
            Utilities.WriteBytesLittleEndian(length, buffer, offset + 0x04);
            buffer[offset + 0x08] = _nonResidentFlag;
            buffer[offset + 0x09] = nameLength;
            Utilities.WriteBytesLittleEndian(nameOffset, buffer, offset + 0x0A);
            Utilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x0C);
            Utilities.WriteBytesLittleEndian(_attributeId, buffer, offset + 0x0E);

            Utilities.WriteBytesLittleEndian(_startingVCN, buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian(_lastVCN, buffer, offset + 0x18);
            Utilities.WriteBytesLittleEndian(dataOffset, buffer, offset + 0x20);
            Utilities.WriteBytesLittleEndian(_compressionUnitSize, buffer, offset + 0x22);
            Utilities.WriteBytesLittleEndian((uint)0, buffer, offset + 0x24); // Padding
            Utilities.WriteBytesLittleEndian(_dataAllocatedSize, buffer, offset + 0x28);
            Utilities.WriteBytesLittleEndian(_dataRealSize, buffer, offset + 0x30);
            Utilities.WriteBytesLittleEndian(_initializedDataSize, buffer, offset + 0x38);
            if ((Flags & AttributeFlags.Compressed) != 0)
            {
                Utilities.WriteBytesLittleEndian(_compressedSize, buffer, offset + 0x40);
            }

            if (Name != null)
            {
                Array.Copy(Encoding.Unicode.GetBytes(Name), 0, buffer, offset + nameOffset, nameLength * 2);
            }

            return length;
        }

        public override AttributeRecord Split(FileRecord fileRecord)
        {
            int splitIdx = _cookedDataRuns.Count / 2;

            List<CookedDataRun> newRecordRuns = new List<CookedDataRun>();
            while (_cookedDataRuns.Count > splitIdx)
            {
                newRecordRuns.Add(_cookedDataRuns[splitIdx]);
                _cookedDataRuns.RemoveAt(splitIdx);
            }

            newRecordRuns[0].DataRun.RunOffset = newRecordRuns[0].StartLcn;

            CookedDataRun lastRemRun = _cookedDataRuns[_cookedDataRuns.Count - 1];
            _lastVCN = (ulong)(lastRemRun.StartVcn + lastRemRun.Length - 1);

            return new NonResidentAttributeRecord(_type, _name, 0, _flags, newRecordRuns);
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
            if ((Flags & AttributeFlags.Compressed) != 0)
            {
                writer.WriteLine(indent + "  Compressed Size: " + _compressedSize);
            }

            string runStr = string.Empty;

            foreach (CookedDataRun run in _cookedDataRuns)
            {
                runStr += " " + run.DataRun.ToString();
            }

            writer.WriteLine(indent + "        Data Runs:" + runStr);
        }

        protected override void Read(byte[] buffer, int offset, out int length)
        {
            _cookedDataRuns = null;

            base.Read(buffer, offset, out length);

            _startingVCN = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            _lastVCN = Utilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            _dataRunsOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x20);
            _compressionUnitSize = Utilities.ToUInt16LittleEndian(buffer, offset + 0x22);
            _dataAllocatedSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x28);
            _dataRealSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x30);
            _initializedDataSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x38);
            if ((Flags & AttributeFlags.Compressed) != 0)
            {
                _compressedSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x40);
            }

            var dataRuns = new List<DataRun>();
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

                dataRuns.Add(run);
                pos += len;
            }

            _cookedDataRuns = CookedDataRun.Cook(dataRuns);
        }
    }
}

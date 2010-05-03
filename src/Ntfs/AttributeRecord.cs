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
using System.Text;

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum AttributeFlags : ushort
    {
        None = 0x0000,
        Compressed = 0x0001,
        Encrypted = 0x4000,
        Sparse = 0x8000
    }

    internal abstract class AttributeRecord : IComparable<AttributeRecord>
    {
        protected AttributeType _type;
        protected byte _nonResidentFlag;
        protected AttributeFlags _flags;
        protected ushort _attributeId;

        protected string _name;

        public AttributeRecord()
        {
        }

        public AttributeRecord(AttributeType type, string name, ushort id, AttributeFlags flags)
        {
            _type = type;
            _name = name;
            _attributeId = id;
            _flags = flags;
        }

        public AttributeType AttributeType
        {
            get { return _type; }
        }

        public ushort AttributeId
        {
            get { return _attributeId; }
            set { _attributeId = value; }
        }

        public abstract long AllocatedLength
        {
            get;
            set;
        }

        public abstract long StartVcn
        {
            get;
        }

        public abstract long DataLength
        {
            get;
            set;
        }

        public abstract long InitializedDataLength
        {
            get;
            set;
        }

        public bool IsNonResident
        {
            get { return _nonResidentFlag != 0; }
        }

        public string Name
        {
            get { return _name; }
        }

        public AttributeFlags Flags
        {
            get { return _flags; }
        }

        public abstract Range<long, long>[] GetClusters();

        public abstract IBuffer GetDataBuffer(File file);

        public abstract long OffsetToAbsolutePos(long offset, long recordStart, int bytesPerCluster);

        public static AttributeRecord FromBytes(byte[] buffer, int offset, out int length)
        {
            if (Utilities.ToUInt32LittleEndian(buffer, offset) == 0xFFFFFFFF)
            {
                length = 0;
                return null;
            }
            else if (buffer[offset + 0x08] != 0x00)
            {
                return new NonResidentAttributeRecord(buffer, offset, out length);
            }
            else
            {
                return new ResidentAttributeRecord(buffer, offset, out length);
            }
        }

        public int CompareTo(AttributeRecord other)
        {
            int val = ((int)_type) - (int)other._type;
            if(val != 0)
            {
                return val;
            }

            val = string.Compare(_name, other._name, StringComparison.OrdinalIgnoreCase);
            if (val != 0)
            {
                return val;
            }

            return ((int)_attributeId) - (int)other._attributeId;
        }


        protected virtual void Read(byte[] buffer, int offset, out int length)
        {
            _type = (AttributeType)Utilities.ToUInt32LittleEndian(buffer, offset + 0x00);
            length = Utilities.ToInt32LittleEndian(buffer, offset + 0x04);

            _nonResidentFlag = buffer[offset + 0x08];
            byte nameLength = buffer[offset + 0x09];
            ushort nameOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            _flags = (AttributeFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x0C);
            _attributeId = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0E);

            if (nameLength != 0x00)
            {
                if (nameLength + nameOffset > length)
                {
                    throw new IOException("Corrupt attribute, name outside of attribute");
                }

                _name = Encoding.Unicode.GetString(buffer, offset + nameOffset, nameLength * 2);
            }
        }

        public abstract int Write(byte[] buffer, int offset);
        public abstract int Size { get; }
        public abstract AttributeRecord Split(FileRecord fileRecord);

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE RECORD");
            writer.WriteLine(indent + "            Type: " + _type);
            writer.WriteLine(indent + "    Non-Resident: " + _nonResidentFlag);
            writer.WriteLine(indent + "            Name: " + _name);
            writer.WriteLine(indent + "           Flags: " + _flags);
            writer.WriteLine(indent + "     AttributeId: " + _attributeId);
        }

    }

    internal sealed class ResidentAttributeRecord : AttributeRecord
    {
        private byte _indexedFlag;
        private SparseMemoryBuffer _memoryBuffer;

        public ResidentAttributeRecord(byte[] buffer, int offset, out int length)
        {
            Read(buffer, offset, out length);
        }

        public ResidentAttributeRecord(AttributeType type, string name, ushort id, bool indexed, AttributeFlags flags)
            : base(type, name, id, flags)
        {
            base._nonResidentFlag = 0;
            _indexedFlag = (byte)(indexed ? 1 : 0);
            _memoryBuffer = new SparseMemoryBuffer(1024);
        }

        public override long AllocatedLength
        {
            get { return Utilities.RoundUp(DataLength, 8); }
            set { throw new NotSupportedException(); }
        }

        public override long StartVcn
        {
            get { return 0; }
        }

        public override long DataLength
        {
            get { return _memoryBuffer.Capacity; }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// The amount of initialized data in the attribute (in bytes)
        /// </summary>
        public override long InitializedDataLength
        {
            get { return (long)DataLength; }
            set { throw new NotSupportedException(); }
        }

        public override IBuffer GetDataBuffer(File file)
        {
            return _memoryBuffer;
        }

        public override long OffsetToAbsolutePos(long offset, long recordStart, int bytesPerCluster)
        {
            return recordStart + DataOffset + offset;
        }

        public override Range<long, long>[] GetClusters()
        {
            return new Range<long, long>[0];
        }

        protected override void Read(byte[] buffer, int offset, out int length)
        {
            base.Read(buffer, offset, out length);

            uint dataLength = Utilities.ToUInt32LittleEndian(buffer, offset + 0x10);
            ushort dataOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x14);
            _indexedFlag = buffer[offset + 0x16];

            if (dataOffset + dataLength > length)
            {
                throw new IOException("Corrupt attribute, data outside of attribute");
            }

            _memoryBuffer = new SparseMemoryBuffer(1024);
            _memoryBuffer.Write(0, buffer, offset + dataOffset, (int)dataLength);
        }

        public override int Write(byte[] buffer, int offset)
        {
            byte nameLength = 0;
            ushort nameOffset = 0;
            if (Name != null)
            {
                nameOffset = 0x18;
                nameLength = (byte)Name.Length;
            }

            ushort dataOffset = (ushort)Utilities.RoundUp(0x18 + (nameLength * 2), 8);
            int length = (int)Utilities.RoundUp(dataOffset + _memoryBuffer.Capacity, 8);

            Utilities.WriteBytesLittleEndian((uint)_type, buffer, offset + 0x00);
            Utilities.WriteBytesLittleEndian(length, buffer, offset + 0x04);
            buffer[offset + 0x08] = _nonResidentFlag;
            buffer[offset + 0x09] = nameLength;
            Utilities.WriteBytesLittleEndian(nameOffset, buffer, offset + 0x0A);
            Utilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x0C);
            Utilities.WriteBytesLittleEndian(_attributeId, buffer, offset + 0x0E);
            Utilities.WriteBytesLittleEndian((int)_memoryBuffer.Capacity, buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian(dataOffset, buffer, offset + 0x14);
            buffer[offset + 0x16] = _indexedFlag;
            buffer[offset + 0x17] = 0; // Padding

            if (Name != null)
            {
                Array.Copy(Encoding.Unicode.GetBytes(Name), 0, buffer, offset + nameOffset, nameLength * 2);
            }
            _memoryBuffer.Read(0, buffer, offset + dataOffset, (int)_memoryBuffer.Capacity);

            return (int)length;
        }

        public override int Size
        {
            get
            {
                byte nameLength = 0;
                ushort nameOffset = 0x18;
                if (Name != null)
                {
                    nameLength = (byte)Name.Length;
                }

                ushort dataOffset = (ushort)Utilities.RoundUp(nameOffset + (nameLength * 2), 8);
                return (int)Utilities.RoundUp(dataOffset + _memoryBuffer.Capacity, 8);
            }
        }

        public int DataOffset
        {
            get
            {
                byte nameLength = 0;
                if (Name != null)
                {
                    nameLength = (byte)Name.Length;
                }
                
                return Utilities.RoundUp(0x18 + (nameLength * 2), 8);
            }
        }

        public override AttributeRecord Split(FileRecord fileRecord)
        {
            throw new InvalidOperationException("Attempting to split resident attribute");
        }

        public override void Dump(TextWriter writer, string indent)
        {
            base.Dump(writer, indent);
            writer.WriteLine(indent + "     Data Length: " + DataLength);
            writer.WriteLine(indent + "         Indexed: " + _indexedFlag);
        }
    }

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
            base._nonResidentFlag = 1;
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
            base._nonResidentFlag = 1;
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
            set { _lastVCN = (ulong)value; }
            get { return (long)_lastVCN; }
        }

        /// <summary>
        /// Size of a compression unit (in clusters)
        /// </summary>
        public int CompressionUnitSize
        {
            get { return 1 << _compressionUnitSize; }
        }

        public List<CookedDataRun> CookedDataRuns
        {
            get { return _cookedDataRuns; }
        }

        public override Range<long, long>[] GetClusters()
        {
            var cookedRuns = CookedDataRuns;

            List<Range<long, long>> result = new List<Range<long, long>>(cookedRuns.Count);
            foreach(var run in cookedRuns)
            {
                if (!run.IsSparse)
                {
                    result.Add(new Range<long, long>(run.StartLcn, run.Length));
                }
            }

            return result.ToArray();
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

        public override IBuffer GetDataBuffer(File file)
        {
            if (_dataBuffer == null)
            {
                _dataBuffer = new NonResidentAttributeBuffer(file, this);
            }
            return _dataBuffer;
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

        public override AttributeRecord Split(FileRecord fileRecord)
        {
            int splitIdx = _cookedDataRuns.Count / 2;

            List<CookedDataRun> newRecordRuns = new List<CookedDataRun>();
            while(_cookedDataRuns.Count > splitIdx)
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

            string runStr = "";

            foreach (CookedDataRun run in _cookedDataRuns)
            {
                runStr += " " + run.DataRun.ToString();
            }

            writer.WriteLine(indent + "        Data Runs:" + runStr);
        }
    }

    internal class CookedDataRun
    {
        private long _startVcn;
        private long _startLcn;
        private DataRun _raw;

        public CookedDataRun(DataRun raw, long startVcn, long prevLcn)
        {
            _raw = raw;
            _startVcn = startVcn;
            _startLcn = prevLcn + raw.RunOffset;

            if (startVcn < 0)
            {
                throw new ArgumentOutOfRangeException("startVcn", startVcn, "VCN must be >= 0");
            }
            if (_startLcn < 0)
            {
                throw new ArgumentOutOfRangeException("prevLcn", prevLcn, "LCN must be >= 0");
            }
        }

        public long StartVcn
        {
            get { return _startVcn; }
        }

        public long StartLcn
        {
            get { return _startLcn; }
        }

        public long Length
        {
            get { return _raw.RunLength; }
            set { _raw.RunLength = value; }
        }

        public bool IsSparse
        {
            get { return _raw.IsSparse; }
        }

        public DataRun DataRun
        {
            get { return _raw; }
        }

        public static List<CookedDataRun> Cook(List<DataRun> runs)
        {
            List<CookedDataRun> result = new List<CookedDataRun>(runs.Count);

            long vcn = 0;
            long lcn = 0;
            for (int i = 0; i < runs.Count; ++i)
            {
                result.Add(new CookedDataRun(runs[i], vcn, lcn));
                vcn += runs[i].RunLength;
                lcn += runs[i].RunOffset;
            }

            return result;
        }

    }

}

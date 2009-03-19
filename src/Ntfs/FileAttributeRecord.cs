//
// Copyright (c) 2008-2009, Kenneth Bell
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
using DiscUtils.Ntfs.Attributes;

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum FileAttributeFlags : ushort
    {
        None = 0x0000,
        Compressed = 0x0001,
        Encrypted = 0x4000,
        Sparse = 0x8000
    }

    internal abstract class FileAttributeRecord
    {
        protected AttributeType _type;
        protected byte _nonResidentFlag;
        protected FileAttributeFlags _flags;
        protected ushort _attributeId;

        protected string _name;

        protected int _debug_bufPos;
        protected int _debug_length;

        public FileAttributeRecord()
        {
        }

        public FileAttributeRecord(FileAttributeRecord toCopy)
        {
            _type = toCopy._type;
            _nonResidentFlag = toCopy._nonResidentFlag;
            _flags = toCopy._flags;
            _attributeId = toCopy._attributeId;
            _name = toCopy._name;
        }

        public FileAttributeRecord(AttributeType type, string name, ushort id)
        {
            _type = type;
            _name = name;
            _attributeId = id;
        }

        public AttributeType AttributeType
        {
            get { return _type; }
        }

        public abstract long AllocatedLength
        {
            get;
            set;
        }

        public abstract long DataLength
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

        public FileAttributeFlags Flags
        {
            get { return _flags; }
        }

        public abstract SparseStream Open(ClusterBitmap clusterBitmap, Stream rawStream, long bytesPerCluster, FileAccess access);

        public static FileAttributeRecord FromBytes(byte[] buffer, int offset, out int length)
        {
            if (Utilities.ToUInt32LittleEndian(buffer, offset) == 0xFFFFFFFF)
            {
                length = 0;
                return null;
            }
            else if (buffer[offset + 0x08] != 0x00)
            {
                return new NonResidentFileAttributeRecord(buffer, offset, out length);
            }
            else
            {
                return new ResidentFileAttributeRecord(buffer, offset, out length);
            }
        }

        protected virtual void Read(byte[] buffer, int offset, out int length)
        {
            _debug_bufPos = offset;

            _type = (AttributeType)Utilities.ToUInt32LittleEndian(buffer, offset + 0x00);
            length = Utilities.ToInt32LittleEndian(buffer, offset + 0x04);

            _debug_length = length;

            _nonResidentFlag = buffer[offset + 0x08];
            byte nameLength = buffer[offset + 0x09];
            ushort nameOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            _flags = (FileAttributeFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x0C);
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

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE RECORD");
            writer.WriteLine(indent + "            Type: " + _type);
            writer.WriteLine(indent + "    Non-Resident: " + _nonResidentFlag);
            writer.WriteLine(indent + "            Name: " + _name);
            writer.WriteLine(indent + "           Flags: " + _flags);
            writer.WriteLine(indent + "     AttributeId: " + _attributeId);
            writer.WriteLine(indent + "   DEBUG: bufPos: " + _debug_bufPos);
            writer.WriteLine(indent + "   DEBUG: length: " + _debug_length);
            if (_nonResidentFlag == 0)
            {
                BaseAttribute.FromRecord(null, this).Dump(writer, indent + "  ");
            }
            else
            {
                new NonResidentAttribute(this).Dump(writer, indent + "  ");
            }
        }

    }

    internal sealed class ResidentFileAttributeRecord : FileAttributeRecord
    {
        private byte _indexedFlag;
        private SparseMemoryBuffer _memoryBuffer;

        public ResidentFileAttributeRecord(byte[] buffer, int offset, out int length)
        {
            Read(buffer, offset, out length);
        }

        public ResidentFileAttributeRecord(FileAttributeRecord toCopy)
            : base(toCopy)
        {
            base._nonResidentFlag = 0;
            _memoryBuffer = new SparseMemoryBuffer(1024);
        }

        public ResidentFileAttributeRecord(AttributeType type, string name, ushort id, bool indexed)
            : base(type, name, id)
        {
            base._nonResidentFlag = 0;
            _indexedFlag = (byte)(indexed ? 1 : 0);
            _memoryBuffer = new SparseMemoryBuffer(1024);
        }

        public override long AllocatedLength
        {
            get { return DataLength; }
            set { DataLength = value; }
        }

        public override long DataLength
        {
            get { return _memoryBuffer.Capacity; }
            set { throw new NotSupportedException(); }
        }

        public byte[] GetData()
        {
            byte[] buffer = new byte[_memoryBuffer.Capacity];
            _memoryBuffer.Read(0, buffer, 0, buffer.Length);
            return buffer;
        }

        public override SparseStream Open(ClusterBitmap clusterBitmap, Stream rawStream, long bytesPerCluster, FileAccess access)
        {
            return new SparseMemoryStream(_memoryBuffer, access);
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

        public override void Dump(TextWriter writer, string indent)
        {
            base.Dump(writer, indent);
            writer.WriteLine(indent + "     Data Length: " + DataLength);
            writer.WriteLine(indent + "         Indexed: " + _indexedFlag);
        }
    }

    internal sealed class NonResidentFileAttributeRecord : FileAttributeRecord
    {
        private ulong _startingVCN;
        private ulong _lastVCN;
        private ushort _dataRunsOffset;
        private ushort _compressionUnitSize;
        private ulong _dataAllocatedSize;
        private ulong _dataRealSize;
        private ulong _initializedDataSize;

        private List<DataRun> _dataRuns;

        public NonResidentFileAttributeRecord(byte[] buffer, int offset, out int length)
        {
            Read(buffer, offset, out length);
        }

        public NonResidentFileAttributeRecord(FileAttributeRecord toCopy)
            : base(toCopy)
        {
            base._nonResidentFlag = 1;
            _dataRuns = new List<DataRun>();
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
        public long RealLength
        {
            get { return (long)_dataRealSize; }
            set { _dataRealSize = (ulong)value; }
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
        public long InitializedDataLength
        {
            get { return (long)_initializedDataSize; }
            set { _initializedDataSize = (ulong)value; }
        }

        public long LastVcn
        {
            get { return (long)_lastVCN; }
            set { _lastVCN = (ulong)value; }
        }

        /// <summary>
        /// Size of a compression unit (in clusters)
        /// </summary>
        public int CompressionUnitSize
        {
            get { return 1 << _compressionUnitSize; }
        }

        public List<DataRun> DataRuns
        {
            get { return _dataRuns; }
        }

        public override SparseStream Open(ClusterBitmap clusterBitmap, Stream rawStream, long bytesPerCluster, FileAccess access)
        {
            return new NonResidentAttributeStream(rawStream, clusterBitmap, bytesPerCluster, access, this);
        }

        protected override void Read(byte[] buffer, int offset, out int length)
        {
            base.Read(buffer, offset, out length);

            _startingVCN = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            _lastVCN = Utilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            _dataRunsOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x20);
            _compressionUnitSize = Utilities.ToUInt16LittleEndian(buffer, offset + 0x22);
            _dataAllocatedSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x28);
            _dataRealSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x30);
            _initializedDataSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x38);

            _dataRuns = new List<DataRun>();
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

                _dataRuns.Add(run);
                pos += len;
            }
        }

        public override int Write(byte[] buffer, int offset)
        {
            byte nameLength = 0;
            ushort nameOffset = 0;
            if (Name != null)
            {
                nameOffset = 0x40;
                nameLength = (byte)Name.Length;
            }
            ushort dataOffset = (ushort)Utilities.RoundUp(0x40 + (nameLength * 2), 8);

            // Write out data first, since we know where it goes...
            int dataLen = 0;
            foreach (var run in _dataRuns)
            {
                dataLen += run.Write(buffer, offset + dataOffset + dataLen);
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
                ushort nameOffset = 0x40;
                if (Name != null)
                {
                    nameLength = (byte)Name.Length;
                }
                ushort dataOffset = (ushort)Utilities.RoundUp(nameOffset + (nameLength * 2), 8);

                // Write out data first, since we know where it goes...
                int dataLen = 0;
                foreach (var run in _dataRuns)
                {
                    dataLen += run.Size;
                }
                dataLen++; // NULL terminator

                return Utilities.RoundUp(dataOffset + dataLen, 8);
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            base.Dump(writer, indent);
            writer.WriteLine(indent + "    Starting VCN: " + _startingVCN);
            writer.WriteLine(indent + "        Last VCN: " + _lastVCN);
            writer.WriteLine(indent + "  Comp Unit Size: " + _compressionUnitSize);
            writer.WriteLine(indent + "  Allocated Size: " + _dataAllocatedSize);
            writer.WriteLine(indent + "       Real Size: " + _dataRealSize);
            writer.WriteLine(indent + "  Init Data Size: " + _initializedDataSize);

            string runStr = "";

            foreach (DataRun run in _dataRuns)
            {
                runStr += " " + run.ToString();
            }

            writer.WriteLine(indent + "       Data Runs:" + runStr);
        }

    }

}

//
// Copyright (c) 2008, Kenneth Bell
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
        protected uint _length;
        protected byte _nonResidentFlag;
        protected byte _nameLength;
        protected ushort _nameOffset;
        protected FileAttributeFlags _flags;
        protected ushort _attributeId;

        protected string _name;

        public AttributeType AttributeType
        {
            get { return _type; }
        }

        public uint Length
        {
            get { return _length; }
        }

        public abstract long DataLength
        {
            get;
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

        public abstract Stream Open(Stream rawStream, long bytesPerCluster, FileAccess access);

        public static FileAttributeRecord FromBytes(byte[] buffer, int offset)
        {
            if (Utilities.ToUInt32LittleEndian(buffer, offset) == 0xFFFFFFFF)
            {
                return null;
            }
            else if (buffer[offset + 0x08] != 0x00)
            {
                return new NonResidentFileAttributeRecord(buffer, offset);
            }
            else
            {
                return new ResidentFileAttributeRecord(buffer, offset);
            }
        }

        public virtual void Read(byte[] buffer, int offset)
        {
            _type = (AttributeType)Utilities.ToUInt32LittleEndian(buffer, offset + 0x00);
            _length = Utilities.ToUInt32LittleEndian(buffer, offset + 0x04);
            _nonResidentFlag = buffer[offset + 0x08];
            _nameLength = buffer[offset + 0x09];
            _nameOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            _flags = (FileAttributeFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x0C);
            _attributeId = Utilities.ToUInt16LittleEndian(buffer, offset + 0x0E);

            if (_nameLength != 0x00)
            {
                if (_nameLength + _nameOffset > _length)
                {
                    throw new IOException("Corrupt attribute, name outside of attribute");
                }

                _name = Encoding.Unicode.GetString(buffer, offset + _nameOffset, _nameLength * 2);
            }
        }

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE RECORD");
            writer.WriteLine(indent + "            Type: " + _type);
            writer.WriteLine(indent + "          Length: " + _length);
            writer.WriteLine(indent + "    Non-Resident: " + _nonResidentFlag);
            writer.WriteLine(indent + "            Name: " + _name);
            writer.WriteLine(indent + "           Flags: " + _flags);
            writer.WriteLine(indent + "     AttributeId: " + _attributeId);
            if (_nonResidentFlag == 0)
            {
                FileAttribute.FromRecord(null, this).Dump(writer, indent + "  ");
            }
            else
            {
                new NonResidentFileAttribute(this).Dump(writer, indent + "  ");
            }
        }
    }

    internal sealed class ResidentFileAttributeRecord : FileAttributeRecord
    {
        private uint _dataLength;
        private ushort _dataOffset;
        private byte _indexedFlag;
        private byte[] _data;

        public ResidentFileAttributeRecord(byte[] buffer, int offset)
        {
            Read(buffer, offset);
        }

        public override long DataLength
        {
            get { return _dataLength; }
        }

        public byte[] Data
        {
            get { return _data; }
        }

        public override Stream Open(Stream rawStream, long bytesPerCluster, FileAccess access)
        {
            return new MemoryStream(_data, 0, _data.Length, access != FileAccess.Read, false);
        }

        public override void Read(byte[] buffer, int offset)
        {
            base.Read(buffer, offset);

            _dataLength = Utilities.ToUInt32LittleEndian(buffer, offset + 0x10);
            _dataOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x14);
            _indexedFlag = buffer[offset + 0x16];

            if (_dataOffset + _dataLength > _length)
            {
                throw new IOException("Corrupt attribute, data outside of attribute");
            }

            _data = new byte[_dataLength];
            Array.Copy(buffer, offset + _dataOffset, _data, 0, _dataLength);
        }

        public override void Dump(TextWriter writer, string indent)
        {
            base.Dump(writer, indent);
            writer.WriteLine(indent + "     Data Length: " + _dataLength);
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

        public NonResidentFileAttributeRecord(byte[] buffer, int offset)
        {
            Read(buffer, offset);
        }

        /// <summary>
        /// The amount of space occupied by the attribute (in bytes)
        /// </summary>
        public long AllocatedLength
        {
            get { return (long)_dataAllocatedSize; }
        }

        /// <summary>
        /// The amount of data in the attribute (in bytes)
        /// </summary>
        public override long DataLength
        {
            get { return (long)_dataRealSize; }
        }

        /// <summary>
        /// The amount of initialized data in the attribute (in bytes)
        /// </summary>
        public long InitializedDataLength
        {
            get { return (long)_initializedDataSize; }
        }

        /// <summary>
        /// Size of a compression unit (in clusters)
        /// </summary>
        public int CompressionUnitSize
        {
            get { return 1 << _compressionUnitSize; }
        }

        public DataRun[] DataRuns
        {
            get { return _dataRuns.ToArray(); }
        }

        public override Stream Open(Stream rawStream, long bytesPerCluster, FileAccess access)
        {
            return new NonResidentAttributeStream(rawStream, bytesPerCluster, access, this);
        }

        public override void Read(byte[] buffer, int offset)
        {
            base.Read(buffer, offset);

            _startingVCN = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            _lastVCN = Utilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            _dataRunsOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x20);
            _compressionUnitSize = Utilities.ToUInt16LittleEndian(buffer, offset + 0x22);
            _dataAllocatedSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x28);
            _dataRealSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x30);
            _initializedDataSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x38);

            _dataRuns = new List<DataRun>();
            int pos = _dataRunsOffset;
            while (pos < _length)
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

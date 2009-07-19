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
using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum FileRecordFlags : ushort
    {
        None = 0x0000,
        InUse = 0x0001,
        IsDirectory = 0x0002,
        IsMetaFile = 0x0004,
        HasViewIndex = 0x0008
    }

    internal class FileRecord : FixupRecordBase
    {
        private ulong _logFileSequenceNumber;
        private ushort _sequenceNumber;
        private ushort _hardLinkCount;
        private ushort _firstAttributeOffset;
        private FileRecordFlags _flags;
        private uint _recordRealSize;
        private uint _recordAllocatedSize;
        private FileReference _baseFile;
        private ushort _nextAttributeId;
        private uint _index; // Self-reference (on XP+)
        private List<AttributeRecord> _attributes;

        private bool _haveIndex;

        public FileRecord(int sectorSize)
            : base("FILE", sectorSize)
        {
        }

        public FileRecord(int sectorSize, int recordLength, uint index)
            : base("FILE", sectorSize, recordLength)
        {
            ReInitialize(sectorSize, recordLength, index);
        }

        public void ReInitialize(int sectorSize, int recordLength, uint index)
        {
            Initialize("FILE", sectorSize, recordLength);
            _sequenceNumber++;
            _flags = FileRecordFlags.InUse;
            _recordAllocatedSize = (uint)recordLength;
            _nextAttributeId = 1;
            _index = index;
            _hardLinkCount = 0;

            _attributes = new List<AttributeRecord>();
            _haveIndex = true;
        }

        public uint MasterFileTableIndex
        {
            get { return _index; }
        }

        public ushort SequenceNumber
        {
            get { return _sequenceNumber; }
        }

        public ushort HardLinkCount
        {
            get { return _hardLinkCount; }
            set { _hardLinkCount = value; }
        }

        public uint AllocatedSize
        {
            get { return _recordAllocatedSize; }
        }

        public uint RealSize
        {
            get { return _recordRealSize; }
        }

        public FileReference BaseFile
        {
            get { return _baseFile; }
        }

        public FileRecordFlags Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public ICollection<AttributeRecord> Attributes
        {
            get { return new ReadOnlyCollection<AttributeRecord>(_attributes); }
        }

        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        /// <param name="name">The name of the new attribute</param>
        /// <param name="indexed">Whether the attribute is marked as indexed</param>
        internal ushort CreateAttribute(AttributeType type, string name, bool indexed)
        {
            ushort id = _nextAttributeId++;
            _attributes.Add(
                new ResidentAttributeRecord(
                    type,
                    name,
                    id,
                    indexed)
                );
            _attributes.Sort();
            return id;
        }

        /// <summary>
        /// Removes an attribute by it's id.
        /// </summary>
        /// <param name="id">The attribute's id</param>
        internal void RemoveAttribute(ushort id)
        {
            for (int i = 0; i < _attributes.Count; ++i)
            {
                if (_attributes[i].AttributeId == id)
                {
                    _attributes.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Gets an attribute by it's id.
        /// </summary>
        /// <param name="id">The attribute's id</param>
        /// <returns>The attribute, or <c>null</c></returns>
        public AttributeRecord GetAttribute(ushort id)
        {
            foreach (AttributeRecord attrRec in _attributes)
            {
                if (attrRec.AttributeId == id)
                {
                    return attrRec;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public AttributeRecord GetAttribute(AttributeType type)
        {
            return GetAttribute(type, null);
        }

        /// <summary>
        /// Gets an named attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <param name="name">The name of the attribute</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public AttributeRecord GetAttribute(AttributeType type, string name)
        {
            foreach (AttributeRecord attrRec in _attributes)
            {
                if (attrRec.AttributeType == type && attrRec.Name == name)
                {
                    return attrRec;
                }
            }

            return null;
        }

        public void SetAttribute(AttributeRecord record)
        {
            for(int i = 0; i < _attributes.Count; ++i)
            {
                if (_attributes[i].AttributeType == record.AttributeType && _attributes[i].Name == record.Name)
                {
                    if (record.AttributeId != _attributes[i].AttributeId)
                    {
                        throw new InvalidOperationException("Attempt to set attribute where (type,name,id) don't match");
                    }

                    _attributes[i] = record;
                    return;
                }
            }

            throw new InvalidOperationException("Attempt to create attribute by setting it");
        }

        internal void Reset()
        {
            _attributes.Clear();
            _flags = FileRecordFlags.None;
            _hardLinkCount = 0;
            _nextAttributeId = 0;
            _recordRealSize = 0;
        }

        protected override void Read(byte[] buffer, int offset)
        {
            _logFileSequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            _sequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 0x10);
            _hardLinkCount = Utilities.ToUInt16LittleEndian(buffer, offset + 0x12);
            _firstAttributeOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x14);
            _flags = (FileRecordFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x16);
            _recordRealSize = Utilities.ToUInt32LittleEndian(buffer, offset + 0x18);
            _recordAllocatedSize = Utilities.ToUInt32LittleEndian(buffer, offset + 0x1C);
            _baseFile = new FileReference(Utilities.ToUInt64LittleEndian(buffer, offset + 0x20));
            _nextAttributeId = Utilities.ToUInt16LittleEndian(buffer, offset + 0x28);

            if (UpdateSequenceOffset >= 0x30)
            {
                _index = Utilities.ToUInt32LittleEndian(buffer, offset + 0x2C);
                _haveIndex = true;
            }

            _attributes = new List<AttributeRecord>();
            int focus = _firstAttributeOffset;
            while (true)
            {
                int length;
                AttributeRecord attr = AttributeRecord.FromBytes(buffer, focus, out length);
                if (attr == null)
                {
                    break;
                }

                _attributes.Add(attr);
                focus += (int)length;
            }
        }

        protected override ushort Write(byte[] buffer, int offset)
        {
            ushort headerEnd = (ushort)(_haveIndex ? 0x30 : 0x2A);

            _firstAttributeOffset = (ushort)Utilities.RoundUp(headerEnd + UpdateSequenceSize, 0x08);
            _recordRealSize = (uint)CalcSize();

            Utilities.WriteBytesLittleEndian(_logFileSequenceNumber, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian(_sequenceNumber, buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian(_hardLinkCount, buffer, offset + 0x12);
            Utilities.WriteBytesLittleEndian(_firstAttributeOffset, buffer, offset + 0x14);
            Utilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x16);
            Utilities.WriteBytesLittleEndian(_recordRealSize, buffer, offset + 0x18);
            Utilities.WriteBytesLittleEndian(_recordAllocatedSize, buffer, offset + 0x1C);
            Utilities.WriteBytesLittleEndian(_baseFile.Value, buffer, offset + 0x20);
            Utilities.WriteBytesLittleEndian(_nextAttributeId, buffer, offset + 0x28);

            if (_haveIndex)
            {
                Utilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x2A); // Alignment field
                Utilities.WriteBytesLittleEndian(_index, buffer, offset + 0x2C);
            }

            int pos = _firstAttributeOffset;
            foreach (var attr in _attributes)
            {
                pos += attr.Write(buffer, offset + pos);
            }
            Utilities.WriteBytesLittleEndian(uint.MaxValue, buffer, offset + pos);

            return headerEnd;
        }

        protected override int CalcSize()
        {
            int firstAttrPos = (ushort)Utilities.RoundUp((_haveIndex ? 0x30 : 0x2A) + UpdateSequenceSize, 8);

            int size = firstAttrPos;
            foreach (var attr in _attributes)
            {
                size += attr.Size;
            }

            return Utilities.RoundUp(size + 4, 8); // 0xFFFFFFFF terminator on attributes
        }

        internal long GetAttributeOffset(ushort id)
        {
            int firstAttrPos = (ushort)Utilities.RoundUp((_haveIndex ? 0x30 : 0x2A) + UpdateSequenceSize, 8);

            int offset = firstAttrPos;
            foreach (var attr in _attributes)
            {
                if (attr.AttributeId == id)
                {
                    return offset;
                }
                offset += attr.Size;
            }

            return -1;
        }

        public override string ToString()
        {
            foreach (AttributeRecord attr in _attributes)
            {
                if (attr.AttributeType == AttributeType.FileName)
                {
                    StructuredNtfsAttribute<FileNameRecord> fnAttr = new StructuredNtfsAttribute<FileNameRecord>(null, new FileReference(0), attr);
                    return fnAttr.Content.FileName;
                }
            }

            return "No Name";
        }

        internal void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE RECORD (" + ToString() + ")");
            writer.WriteLine(indent + "              Magic: " + Magic);
            writer.WriteLine(indent + "  Update Seq Offset: " + UpdateSequenceOffset);
            writer.WriteLine(indent + "   Update Seq Count: " + UpdateSequenceCount);
            writer.WriteLine(indent + "  Update Seq Number: " + UpdateSequenceNumber);
            writer.WriteLine(indent + "   Log File Seq Num: " + _logFileSequenceNumber);
            writer.WriteLine(indent + "    Sequence Number: " + _sequenceNumber);
            writer.WriteLine(indent + "    Hard Link Count: " + _hardLinkCount);
            writer.WriteLine(indent + "              Flags: " + _flags);
            writer.WriteLine(indent + "   Record Real Size: " + _recordRealSize);
            writer.WriteLine(indent + "  Record Alloc Size: " + _recordAllocatedSize);
            writer.WriteLine(indent + "          Base File: " + _baseFile);
            writer.WriteLine(indent + "  Next Attribute Id: " + _nextAttributeId);
            writer.WriteLine(indent + "    Attribute Count: " + _attributes.Count);
            writer.WriteLine(indent + "   Index (Self Ref): " + _index);
        }
    }
}

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
    internal class FileRecord : FixupRecordBase
    {
        private ulong _logFileSequenceNumber;
        private ushort _sequenceNumber;
        private ushort _hardLinkCount;
        private ushort _firstAttributeOffset;
        private ushort _flags;
        private uint _recordRealSize;
        private uint _recordAllocatedSize;
        private FileReference _baseFile;
        private ushort _nextAttributeId;
        private uint _index; // Self-reference (on XP+)
        private List<AttributeRecord> _attributes;

        private bool _haveIndex;

        public FileRecord(int sectorSize)
            : base(sectorSize)
        {
        }

        public uint MasterFileTableIndex
        {
            get { return _index; }
            set
            {
                _index = value;
                _haveIndex = true;
            }
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

        public uint MaxSize
        {
            get { return _recordAllocatedSize; }
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
                new ResidentFileAttributeRecord(
                    type,
                    name,
                    id,
                    indexed)
                );
            _attributes.Sort();
            return id;
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
                    _attributes[i] = record;
                    return;
                }
            }

            throw new NotImplementedException("Adding new attributes");
        }

        protected override void Read(byte[] buffer, int offset)
        {
            _logFileSequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            _sequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 0x10);
            _hardLinkCount = Utilities.ToUInt16LittleEndian(buffer, offset + 0x12);
            _firstAttributeOffset = Utilities.ToUInt16LittleEndian(buffer, offset + 0x14);
            _flags = Utilities.ToUInt16LittleEndian(buffer, offset + 0x16);
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

        protected override ushort Write(byte[] buffer, int offset, ushort updateSeqSize)
        {
            ushort headerEnd = (ushort)(_haveIndex ? 0x30 : 0x2A);

            _firstAttributeOffset = (ushort)Utilities.RoundUp(headerEnd + updateSeqSize, 0x08);
            _recordRealSize = (uint)CalcSize(updateSeqSize);

            Utilities.WriteBytesLittleEndian(_logFileSequenceNumber, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian(_sequenceNumber, buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian(_hardLinkCount, buffer, offset + 0x12);
            Utilities.WriteBytesLittleEndian(_firstAttributeOffset, buffer, offset + 0x14);
            Utilities.WriteBytesLittleEndian(_flags, buffer, offset + 0x16);
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

        protected override int CalcSize(int updateSeqSize)
        {
            int headerEnd = _haveIndex ? 0x30 : 0x2A;

            int size = headerEnd + updateSeqSize;
            foreach (var attr in _attributes)
            {
                size += attr.Size;
            }

            return Utilities.RoundUp(size + 4, 8); // 0xFFFFFFFF terminator on attributes
        }

        public override string ToString()
        {
            foreach (AttributeRecord attr in _attributes)
            {
                if (attr.AttributeType == AttributeType.FileName)
                {
                    StructuredNtfsAttribute<FileNameRecord> fnAttr = new StructuredNtfsAttribute<FileNameRecord>(null, attr);
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
            writer.WriteLine(indent + "    Update Seq Size: " + UpdateSequenceSize);
            writer.WriteLine(indent + "  Update Seq Number: " + UpdateSequenceNumber);
            writer.WriteLine(indent + "   Log File Seq Num: " + _logFileSequenceNumber);
            writer.WriteLine(indent + "    Sequence Number: " + _sequenceNumber);
            writer.WriteLine(indent + "    Hard Link Count: " + _hardLinkCount);
            writer.WriteLine(indent + "              Flags: " + _flags);
            writer.WriteLine(indent + "   Record Real Size: " + _recordRealSize);
            writer.WriteLine(indent + "  Record Alloc Size: " + _recordAllocatedSize);
            writer.WriteLine(indent + "          Base File: " + _baseFile);
            writer.WriteLine(indent + "  Next Attribute Id: " + _nextAttributeId);
            writer.WriteLine(indent + "   Index (Self Ref): " + _index);

            foreach (AttributeRecord attr in _attributes)
            {
                attr.Dump(writer, indent + "     ");
            }
        }

    }
}

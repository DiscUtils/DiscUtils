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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using DiscUtils.Ntfs.Attributes;

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
        private List<FileAttributeRecord> _attributes;

        public FileRecord(int sectorSize)
            : base(sectorSize)
        {
        }

        public uint MasterFileTableIndex
        {
            get { return _index; }
        }

        public ushort SequenceNumber
        {
            get { return _sequenceNumber; }
        }

        public ICollection<FileAttributeRecord> Attributes
        {
            get { return new ReadOnlyCollection<FileAttributeRecord>(_attributes); }
        }

        /// <summary>
        /// Gets an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public FileAttributeRecord GetAttribute(AttributeType type)
        {
            return GetAttribute(type, null);
        }

        /// <summary>
        /// Gets an named attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <param name="name">The name of the attribute</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public FileAttributeRecord GetAttribute(AttributeType type, string name)
        {
            foreach (FileAttributeRecord attrRec in _attributes)
            {
                if (attrRec.AttributeType == type && attrRec.Name == name)
                {
                    return attrRec;
                }
            }

            return null;
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
            }

            _attributes = new List<FileAttributeRecord>();
            int focus = _firstAttributeOffset;
            while (true)
            {
                FileAttributeRecord attr = FileAttributeRecord.FromBytes(buffer, focus);
                if (attr == null)
                {
                    break;
                }

                _attributes.Add(attr);
                focus += (int)attr.Length;
            }
        }

        public override string ToString()
        {
            foreach (FileAttributeRecord attr in _attributes)
            {
                if (attr.AttributeType == AttributeType.FileName)
                {
                    FileNameAttribute fnAttr = new FileNameAttribute((ResidentFileAttributeRecord)attr);
                    return fnAttr.ToString();
                }
            }

            return "No Name";
        }

        internal void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE RECORD (" + ToString() + ")");
            writer.WriteLine(indent + "   Log File Seq Num: " + _logFileSequenceNumber);
            writer.WriteLine(indent + "    Sequence Number: " + _sequenceNumber);
            writer.WriteLine(indent + "    Hard Link Count: " + _hardLinkCount);
            writer.WriteLine(indent + "              Flags: " + _flags);
            writer.WriteLine(indent + "   Record Real Size: " + _recordRealSize);
            writer.WriteLine(indent + "  Record Alloc Size: " + _recordAllocatedSize);
            writer.WriteLine(indent + "          Base File: " + _baseFile);
            writer.WriteLine(indent + "  Next Attribute Id: " + _nextAttributeId);
            writer.WriteLine(indent + "   Index (Self Ref): " + _index);

            foreach (FileAttributeRecord attr in _attributes)
            {
                attr.Dump(writer, indent + "     ");
            }
        }
    }
}

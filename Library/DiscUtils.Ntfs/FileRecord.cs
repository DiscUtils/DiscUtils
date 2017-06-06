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

using System.Collections.Generic;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class FileRecord : FixupRecordBase
    {
        private ushort _firstAttributeOffset;

        private bool _haveIndex;
        private uint _index; // Self-reference (on XP+)

        public FileRecord(int sectorSize)
            : base("FILE", sectorSize) {}

        public FileRecord(int sectorSize, int recordLength, uint index)
            : base("FILE", sectorSize, recordLength)
        {
            ReInitialize(sectorSize, recordLength, index);
        }

        public uint AllocatedSize { get; private set; }

        public List<AttributeRecord> Attributes { get; private set; }

        public FileRecordReference BaseFile { get; set; }

        public AttributeRecord FirstAttribute
        {
            get { return Attributes.Count > 0 ? Attributes[0] : null; }
        }

        public FileRecordFlags Flags { get; set; }

        public ushort HardLinkCount { get; set; }

        public bool IsMftRecord
        {
            get
            {
                return MasterFileTableIndex == MasterFileTable.MftIndex ||
                       (BaseFile.MftIndex == MasterFileTable.MftIndex && BaseFile.SequenceNumber != 0);
            }
        }

        public uint LoadedIndex { get; set; }

        public ulong LogFileSequenceNumber { get; private set; }

        public uint MasterFileTableIndex
        {
            get { return _haveIndex ? _index : LoadedIndex; }
        }

        public ushort NextAttributeId { get; private set; }

        public uint RealSize { get; private set; }

        public FileRecordReference Reference
        {
            get { return new FileRecordReference(MasterFileTableIndex, SequenceNumber); }
        }

        public ushort SequenceNumber { get; set; }

        public static FileAttributeFlags ConvertFlags(FileRecordFlags source)
        {
            FileAttributeFlags result = FileAttributeFlags.None;

            if ((source & FileRecordFlags.IsDirectory) != 0)
            {
                result |= FileAttributeFlags.Directory;
            }

            if ((source & FileRecordFlags.HasViewIndex) != 0)
            {
                result |= FileAttributeFlags.IndexView;
            }

            if ((source & FileRecordFlags.IsMetaFile) != 0)
            {
                result |= FileAttributeFlags.Hidden | FileAttributeFlags.System;
            }

            return result;
        }

        public void ReInitialize(int sectorSize, int recordLength, uint index)
        {
            Initialize("FILE", sectorSize, recordLength);
            SequenceNumber++;
            Flags = FileRecordFlags.None;
            AllocatedSize = (uint)recordLength;
            NextAttributeId = 0;
            _index = index;
            HardLinkCount = 0;
            BaseFile = new FileRecordReference(0);

            Attributes = new List<AttributeRecord>();
            _haveIndex = true;
        }

        /// <summary>
        /// Gets an attribute by it's id.
        /// </summary>
        /// <param name="id">The attribute's id.</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public AttributeRecord GetAttribute(ushort id)
        {
            foreach (AttributeRecord attrRec in Attributes)
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
        /// <param name="type">The attribute type.</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public AttributeRecord GetAttribute(AttributeType type)
        {
            return GetAttribute(type, null);
        }

        /// <summary>
        /// Gets an named attribute.
        /// </summary>
        /// <param name="type">The attribute type.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public AttributeRecord GetAttribute(AttributeType type, string name)
        {
            foreach (AttributeRecord attrRec in Attributes)
            {
                if (attrRec.AttributeType == type && attrRec.Name == name)
                {
                    return attrRec;
                }
            }

            return null;
        }

        public override string ToString()
        {
            foreach (AttributeRecord attr in Attributes)
            {
                if (attr.AttributeType == AttributeType.FileName)
                {
                    StructuredNtfsAttribute<FileNameRecord> fnAttr =
                        (StructuredNtfsAttribute<FileNameRecord>)
                        NtfsAttribute.FromRecord(null, new FileRecordReference(0), attr);
                    return fnAttr.Content.FileName;
                }
            }

            return "No Name";
        }

        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute.</param>
        /// <param name="name">The name of the new attribute.</param>
        /// <param name="indexed">Whether the attribute is marked as indexed.</param>
        /// <param name="flags">Flags for the new attribute.</param>
        /// <returns>The id of the new attribute.</returns>
        public ushort CreateAttribute(AttributeType type, string name, bool indexed, AttributeFlags flags)
        {
            ushort id = NextAttributeId++;
            Attributes.Add(
                new ResidentAttributeRecord(
                    type,
                    name,
                    id,
                    indexed,
                    flags));
            Attributes.Sort();
            return id;
        }

        /// <summary>
        /// Creates a new non-resident attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute.</param>
        /// <param name="name">The name of the new attribute.</param>
        /// <param name="flags">Flags for the new attribute.</param>
        /// <returns>The id of the new attribute.</returns>
        public ushort CreateNonResidentAttribute(AttributeType type, string name, AttributeFlags flags)
        {
            ushort id = NextAttributeId++;
            Attributes.Add(
                new NonResidentAttributeRecord(
                    type,
                    name,
                    id,
                    flags,
                    0,
                    new List<DataRun>()));
            Attributes.Sort();
            return id;
        }

        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute.</param>
        /// <param name="name">The name of the new attribute.</param>
        /// <param name="flags">Flags for the new attribute.</param>
        /// <param name="firstCluster">The first cluster to assign to the attribute.</param>
        /// <param name="numClusters">The number of sequential clusters to assign to the attribute.</param>
        /// <param name="bytesPerCluster">The number of bytes in each cluster.</param>
        /// <returns>The id of the new attribute.</returns>
        public ushort CreateNonResidentAttribute(AttributeType type, string name, AttributeFlags flags,
                                                 long firstCluster, ulong numClusters, uint bytesPerCluster)
        {
            ushort id = NextAttributeId++;
            Attributes.Add(
                new NonResidentAttributeRecord(
                    type,
                    name,
                    id,
                    flags,
                    firstCluster,
                    numClusters,
                    bytesPerCluster));
            Attributes.Sort();
            return id;
        }

        /// <summary>
        /// Adds an existing attribute.
        /// </summary>
        /// <param name="attrRec">The attribute to add.</param>
        /// <returns>The new Id of the attribute.</returns>
        /// <remarks>This method is used to move an attribute between different MFT records.</remarks>
        public ushort AddAttribute(AttributeRecord attrRec)
        {
            attrRec.AttributeId = NextAttributeId++;
            Attributes.Add(attrRec);
            Attributes.Sort();
            return attrRec.AttributeId;
        }

        /// <summary>
        /// Removes an attribute by it's id.
        /// </summary>
        /// <param name="id">The attribute's id.</param>
        public void RemoveAttribute(ushort id)
        {
            for (int i = 0; i < Attributes.Count; ++i)
            {
                if (Attributes[i].AttributeId == id)
                {
                    Attributes.RemoveAt(i);
                    break;
                }
            }
        }

        public void Reset()
        {
            Attributes.Clear();
            Flags = FileRecordFlags.None;
            HardLinkCount = 0;
            NextAttributeId = 0;
            RealSize = 0;
        }

        internal long GetAttributeOffset(ushort id)
        {
            int firstAttrPos = (ushort)MathUtilities.RoundUp((_haveIndex ? 0x30 : 0x2A) + UpdateSequenceSize, 8);

            int offset = firstAttrPos;
            foreach (AttributeRecord attr in Attributes)
            {
                if (attr.AttributeId == id)
                {
                    return offset;
                }

                offset += attr.Size;
            }

            return -1;
        }

        internal void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE RECORD (" + ToString() + ")");
            writer.WriteLine(indent + "              Magic: " + Magic);
            writer.WriteLine(indent + "  Update Seq Offset: " + UpdateSequenceOffset);
            writer.WriteLine(indent + "   Update Seq Count: " + UpdateSequenceCount);
            writer.WriteLine(indent + "  Update Seq Number: " + UpdateSequenceNumber);
            writer.WriteLine(indent + "   Log File Seq Num: " + LogFileSequenceNumber);
            writer.WriteLine(indent + "    Sequence Number: " + SequenceNumber);
            writer.WriteLine(indent + "    Hard Link Count: " + HardLinkCount);
            writer.WriteLine(indent + "              Flags: " + Flags);
            writer.WriteLine(indent + "   Record Real Size: " + RealSize);
            writer.WriteLine(indent + "  Record Alloc Size: " + AllocatedSize);
            writer.WriteLine(indent + "          Base File: " + BaseFile);
            writer.WriteLine(indent + "  Next Attribute Id: " + NextAttributeId);
            writer.WriteLine(indent + "    Attribute Count: " + Attributes.Count);
            writer.WriteLine(indent + "   Index (Self Ref): " + _index);
        }

        protected override void Read(byte[] buffer, int offset)
        {
            LogFileSequenceNumber = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            SequenceNumber = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x10);
            HardLinkCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x12);
            _firstAttributeOffset = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x14);
            Flags = (FileRecordFlags)EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x16);
            RealSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x18);
            AllocatedSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x1C);
            BaseFile = new FileRecordReference(EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x20));
            NextAttributeId = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x28);

            if (UpdateSequenceOffset >= 0x30)
            {
                _index = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x2C);
                _haveIndex = true;
            }

            Attributes = new List<AttributeRecord>();
            int focus = _firstAttributeOffset;
            while (true)
            {
                int length;
                AttributeRecord attr = AttributeRecord.FromBytes(buffer, focus, out length);
                if (attr == null)
                {
                    break;
                }

                Attributes.Add(attr);
                focus += length;
            }
        }

        protected override ushort Write(byte[] buffer, int offset)
        {
            ushort headerEnd = (ushort)(_haveIndex ? 0x30 : 0x2A);

            _firstAttributeOffset = (ushort)MathUtilities.RoundUp(headerEnd + UpdateSequenceSize, 0x08);
            RealSize = (uint)CalcSize();

            EndianUtilities.WriteBytesLittleEndian(LogFileSequenceNumber, buffer, offset + 0x08);
            EndianUtilities.WriteBytesLittleEndian(SequenceNumber, buffer, offset + 0x10);
            EndianUtilities.WriteBytesLittleEndian(HardLinkCount, buffer, offset + 0x12);
            EndianUtilities.WriteBytesLittleEndian(_firstAttributeOffset, buffer, offset + 0x14);
            EndianUtilities.WriteBytesLittleEndian((ushort)Flags, buffer, offset + 0x16);
            EndianUtilities.WriteBytesLittleEndian(RealSize, buffer, offset + 0x18);
            EndianUtilities.WriteBytesLittleEndian(AllocatedSize, buffer, offset + 0x1C);
            EndianUtilities.WriteBytesLittleEndian(BaseFile.Value, buffer, offset + 0x20);
            EndianUtilities.WriteBytesLittleEndian(NextAttributeId, buffer, offset + 0x28);

            if (_haveIndex)
            {
                EndianUtilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x2A); // Alignment field
                EndianUtilities.WriteBytesLittleEndian(_index, buffer, offset + 0x2C);
            }

            int pos = _firstAttributeOffset;
            foreach (AttributeRecord attr in Attributes)
            {
                pos += attr.Write(buffer, offset + pos);
            }

            EndianUtilities.WriteBytesLittleEndian(uint.MaxValue, buffer, offset + pos);

            return headerEnd;
        }

        protected override int CalcSize()
        {
            int firstAttrPos = (ushort)MathUtilities.RoundUp((_haveIndex ? 0x30 : 0x2A) + UpdateSequenceSize, 8);

            int size = firstAttrPos;
            foreach (AttributeRecord attr in Attributes)
            {
                size += attr.Size;
            }

            return MathUtilities.RoundUp(size + 4, 8); // 0xFFFFFFFF terminator on attributes
        }
    }
}
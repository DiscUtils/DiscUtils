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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiscUtils.Ntfs
{
    internal class AttributeList : IByteArraySerializable, IDiagnosticTraceable, ICollection<AttributeListRecord>
    {
        private List<AttributeListRecord> _records;

        public AttributeList()
        {
            _records = new List<AttributeListRecord>();
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            _records.Clear();

            int pos = 0;
            while (pos < buffer.Length)
            {
                AttributeListRecord r = new AttributeListRecord();
                pos += r.ReadFrom(buffer, offset + pos);
                _records.Add(r);
            }

            return pos;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            int pos = offset;
            foreach (var record in _records)
            {
                record.WriteTo(buffer, offset + pos);
                pos += record.Size;
            }
        }

        public int Size
        {
            get
            {
                int total = 0;
                foreach (var record in _records)
                {
                    total += record.Size;
                }
                return total;
            }
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE LIST RECORDS");
            foreach (AttributeListRecord r in _records)
            {
                r.Dump(writer, indent + "  ");
            }
        }


        #region ICollection<AttributeListRecord> Members

        public void Add(AttributeListRecord item)
        {
            _records.Add(item);
            _records.Sort();
        }

        public void Clear()
        {
            _records.Clear();
        }

        public bool Contains(AttributeListRecord item)
        {
            return _records.Contains(item);
        }

        public void CopyTo(AttributeListRecord[] array, int arrayIndex)
        {
            _records.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _records.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(AttributeListRecord item)
        {
            return _records.Remove(item);
        }

        #endregion

        #region IEnumerable<AttributeListRecord> Members

        public IEnumerator<AttributeListRecord> GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        #endregion
    }

    internal class AttributeListRecord : IDiagnosticTraceable, IByteArraySerializable, IComparable<AttributeListRecord>
    {
        public AttributeType Type;
        public ushort RecordLength;
        public byte NameLength;
        public byte NameOffset;
        public string Name;
        public ulong StartVcn;
        public FileRecordReference BaseFileReference;
        public ushort AttributeId;

        #region IByteArraySerializable Members

        public int ReadFrom(byte[] data, int offset)
        {
            Type = (AttributeType)Utilities.ToUInt32LittleEndian(data, offset + 0x00);
            RecordLength = Utilities.ToUInt16LittleEndian(data, offset + 0x04);
            NameLength = data[offset + 0x06];
            NameOffset = data[offset + 0x07];
            StartVcn = Utilities.ToUInt64LittleEndian(data, offset + 0x08);
            BaseFileReference = new FileRecordReference(Utilities.ToUInt64LittleEndian(data, offset + 0x10));
            AttributeId = Utilities.ToUInt16LittleEndian(data, offset + 0x18);

            if (NameLength > 0)
            {
                Name = Encoding.Unicode.GetString(data, offset + NameOffset, NameLength * 2);
            }
            else
            {
                Name = null;
            }

            if (RecordLength < 0x18)
            {
                throw new InvalidDataException("Malformed AttributeList record");
            }

            return RecordLength;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            NameOffset = 0x20;
            if (string.IsNullOrEmpty(Name))
            {
                NameLength = 0;
            }
            else
            {
                NameLength = (byte)Encoding.Unicode.GetBytes(Name, 0, Name.Length, buffer, offset + NameOffset);
            }
            RecordLength = (ushort)((ushort)NameOffset + (ushort)NameLength);

            Utilities.WriteBytesLittleEndian((uint)Type, buffer, offset);
            Utilities.WriteBytesLittleEndian(RecordLength, buffer, offset + 0x04);
            buffer[offset + 0x06] = NameLength;
            buffer[offset + 0x07] = NameOffset;
            Utilities.WriteBytesLittleEndian(StartVcn, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian(BaseFileReference.Value, buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian(AttributeId, buffer, offset + 0x18);
        }

        public int Size
        {
            get
            {
                return 0x20 + (string.IsNullOrEmpty(Name) ? 0 : Encoding.Unicode.GetByteCount(Name));
            }
        }

        #endregion

        #region IComparable<AttributeListRecord> Members

        public int CompareTo(AttributeListRecord other)
        {
            int val = ((int)Type) - (int)other.Type;
            if (val != 0)
            {
                return val;
            }

            val = string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            if (val != 0)
            {
                return val;
            }

            return ((int)StartVcn) - (int)other.StartVcn;
        }

        #endregion

        public static AttributeListRecord FromAttribute(AttributeRecord attr, FileRecordReference mftRecord)
        {
            AttributeListRecord newRecord = new AttributeListRecord()
            {
                Type = attr.AttributeType,
                Name = attr.Name,
                StartVcn = 0,
                BaseFileReference = mftRecord,
                AttributeId = attr.AttributeId
            };

            if (attr.IsNonResident)
            {
                newRecord.StartVcn = (ulong)((NonResidentAttributeRecord)attr).StartVcn;
            }

            return newRecord;
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE LIST RECORD");
            writer.WriteLine(indent + "                 Type: " + Type);
            writer.WriteLine(indent + "        Record Length: " + RecordLength);
            writer.WriteLine(indent + "                 Name: " + Name);
            writer.WriteLine(indent + "            Start VCN: " + StartVcn);
            writer.WriteLine(indent + "  Base File Reference: " + BaseFileReference);
            writer.WriteLine(indent + "         Attribute ID: " + AttributeId);
        }

    }
}

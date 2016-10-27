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

namespace DiscUtils.Ntfs
{
    using System;
    using System.IO;
    using System.Text;

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
            set { _flags = value; }
        }

        public abstract int Size { get; }

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

        public static int CompareStartVcns(AttributeRecord x, AttributeRecord y)
        {
            if (x.StartVcn < y.StartVcn)
            {
                return -1;
            }
            else if (x.StartVcn == y.StartVcn)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        public abstract Range<long, long>[] GetClusters();

        public abstract IBuffer GetReadOnlyDataBuffer(INtfsContext context);

        public int CompareTo(AttributeRecord other)
        {
            int val = ((int)_type) - (int)other._type;
            if (val != 0)
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

        public abstract int Write(byte[] buffer, int offset);

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE RECORD");
            writer.WriteLine(indent + "            Type: " + _type);
            writer.WriteLine(indent + "    Non-Resident: " + _nonResidentFlag);
            writer.WriteLine(indent + "            Name: " + _name);
            writer.WriteLine(indent + "           Flags: " + _flags);
            writer.WriteLine(indent + "     AttributeId: " + _attributeId);
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
    }
}

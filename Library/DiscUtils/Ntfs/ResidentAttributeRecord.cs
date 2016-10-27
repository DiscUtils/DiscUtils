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
            _nonResidentFlag = 0;
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
        /// The amount of initialized data in the attribute (in bytes).
        /// </summary>
        public override long InitializedDataLength
        {
            get { return (long)DataLength; }
            set { throw new NotSupportedException(); }
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

        public IBuffer DataBuffer
        {
            get { return _memoryBuffer; }
        }

        public override IBuffer GetReadOnlyDataBuffer(INtfsContext context)
        {
            return _memoryBuffer;
        }

        public override Range<long, long>[] GetClusters()
        {
            return new Range<long, long>[0];
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

        public override void Dump(TextWriter writer, string indent)
        {
            base.Dump(writer, indent);
            writer.WriteLine(indent + "     Data Length: " + DataLength);
            writer.WriteLine(indent + "         Indexed: " + _indexedFlag);
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
    }
}

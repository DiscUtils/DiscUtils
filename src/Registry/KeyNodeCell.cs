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

namespace DiscUtils.Registry
{
    internal sealed class KeyNodeCell : Cell
    {
        public RegistryKeyFlags Flags;
        public DateTime Timestamp;
        public int ParentIndex;
        public int NumSubKeys;
        public int SubKeysIndex;
        public int NumValues;
        public int ValueListIndex;
        public int SecurityIndex;
        public int ClassNameIndex;

        /// <summary>
        /// Number of bytes to represent largest subkey name in Unicode - no null terminator
        /// </summary>
        public int MaxSubKeyNameBytes;

        /// <summary>
        /// Number of bytes to represent largest value name in Unicode - no null terminator
        /// </summary>
        public int MaxValNameBytes;

        /// <summary>
        /// Number of bytes to represent largest value content (strings in Unicode, with null terminator - if stored)
        /// </summary>
        public int MaxValDataBytes;

        public int IndexInParent;
        public int ClassNameLength;
        public string Name;

        public KeyNodeCell(string name, int parentCellIndex)
            : this(-1)
        {
            Flags = RegistryKeyFlags.Normal;
            Timestamp = DateTime.UtcNow;
            ParentIndex = parentCellIndex;
            SubKeysIndex = -1;
            ValueListIndex = -1;
            SecurityIndex = -1;
            ClassNameIndex = -1;
            Name = name;
        }

        public KeyNodeCell(int index)
            : base(index)
        {
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Flags = (RegistryKeyFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            Timestamp = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x04));
            ParentIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x10);
            NumSubKeys = Utilities.ToInt32LittleEndian(buffer, offset + 0x14);
            SubKeysIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x1C);
            NumValues = Utilities.ToInt32LittleEndian(buffer, offset + 0x24);
            ValueListIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x28);
            SecurityIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x2C);
            ClassNameIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x30);
            MaxSubKeyNameBytes = Utilities.ToInt32LittleEndian(buffer, offset + 0x34);
            MaxValNameBytes = Utilities.ToInt32LittleEndian(buffer, offset + 0x3C);
            MaxValDataBytes = Utilities.ToInt32LittleEndian(buffer, offset + 0x40);
            IndexInParent = Utilities.ToInt32LittleEndian(buffer, offset + 0x44);
            int nameLength = Utilities.ToInt16LittleEndian(buffer, offset + 0x48);
            ClassNameLength = Utilities.ToInt16LittleEndian(buffer, offset + 0x4A);
            Name = Utilities.BytesToString(buffer, offset + 0x4C, nameLength);

            return 0x4C + nameLength;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            Utilities.StringToBytes("nk", buffer, offset, 2);
            Utilities.WriteBytesLittleEndian((ushort)Flags, buffer, offset + 0x02);
            Utilities.WriteBytesLittleEndian(Timestamp.ToFileTimeUtc(), buffer, offset + 0x04);
            Utilities.WriteBytesLittleEndian(ParentIndex, buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian(NumSubKeys, buffer, offset + 0x14);
            Utilities.WriteBytesLittleEndian(SubKeysIndex, buffer, offset + 0x1C);
            Utilities.WriteBytesLittleEndian(NumValues, buffer, offset + 0x24);
            Utilities.WriteBytesLittleEndian(ValueListIndex, buffer, offset + 0x28);
            Utilities.WriteBytesLittleEndian(SecurityIndex, buffer, offset + 0x2C);
            Utilities.WriteBytesLittleEndian(ClassNameIndex, buffer, offset + 0x30);
            Utilities.WriteBytesLittleEndian(IndexInParent, buffer, offset + 0x44);
            Utilities.WriteBytesLittleEndian((ushort)Name.Length, buffer, offset + 0x48);
            Utilities.WriteBytesLittleEndian(ClassNameLength, buffer, offset + 0x4A);
            Utilities.StringToBytes(Name, buffer, offset + 0x4C, Name.Length);
        }

        public override int Size
        {
            get { return 0x4C + Name.Length; }
        }

        public override string ToString()
        {
            return "Key:" + Name + "[" + Flags + "] <" + Timestamp + ">";
        }
    }

}

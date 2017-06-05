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

using System;
using DiscUtils.Streams;

namespace DiscUtils.Registry
{
    internal sealed class KeyNodeCell : Cell
    {
        public int ClassNameIndex;
        public int ClassNameLength;
        public RegistryKeyFlags Flags;

        public int IndexInParent;

        /// <summary>
        /// Number of bytes to represent largest subkey name in Unicode - no null terminator.
        /// </summary>
        public int MaxSubKeyNameBytes;

        /// <summary>
        /// Number of bytes to represent largest value content (strings in Unicode, with null terminator - if stored).
        /// </summary>
        public int MaxValDataBytes;

        /// <summary>
        /// Number of bytes to represent largest value name in Unicode - no null terminator.
        /// </summary>
        public int MaxValNameBytes;

        public string Name;
        public int NumSubKeys;
        public int NumValues;
        public int ParentIndex;
        public int SecurityIndex;
        public int SubKeysIndex;
        public DateTime Timestamp;
        public int ValueListIndex;

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
            : base(index) {}

        public override int Size
        {
            get { return 0x4C + Name.Length; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Flags = (RegistryKeyFlags)EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            Timestamp = DateTime.FromFileTimeUtc(EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x04));
            ParentIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x10);
            NumSubKeys = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x14);
            SubKeysIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x1C);
            NumValues = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x24);
            ValueListIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x28);
            SecurityIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x2C);
            ClassNameIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x30);
            MaxSubKeyNameBytes = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x34);
            MaxValNameBytes = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x3C);
            MaxValDataBytes = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x40);
            IndexInParent = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x44);
            int nameLength = EndianUtilities.ToInt16LittleEndian(buffer, offset + 0x48);
            ClassNameLength = EndianUtilities.ToInt16LittleEndian(buffer, offset + 0x4A);
            Name = EndianUtilities.BytesToString(buffer, offset + 0x4C, nameLength);

            return 0x4C + nameLength;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.StringToBytes("nk", buffer, offset, 2);
            EndianUtilities.WriteBytesLittleEndian((ushort)Flags, buffer, offset + 0x02);
            EndianUtilities.WriteBytesLittleEndian(Timestamp.ToFileTimeUtc(), buffer, offset + 0x04);
            EndianUtilities.WriteBytesLittleEndian(ParentIndex, buffer, offset + 0x10);
            EndianUtilities.WriteBytesLittleEndian(NumSubKeys, buffer, offset + 0x14);
            EndianUtilities.WriteBytesLittleEndian(SubKeysIndex, buffer, offset + 0x1C);
            EndianUtilities.WriteBytesLittleEndian(NumValues, buffer, offset + 0x24);
            EndianUtilities.WriteBytesLittleEndian(ValueListIndex, buffer, offset + 0x28);
            EndianUtilities.WriteBytesLittleEndian(SecurityIndex, buffer, offset + 0x2C);
            EndianUtilities.WriteBytesLittleEndian(ClassNameIndex, buffer, offset + 0x30);
            EndianUtilities.WriteBytesLittleEndian(IndexInParent, buffer, offset + 0x44);
            EndianUtilities.WriteBytesLittleEndian((ushort)Name.Length, buffer, offset + 0x48);
            EndianUtilities.WriteBytesLittleEndian(ClassNameLength, buffer, offset + 0x4A);
            EndianUtilities.StringToBytes(Name, buffer, offset + 0x4C, Name.Length);
        }

        public override string ToString()
        {
            return "Key:" + Name + "[" + Flags + "] <" + Timestamp + ">";
        }
    }
}
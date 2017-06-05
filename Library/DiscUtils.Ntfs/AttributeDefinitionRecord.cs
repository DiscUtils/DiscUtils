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

using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class AttributeDefinitionRecord
    {
        public const int Size = 0xA0;
        public AttributeCollationRule CollationRule;
        public uint DisplayRule;
        public AttributeTypeFlags Flags;
        public long MaxSize;
        public long MinSize;

        public string Name;
        public AttributeType Type;

        internal void Read(byte[] buffer, int offset)
        {
            Name = Encoding.Unicode.GetString(buffer, offset + 0, 128).Trim('\0');
            Type = (AttributeType)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x80);
            DisplayRule = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x84);
            CollationRule = (AttributeCollationRule)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x88);
            Flags = (AttributeTypeFlags)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x8C);
            MinSize = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x90);
            MaxSize = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x98);
        }

        internal void Write(byte[] buffer, int offset)
        {
            Encoding.Unicode.GetBytes(Name, 0, Name.Length, buffer, offset + 0);
            EndianUtilities.WriteBytesLittleEndian((uint)Type, buffer, offset + 0x80);
            EndianUtilities.WriteBytesLittleEndian(DisplayRule, buffer, offset + 0x84);
            EndianUtilities.WriteBytesLittleEndian((uint)CollationRule, buffer, offset + 0x88);
            EndianUtilities.WriteBytesLittleEndian((uint)Flags, buffer, offset + 0x8C);
            EndianUtilities.WriteBytesLittleEndian(MinSize, buffer, offset + 0x90);
            EndianUtilities.WriteBytesLittleEndian(MaxSize, buffer, offset + 0x98);
        }
    }
}
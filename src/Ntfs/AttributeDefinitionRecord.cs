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
using System.Text;

namespace DiscUtils.Ntfs
{
    internal enum AttributeCollationRule : int
    {
        Binary                = 0x00000000,
        Filename              = 0x00000001,
        UnicodeString         = 0x00000002,
        UnsignedLong          = 0x00000010,
        Sid                   = 0x00000011,
        SecurityHash          = 0x00000012,
        MultipleUnsignedLongs = 0x00000013
    }

    [Flags]
    internal enum AttributeTypeFlags : int
    {
        None             = 0x00,
        Indexed          = 0x02,
        Multiple         = 0x04,
        NotZero          = 0x08,
        IndexedUnique    = 0x10,
        NamedUnique      = 0x20,
        MustBeResident   = 0x40,
        CanBeNonResident = 0x80
    }

    internal sealed class AttributeDefinitionRecord
    {
        public const int Size = 0xA0;

        public string Name;
        public AttributeType Type;
        public uint DisplayRule;
        public AttributeCollationRule CollationRule;
        public AttributeTypeFlags Flags;
        public long MinSize;
        public long MaxSize;

        internal void Read(byte[] buffer, int offset)
        {
            Name = Encoding.Unicode.GetString(buffer, offset + 0, 128).Trim('\0');
            Type = (AttributeType)Utilities.ToUInt32LittleEndian(buffer, offset + 0x80);
            DisplayRule = Utilities.ToUInt32LittleEndian(buffer, offset + 0x84);
            CollationRule = (AttributeCollationRule)Utilities.ToUInt32LittleEndian(buffer, offset + 0x88);
            Flags = (AttributeTypeFlags)Utilities.ToUInt32LittleEndian(buffer, offset + 0x8C);
            MinSize = Utilities.ToInt64LittleEndian(buffer, offset + 0x90);
            MaxSize = Utilities.ToInt64LittleEndian(buffer, offset + 0x98);
        }

        internal void Write(byte[] buffer, int offset)
        {
            Encoding.Unicode.GetBytes(Name, 0, Name.Length, buffer, offset + 0);
            Utilities.WriteBytesLittleEndian((uint)Type, buffer, offset + 0x80);
            Utilities.WriteBytesLittleEndian(DisplayRule, buffer, offset + 0x84);
            Utilities.WriteBytesLittleEndian((uint)CollationRule, buffer, offset + 0x88);
            Utilities.WriteBytesLittleEndian((uint)Flags, buffer, offset + 0x8C);
            Utilities.WriteBytesLittleEndian(MinSize, buffer, offset + 0x90);
            Utilities.WriteBytesLittleEndian(MaxSize, buffer, offset + 0x98);
        }
    }
}

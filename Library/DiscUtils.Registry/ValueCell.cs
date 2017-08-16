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

using DiscUtils.Streams;

namespace DiscUtils.Registry
{
    internal sealed class ValueCell : Cell
    {
        private ValueFlags _flags;

        public ValueCell(string name)
            : this(-1)
        {
            Name = name;
        }

        public ValueCell(int index)
            : base(index)
        {
            DataIndex = -1;
        }

        public int DataIndex { get; set; }

        public int DataLength { get; set; }

        public RegistryValueType DataType { get; set; }

        public string Name { get; private set; }

        public override int Size
        {
            get { return 0x14 + (string.IsNullOrEmpty(Name) ? 0 : Name.Length); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            int nameLen = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            DataLength = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x04);
            DataIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x08);
            DataType = (RegistryValueType)EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x0C);
            _flags = (ValueFlags)EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x10);

            if ((_flags & ValueFlags.Named) != 0)
            {
                Name = EndianUtilities.BytesToString(buffer, offset + 0x14, nameLen).Trim('\0');
            }

            return 0x14 + nameLen;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            int nameLen;

            if (string.IsNullOrEmpty(Name))
            {
                _flags &= ~ValueFlags.Named;
                nameLen = 0;
            }
            else
            {
                _flags |= ValueFlags.Named;
                nameLen = Name.Length;
            }

            EndianUtilities.StringToBytes("vk", buffer, offset, 2);
            EndianUtilities.WriteBytesLittleEndian(nameLen, buffer, offset + 0x02);
            EndianUtilities.WriteBytesLittleEndian(DataLength, buffer, offset + 0x04);
            EndianUtilities.WriteBytesLittleEndian(DataIndex, buffer, offset + 0x08);
            EndianUtilities.WriteBytesLittleEndian((int)DataType, buffer, offset + 0x0C);
            EndianUtilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x10);
            if (nameLen != 0)
            {
                EndianUtilities.StringToBytes(Name, buffer, offset + 0x14, nameLen);
            }
        }
    }
}
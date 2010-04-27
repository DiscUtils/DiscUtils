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


namespace DiscUtils.Registry
{
    internal sealed class ValueCell : Cell
    {
        private int _dataLength;
        private int _dataIndex;
        private RegistryValueType _type;
        private ValueFlags _flags;
        private string _name;

        public ValueCell(string name)
            : this(-1)
        {
            _name = name;
        }

        public ValueCell(int index)
            : base(index)
        {
            _dataIndex = -1;
        }

        public int DataLength
        {
            get { return _dataLength; }
            set { _dataLength = value; }
        }

        public int DataIndex
        {
            get { return _dataIndex; }
            set { _dataIndex = value; }
        }

        public RegistryValueType DataType
        {
            get { return _type; }
            set { _type = value; }
        }

        public string Name
        {
            get { return _name; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            int nameLen = Utilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            _dataLength = Utilities.ToInt32LittleEndian(buffer, offset + 0x04);
            _dataIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x08);
            _type = (RegistryValueType)Utilities.ToInt32LittleEndian(buffer, offset + 0x0C);
            _flags = (ValueFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x10);

            if ((_flags & ValueFlags.Named) != 0)
            {
                _name = Utilities.BytesToString(buffer, offset + 0x14, nameLen).Trim('\0');
            }

            return 0x14 + nameLen;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            int nameLen;

            if (string.IsNullOrEmpty(_name))
            {
                _flags &= ~ValueFlags.Named;
                nameLen = 0;
            }
            else
            {
                _flags |= ValueFlags.Named;
                nameLen = _name.Length;
            }

            Utilities.StringToBytes("vk", buffer, offset, 2);
            Utilities.WriteBytesLittleEndian(nameLen, buffer, offset + 0x02);
            Utilities.WriteBytesLittleEndian(_dataLength, buffer, offset + 0x04);
            Utilities.WriteBytesLittleEndian(_dataIndex, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian((int)_type, buffer, offset + 0x0C);
            Utilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x10);
            if (nameLen != 0)
            {
                Utilities.StringToBytes(_name, buffer, offset + 0x14, nameLen);
            }
        }

        public override int Size
        {
            get { return 0x14 + (string.IsNullOrEmpty(_name) ? 0 : _name.Length); }
        }
    }

}

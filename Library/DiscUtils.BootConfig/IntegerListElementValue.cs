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

using System.Globalization;
using DiscUtils.Streams;

namespace DiscUtils.BootConfig
{
    internal class IntegerListElementValue : ElementValue
    {
        private readonly ulong[] _values;

        public IntegerListElementValue(byte[] value)
        {
            _values = new ulong[value.Length / 8];
            for (int i = 0; i < _values.Length; ++i)
            {
                _values[i] = EndianUtilities.ToUInt64LittleEndian(value, i * 8);
            }
        }

        public IntegerListElementValue(ulong[] values)
        {
            _values = values;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.IntegerList; }
        }

        public override string ToString()
        {
            if (_values == null || _values.Length == 0)
            {
                return "<none>";
            }

            string result = string.Empty;
            for (int i = 0; i < _values.Length; ++i)
            {
                if (i != 0)
                {
                    result += " ";
                }

                result += _values[i].ToString("X16", CultureInfo.InvariantCulture);
            }

            return result;
        }

        internal byte[] GetBytes()
        {
            byte[] bytes = new byte[_values.Length * 8];
            for (int i = 0; i < _values.Length; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian(_values[i], bytes, i * 8);
            }

            return bytes;
        }
    }
}
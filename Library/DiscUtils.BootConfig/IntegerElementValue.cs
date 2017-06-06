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
using System.Globalization;
using DiscUtils.Streams;

namespace DiscUtils.BootConfig
{
    internal class IntegerElementValue : ElementValue
    {
        private readonly ulong _value;

        public IntegerElementValue(byte[] value)
        {
            // Actual bytes stored may be less than 8
            byte[] buffer = new byte[8];
            Array.Copy(value, buffer, value.Length);

            _value = EndianUtilities.ToUInt64LittleEndian(buffer, 0);
        }

        public IntegerElementValue(ulong value)
        {
            _value = value;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.Integer; }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}", _value);
        }

        internal byte[] GetBytes()
        {
            byte[] bytes = new byte[8];
            EndianUtilities.WriteBytesLittleEndian(_value, bytes, 0);
            return bytes;
        }
    }
}
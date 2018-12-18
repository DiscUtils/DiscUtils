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

using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Vhd
{
    internal class Header
    {
        public string Cookie;
        public long DataOffset;

        public static Header FromStream(Stream stream)
        {
            return FromBytes(StreamUtilities.ReadExact(stream, 16), 0);
        }

        public static Header FromBytes(byte[] data, int offset)
        {
            Header result = new Header();
            result.Cookie = EndianUtilities.BytesToString(data, offset, 8);
            result.DataOffset = EndianUtilities.ToInt64BigEndian(data, offset + 8);
            return result;
        }
    }
}
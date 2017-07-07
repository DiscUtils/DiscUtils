//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Streams
{
    public static class BitCounter
    {
        private static readonly byte[] _lookupTable;

        static BitCounter()
        {
            _lookupTable = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                byte bitCount = 0;
                var value = i;
                while (value != 0)
                {
                    bitCount++;
                    value &= (byte)(value - 1);
                }
                _lookupTable[i] = bitCount;
            }
        }

        public static byte Count(byte value)
        {
            return _lookupTable[value];
        }

        public static long Count(byte[] values, int start, int count)
        {
            var end = start + count;
            if (end > values.Length)
                return 0;
            var result = 0L;
            for (int i = start; i < end; i++)
            {
                var value = values[i];
                result += _lookupTable[value];
            }
            return result;
        }
    }
}
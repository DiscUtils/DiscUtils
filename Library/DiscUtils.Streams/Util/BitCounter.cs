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

using System;

namespace DiscUtils.Streams
{
    /// <summary>
    /// Helper to count the number of bits set in a byte or byte[]
    /// </summary>
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

        /// <summary>
        /// count the number of bits set in <paramref name="value"/>
        /// </summary>
        /// <returns>the number of bits set in <paramref name="value"/></returns>
        public static byte Count(byte value)
        {
            return _lookupTable[value];
        }

        /// <summary>
        /// count the number of bits set in each entry of <paramref name="values"/>
        /// </summary>
        /// <param name="values">the <see cref="Array"/> to process</param>
        /// <param name="offset">the values offset to start from</param>
        /// <param name="count">the number of bytes to count</param>
        /// <returns></returns>
        public static long Count(byte[] values, int offset, int count)
        {
            var end = offset + count;
            if (end > values.Length)
                throw new ArgumentOutOfRangeException(nameof(count), "can't count after end of values");
            var result = 0L;
            for (int i = offset; i < end; i++)
            {
                var value = values[i];
                result += _lookupTable[value];
            }
            return result;
        }
    }
}
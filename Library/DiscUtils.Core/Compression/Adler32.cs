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

namespace DiscUtils.Compression
{
    /// <summary>
    /// Implementation of the Adler-32 checksum algorithm.
    /// </summary>
    public class Adler32
    {
        private uint _a;
        private uint _b;

        /// <summary>
        /// Initializes a new instance of the Adler32 class.
        /// </summary>
        public Adler32()
        {
            _a = 1;
        }

        /// <summary>
        /// Gets the checksum of all data processed so far.
        /// </summary>
        public int Value
        {
            get { return (int)(_b << 16 | _a); }
        }

        /// <summary>
        /// Provides data that should be checksummed.
        /// </summary>
        /// <param name="buffer">Buffer containing the data to checksum.</param>
        /// <param name="offset">Offset of the first byte to checksum.</param>
        /// <param name="count">The number of bytes to checksum.</param>
        /// <remarks>
        /// Call this method repeatedly until all checksummed
        /// data has been processed.
        /// </remarks>
        public void Process(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentException("Offset outside of array bounds", nameof(offset));
            }

            if (count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentException("Array index out of bounds", nameof(count));
            }

            int processed = 0;
            while (processed < count)
            {
                int innerEnd = Math.Min(count, processed + 2000);
                while (processed < innerEnd)
                {
                    _a += buffer[processed++];
                    _b += _a;
                }

                _a %= 65521;
                _b %= 65521;
            }
        }
    }
}
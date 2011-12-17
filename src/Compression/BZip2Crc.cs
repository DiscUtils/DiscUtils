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

//
// Based on "libbzip2", Copyright (C) 1996-2007 Julian R Seward.
//

namespace DiscUtils.Compression
{
    /// <summary>
    /// Calculates CRC32 of buffers.
    /// </summary>
    internal class BZip2Crc32
    {
        private const uint Polynomial = 0x04c11db7;

        private static readonly uint[] Table;

        private uint _value;

        static BZip2Crc32()
        {
            uint[] table = new uint[256];

            for (uint i = 0; i < 256; ++i)
            {
                uint crc = i << 24;

                for (int j = 8; j > 0; --j)
                {
                    if ((crc & 0x80000000) != 0)
                    {
                        crc = (crc << 1) ^ Polynomial;
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }

                table[i] = crc;
            }

            Table = table;
        }

        public BZip2Crc32()
        {
            _value = 0xFFFFFFFF;
        }

        public uint Value
        {
            get { return _value ^ 0xFFFFFFFF; }
        }

        public void Compute(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                byte b = buffer[offset + i];
                _value = Table[(_value >> 24) ^ b] ^ (_value << 8);
            }
        }
    }
}

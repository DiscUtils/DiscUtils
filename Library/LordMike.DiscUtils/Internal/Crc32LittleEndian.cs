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

namespace DiscUtils.Internal
{
    /// <summary>
    /// Calculates CRC32 of buffers.
    /// </summary>
    internal sealed class Crc32LittleEndian : Crc32
    {
        private static readonly uint[][] Tables;

        static Crc32LittleEndian()
        {
            Tables = new uint[4][];

            Tables[(int)Crc32Algorithm.Common] = CalcTable(0xEDB88320);
            Tables[(int)Crc32Algorithm.Castagnoli] = CalcTable(0x82F63B78);
            Tables[(int)Crc32Algorithm.Koopman] = CalcTable(0xEB31D82E);
            Tables[(int)Crc32Algorithm.Aeronautical] = CalcTable(0xD5828281);
        }

        public Crc32LittleEndian(Crc32Algorithm algorithm)
            : base(Tables[(int)algorithm]) {}

        public static uint Compute(Crc32Algorithm algorithm, byte[] buffer, int offset, int count)
        {
            return Process(Tables[(int)algorithm], 0xFFFFFFFF, buffer, offset, count) ^ 0xFFFFFFFF;
        }

        public override void Process(byte[] buffer, int offset, int count)
        {
            _value = Process(Table, _value, buffer, offset, count);
        }

        private static uint[] CalcTable(uint polynomial)
        {
            uint[] table = new uint[256];

            table[0] = 0;
            for (uint i = 0; i <= 255; ++i)
            {
                uint crc = i;

                for (int j = 8; j > 0; --j)
                {
                    if ((crc & 1) != 0)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }

                table[i] = crc;
            }

            return table;
        }

        private static uint Process(uint[] table, uint accumulator, byte[] buffer, int offset, int count)
        {
            uint value = accumulator;

            for (int i = 0; i < count; ++i)
            {
                byte b = buffer[offset + i];

                uint temp1 = (value >> 8) & 0x00FFFFFF;
                uint temp2 = table[(value ^ b) & 0xFF];
                value = temp1 ^ temp2;
            }

            return value;
        }
    }
}
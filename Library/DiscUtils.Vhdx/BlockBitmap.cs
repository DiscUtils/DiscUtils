//
// Copyright (c) 2008-2012, Kenneth Bell
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

namespace DiscUtils.Vhdx
{
    internal sealed class BlockBitmap
    {
        private readonly byte[] _data;
        private readonly int _length;
        private readonly int _offset;

        public BlockBitmap(byte[] data, int offset, int length)
        {
            _data = data;
            _offset = offset;
            _length = length;
        }

        public int ContiguousSectors(int first, out bool state)
        {
            int matched = 0;
            int bitPos = first % 8;
            int bytePos = first / 8;

            state = (_data[_offset + bytePos] & (1 << bitPos)) != 0;
            byte matchByte = state ? (byte)0xFF : (byte)0;

            while (bytePos < _length)
            {
                if (_data[_offset + bytePos] == matchByte)
                {
                    matched += 8 - bitPos;
                    bytePos++;
                    bitPos = 0;
                }
                else if ((_data[_offset + bytePos] & (1 << bitPos)) != 0 == state)
                {
                    matched++;
                    bitPos++;
                    if (bitPos == 8)
                    {
                        bitPos = 0;
                        bytePos++;
                    }
                }
                else
                {
                    break;
                }
            }

            return matched;
        }

        internal bool MarkSectorsPresent(int first, int count)
        {
            bool changed = false;
            int marked = 0;
            int bitPos = first % 8;
            int bytePos = first / 8;

            while (marked < count)
            {
                if (bitPos == 0 && count - marked >= 8)
                {
                    if (_data[_offset + bytePos] != 0xFF)
                    {
                        _data[_offset + bytePos] = 0xFF;
                        changed = true;
                    }

                    marked += 8;
                    bytePos++;
                }
                else
                {
                    if ((_data[_offset + bytePos] & (1 << bitPos)) == 0)
                    {
                        _data[_offset + bytePos] |= (byte)(1 << bitPos);
                        changed = true;
                    }

                    marked++;
                    bitPos++;
                    if (bitPos == 8)
                    {
                        bitPos = 0;
                        bytePos++;
                    }
                }
            }

            return changed;
        }
    }
}
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

namespace DiscUtils.Compression
{
    /// <summary>
    /// Converts a byte stream into a bit stream.
    /// </summary>
    internal class BigEndianBitStream : BitStream
    {
        private uint _buffer;
        private int _bufferAvailable;
        private readonly Stream _byteStream;

        private readonly byte[] _readBuffer = new byte[2];

        public BigEndianBitStream(Stream byteStream)
        {
            _byteStream = byteStream;
        }

        public override int MaxReadAhead
        {
            get { return 16; }
        }

        public override uint Read(int count)
        {
            if (count > 16)
            {
                uint result = Read(16) << (count - 16);
                return result | Read(count - 16);
            }

            EnsureBufferFilled();

            _bufferAvailable -= count;

            uint mask = (uint)((1 << count) - 1);

            return (_buffer >> _bufferAvailable) & mask;
        }

        public override uint Peek(int count)
        {
            EnsureBufferFilled();

            uint mask = (uint)((1 << count) - 1);

            return (_buffer >> (_bufferAvailable - count)) & mask;
        }

        public override void Consume(int count)
        {
            EnsureBufferFilled();

            _bufferAvailable -= count;
        }

        private void EnsureBufferFilled()
        {
            if (_bufferAvailable < 16)
            {
                _readBuffer[0] = 0;
                _readBuffer[1] = 0;
                _byteStream.Read(_readBuffer, 0, 2);

                _buffer = _buffer << 16 | (uint)(_readBuffer[0] << 8) | _readBuffer[1];
                _bufferAvailable += 16;
            }
        }
    }
}
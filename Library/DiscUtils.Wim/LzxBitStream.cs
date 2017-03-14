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
using System.IO;
using DiscUtils.Compression;

namespace DiscUtils.Wim
{
    /// <summary>
    /// Converts a byte stream into a bit stream.
    /// </summary>
    /// <remarks>
    /// <para>To avoid alignment issues, the bit stream is infinitely long.  Once the
    /// converted byte stream is consumed, an infinite sequence of zero's is emulated.</para>
    /// <para>It is strongly recommended to use some kind of in memory buffering (such as a
    /// BufferedStream) for the wrapped stream.  This class makes a large number of small
    /// reads.</para>.</remarks>
    internal sealed class LzxBitStream : BitStream
    {
        private uint _buffer;
        private int _bufferAvailable;
        private readonly Stream _byteStream;

        private long _position;

        private readonly byte[] _readBuffer = new byte[2];

        public LzxBitStream(Stream byteStream)
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
                throw new ArgumentOutOfRangeException(nameof(count), count, "Maximum 32 bits can be read");
            }

            if (_bufferAvailable < count)
            {
                Need(count);
            }

            _bufferAvailable -= count;
            _position += count;

            uint mask = (uint)((1 << count) - 1);

            return (_buffer >> _bufferAvailable) & mask;
        }

        public override uint Peek(int count)
        {
            if (_bufferAvailable < count)
            {
                Need(count);
            }

            uint mask = (uint)((1 << count) - 1);

            return (_buffer >> (_bufferAvailable - count)) & mask;
        }

        public override void Consume(int count)
        {
            if (_bufferAvailable < count)
            {
                Need(count);
            }

            _bufferAvailable -= count;
            _position += count;
        }

        public void Align(int bits)
        {
            // Note: Consumes 1-16 bits, to force alignment (never 0)
            int offset = (int)(_position % bits);
            Consume(bits - offset);
        }

        public int ReadBytes(byte[] buffer, int offset, int count)
        {
            if (_position % 8 != 0)
            {
                throw new InvalidOperationException("Attempt to read bytes when not byte-aligned");
            }

            int totalRead = 0;
            while (totalRead < count)
            {
                int numRead = _byteStream.Read(buffer, offset + totalRead, count - totalRead);
                if (numRead == 0)
                {
                    _position += totalRead * 8;
                    return totalRead;
                }

                totalRead += numRead;
            }

            _position += totalRead * 8;
            return totalRead;
        }

        public byte[] ReadBytes(int count)
        {
            if (_position % 8 != 0)
            {
                throw new InvalidOperationException("Attempt to read bytes when not byte-aligned");
            }

            byte[] buffer = new byte[count];
            ReadBytes(buffer, 0, count);
            return buffer;
        }

        private void Need(int count)
        {
            while (_bufferAvailable < count)
            {
                _readBuffer[0] = 0;
                _readBuffer[1] = 0;
                _byteStream.Read(_readBuffer, 0, 2);

                _buffer = _buffer << 16 | (uint)(_readBuffer[1] << 8) | _readBuffer[0];
                _bufferAvailable += 16;
            }
        }
    }
}
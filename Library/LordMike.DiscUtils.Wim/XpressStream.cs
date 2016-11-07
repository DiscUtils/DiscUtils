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
    /// Implements the XPRESS decompression algorithm.
    /// </summary>
    /// <remarks>This class is optimized for the case where the entire stream contents
    /// fit into memory, it is not suitable for unbounded streams.</remarks>
    internal class XpressStream : Stream
    {
        private readonly byte[] _buffer;
        private readonly Stream _compressedStream;
        private long _position;

        /// <summary>
        /// Initializes a new instance of the XpressStream class.
        /// </summary>
        /// <param name="compressed">The stream of compressed data.</param>
        /// <param name="count">The length of this stream (in uncompressed bytes).</param>
        public XpressStream(Stream compressed, int count)
        {
            _compressedStream = new BufferedStream(compressed);
            _buffer = Buffer(count);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return _buffer.Length; }
        }

        public override long Position
        {
            get { return _position; }

            set { _position = value; }
        }

        public override void Flush() {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position > Length)
            {
                return 0;
            }

            int numToRead = (int)Math.Min(count, _buffer.Length - _position);
            Array.Copy(_buffer, (int)_position, buffer, offset, numToRead);
            _position += numToRead;
            return numToRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private HuffmanTree ReadHuffmanTree()
        {
            uint[] lengths = new uint[256 + 16 * 16];

            for (int i = 0; i < lengths.Length; i += 2)
            {
                int b = ReadCompressedByte();

                lengths[i] = (uint)(b & 0xF);
                lengths[i + 1] = (uint)(b >> 4);
            }

            return new HuffmanTree(lengths);
        }

        private byte[] Buffer(int count)
        {
            byte[] buffer = new byte[count];
            int numRead = 0;

            HuffmanTree tree = ReadHuffmanTree();
            XpressBitStream bitStream = new XpressBitStream(_compressedStream);

            while (numRead < count)
            {
                uint symbol = tree.NextSymbol(bitStream);
                if (symbol < 256)
                {
                    // The first 256 symbols are literal byte values
                    buffer[numRead] = (byte)symbol;
                    numRead++;
                }
                else
                {
                    // The next 256 symbols are 4 bits each for offset and length.
                    int offsetBits = (int)((symbol - 256) / 16);
                    int len = (int)((symbol - 256) % 16);

                    // The actual offset
                    int offset = (int)((1 << offsetBits) - 1 + bitStream.Read(offsetBits));

                    // Lengths up to 15 bytes are stored directly in the symbol bits, beyond that
                    // the length is stored in the compression stream.
                    if (len == 15)
                    {
                        // Note this access is directly to the underlying stream - we're not going
                        // through the bit stream.  This makes the precise behaviour of the bit stream,
                        // in terms of read-ahead critical.
                        int b = ReadCompressedByte();

                        if (b == 0xFF)
                        {
                            // Again, note this access is directly to the underlying stream - we're not going
                            // through the bit stream.
                            len = ReadCompressedUShort();
                        }
                        else
                        {
                            len += b;
                        }
                    }

                    // Minimum length for a match is 3 bytes, so all lengths are stored as an offset
                    // from 3.
                    len += 3;

                    // Simply do the copy
                    for (int i = 0; i < len; ++i)
                    {
                        buffer[numRead] = buffer[numRead - offset - 1];
                        numRead++;
                    }
                }
            }

            return buffer;
        }

        private int ReadCompressedByte()
        {
            int b = _compressedStream.ReadByte();
            if (b < 0)
            {
                throw new InvalidDataException("Truncated stream");
            }

            return b;
        }

        private int ReadCompressedUShort()
        {
            int result = ReadCompressedByte();
            return result | ReadCompressedByte() << 8;
        }
    }
}
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
using DiscUtils.Streams;

namespace DiscUtils.Wim
{
    /// <summary>
    /// Class to read data compressed using LZX algorithm.
    /// </summary>
    /// <remarks>This is not a general purpose LZX decompressor - it makes
    /// simplifying assumptions, such as being able to load the entire stream
    /// contents into memory..</remarks>
    internal class LzxStream : Stream
    {
        private static readonly uint[] _positionSlots;
        private static readonly uint[] _extraBits;
        private HuffmanTree _alignedOffsetTree;

        private readonly LzxBitStream _bitStream;

        private byte[] _buffer;
        private int _bufferCount;
        private readonly int _fileSize;
        private HuffmanTree _lengthTree;

        // Block state
        private HuffmanTree _mainTree;
        private readonly int _numPositionSlots;

        private long _position;
        private readonly uint[] _repeatedOffsets = { 1, 1, 1 };
        private readonly int _windowBits;

        static LzxStream()
        {
            _positionSlots = new uint[50];
            _extraBits = new uint[50];

            uint numBits = 0;
            _positionSlots[1] = 1;
            for (int i = 2; i < 50; i += 2)
            {
                _extraBits[i] = numBits;
                _extraBits[i + 1] = numBits;
                _positionSlots[i] = _positionSlots[i - 1] + (uint)(1 << (int)_extraBits[i - 1]);
                _positionSlots[i + 1] = _positionSlots[i] + (uint)(1 << (int)numBits);

                if (numBits < 17)
                {
                    numBits++;
                }
            }
        }

        public LzxStream(Stream stream, int windowBits, int fileSize)
        {
            _bitStream = new LzxBitStream(new BufferedStream(stream, 8192));
            _windowBits = windowBits;
            _fileSize = fileSize;
            _numPositionSlots = _windowBits * 2;
            _buffer = new byte[1 << windowBits];

            ReadBlocks();
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
            get { return _bufferCount; }
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

            int numToRead = (int)Math.Min(count, _bufferCount - _position);
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

        private void ReadBlocks()
        {
            BlockType blockType = (BlockType)_bitStream.Read(3);

            _buffer = new byte[32768];
            _bufferCount = 0;

            while (blockType != BlockType.None)
            {
                int blockSize = _bitStream.Read(1) == 1 ? 1 << 15 : (int)_bitStream.Read(16);

                if (blockType == BlockType.Uncompressed)
                {
                    DecodeUncompressedBlock(blockSize);
                }
                else
                {
                    DecodeCompressedBlock(blockType, blockSize);

                    _bufferCount += blockSize;
                }

                // Read start of next block (if any)
                blockType = (BlockType)_bitStream.Read(3);
            }

            FixupBlockBuffer();
        }

        /// <summary>
        /// Fix up CALL instruction optimization.
        /// </summary>
        /// <remarks>A slightly odd feature of LZX for optimizing executable compression is that
        /// relative CALL instructions (opcode E8) are converted to absolute values before compression.
        /// This feature seems to always be turned-on in WIM files, so we have to apply the reverse
        /// conversion.</remarks>
        private void FixupBlockBuffer()
        {
            byte[] temp = new byte[4];

            int i = 0;
            while (i < _bufferCount - 10)
            {
                if (_buffer[i] == 0xE8)
                {
                    Array.Copy(_buffer, i + 1, temp, 0, 4);
                    int absoluteValue = EndianUtilities.ToInt32LittleEndian(_buffer, i + 1);

                    if (absoluteValue >= -i && absoluteValue < _fileSize)
                    {
                        int offsetValue;
                        if (absoluteValue >= 0)
                        {
                            offsetValue = absoluteValue - i;
                        }
                        else
                        {
                            offsetValue = absoluteValue + _fileSize;
                        }

                        EndianUtilities.WriteBytesLittleEndian(offsetValue, _buffer, i + 1);
                    }

                    i += 4;
                }

                ++i;
            }
        }

        private void DecodeUncompressedBlock(int blockSize)
        {
            _bitStream.Align(16);
            _repeatedOffsets[0] = EndianUtilities.ToUInt32LittleEndian(_bitStream.ReadBytes(4), 0);
            _repeatedOffsets[1] = EndianUtilities.ToUInt32LittleEndian(_bitStream.ReadBytes(4), 0);
            _repeatedOffsets[2] = EndianUtilities.ToUInt32LittleEndian(_bitStream.ReadBytes(4), 0);
            int numRead = _bitStream.ReadBytes(_buffer, _bufferCount, blockSize);
            _bufferCount += numRead;

            if ((numRead & 1) != 0)
            {
                _bitStream.ReadBytes(1);
            }
        }

        private void DecodeCompressedBlock(BlockType blockType, int blockSize)
        {
            if (blockType == BlockType.AlignedOffset)
            {
                _alignedOffsetTree = ReadFixedHuffmanTree(8, 3);
            }

            ReadMainTree();
            ReadLengthTree();

            uint numRead = 0;
            while (numRead < (uint)blockSize)
            {
                uint symbol = _mainTree.NextSymbol(_bitStream);

                if (symbol < 256)
                {
                    _buffer[_bufferCount + numRead++] = (byte)symbol;
                }
                else
                {
                    uint lengthHeader = (symbol - 256) & 7;
                    uint matchLength = lengthHeader + 2 + (lengthHeader == 7 ? _lengthTree.NextSymbol(_bitStream) : 0);
                    uint positionSlot = (symbol - 256) >> 3;

                    uint matchOffset;
                    if (positionSlot == 0)
                    {
                        matchOffset = _repeatedOffsets[0];
                    }
                    else if (positionSlot == 1)
                    {
                        matchOffset = _repeatedOffsets[1];
                        _repeatedOffsets[1] = _repeatedOffsets[0];
                        _repeatedOffsets[0] = matchOffset;
                    }
                    else if (positionSlot == 2)
                    {
                        matchOffset = _repeatedOffsets[2];
                        _repeatedOffsets[2] = _repeatedOffsets[0];
                        _repeatedOffsets[0] = matchOffset;
                    }
                    else
                    {
                        int extra = (int)_extraBits[positionSlot];

                        uint formattedOffset;

                        if (blockType == BlockType.AlignedOffset)
                        {
                            uint verbatimBits = 0;
                            uint alignedBits = 0;

                            if (extra >= 3)
                            {
                                verbatimBits = _bitStream.Read(extra - 3) << 3;
                                alignedBits = _alignedOffsetTree.NextSymbol(_bitStream);
                            }
                            else if (extra > 0)
                            {
                                verbatimBits = _bitStream.Read(extra);
                            }

                            formattedOffset = _positionSlots[positionSlot] + verbatimBits + alignedBits;
                        }
                        else
                        {
                            uint verbatimBits = extra > 0 ? _bitStream.Read(extra) : 0;

                            formattedOffset = _positionSlots[positionSlot] + verbatimBits;
                        }

                        matchOffset = formattedOffset - 2;

                        _repeatedOffsets[2] = _repeatedOffsets[1];
                        _repeatedOffsets[1] = _repeatedOffsets[0];
                        _repeatedOffsets[0] = matchOffset;
                    }

                    int destOffset = _bufferCount + (int)numRead;
                    int srcOffset = destOffset - (int)matchOffset;
                    for (int i = 0; i < matchLength; ++i)
                    {
                        _buffer[destOffset + i] = _buffer[srcOffset + i];
                    }

                    numRead += matchLength;
                }
            }
        }

        private void ReadMainTree()
        {
            uint[] lengths;

            if (_mainTree == null)
            {
                lengths = new uint[256 + 8 * _numPositionSlots];
            }
            else
            {
                lengths = _mainTree.Lengths;
            }

            HuffmanTree preTree = ReadFixedHuffmanTree(20, 4);
            ReadLengths(preTree, lengths, 0, 256);
            preTree = ReadFixedHuffmanTree(20, 4);
            ReadLengths(preTree, lengths, 256, 8 * _numPositionSlots);

            _mainTree = new HuffmanTree(lengths);
        }

        private void ReadLengthTree()
        {
            HuffmanTree preTree = ReadFixedHuffmanTree(20, 4);
            _lengthTree = ReadDynamicHuffmanTree(249, preTree, _lengthTree);
        }

        private HuffmanTree ReadFixedHuffmanTree(int count, int bits)
        {
            uint[] treeLengths = new uint[count];
            for (int i = 0; i < treeLengths.Length; ++i)
            {
                treeLengths[i] = _bitStream.Read(bits);
            }

            return new HuffmanTree(treeLengths);
        }

        private HuffmanTree ReadDynamicHuffmanTree(int count, HuffmanTree preTree, HuffmanTree oldTree)
        {
            uint[] lengths;

            if (oldTree == null)
            {
                lengths = new uint[256 + 8 * _numPositionSlots];
            }
            else
            {
                lengths = oldTree.Lengths;
            }

            ReadLengths(preTree, lengths, 0, count);

            return new HuffmanTree(lengths);
        }

        private void ReadLengths(HuffmanTree preTree, uint[] lengths, int offset, int count)
        {
            int i = 0;

            while (i < count)
            {
                uint value = preTree.NextSymbol(_bitStream);

                if (value == 17)
                {
                    uint numZeros = 4 + _bitStream.Read(4);
                    for (uint j = 0; j < numZeros; ++j)
                    {
                        lengths[offset + i] = 0;
                        ++i;
                    }
                }
                else if (value == 18)
                {
                    uint numZeros = 20 + _bitStream.Read(5);
                    for (uint j = 0; j < numZeros; ++j)
                    {
                        lengths[offset + i] = 0;
                        ++i;
                    }
                }
                else if (value == 19)
                {
                    uint same = _bitStream.Read(1);
                    value = preTree.NextSymbol(_bitStream);

                    if (value > 16)
                    {
                        throw new InvalidDataException("Invalid table encoding");
                    }

                    uint symbol = (17 + lengths[offset + i] - value) % 17;
                    for (uint j = 0; j < 4 + same; ++j)
                    {
                        lengths[offset + i] = symbol;
                        ++i;
                    }
                }
                else
                {
                    lengths[offset + i] = (17 + lengths[offset + i] - value) % 17;
                    ++i;
                }
            }
        }

        private enum BlockType
        {
            None = 0,
            Verbatim = 1,
            AlignedOffset = 2,
            Uncompressed = 3
        }
    }
}
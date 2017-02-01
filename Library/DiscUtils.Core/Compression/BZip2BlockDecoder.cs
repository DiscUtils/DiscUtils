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

using System.IO;

namespace DiscUtils.Compression
{
    internal class BZip2BlockDecoder
    {
        private readonly InverseBurrowsWheeler _inverseBurrowsWheeler;

        public BZip2BlockDecoder(int blockSize)
        {
            _inverseBurrowsWheeler = new InverseBurrowsWheeler(blockSize);
        }

        public uint Crc { get; private set; }

        public int Process(BitStream bitstream, byte[] outputBuffer, int outputBufferOffset)
        {
            Crc = 0;
            for (int i = 0; i < 4; ++i)
            {
                Crc = (Crc << 8) | bitstream.Read(8);
            }

            bool rand = bitstream.Read(1) != 0;
            int origPtr = (int)bitstream.Read(24);

            int thisBlockSize = ReadBuffer(bitstream, outputBuffer, outputBufferOffset);

            _inverseBurrowsWheeler.OriginalIndex = origPtr;
            _inverseBurrowsWheeler.Process(outputBuffer, outputBufferOffset, thisBlockSize, outputBuffer,
                outputBufferOffset);

            if (rand)
            {
                BZip2Randomizer randomizer = new BZip2Randomizer();
                randomizer.Process(outputBuffer, outputBufferOffset, thisBlockSize, outputBuffer, outputBufferOffset);
            }

            return thisBlockSize;
        }

        private static int ReadBuffer(BitStream bitstream, byte[] buffer, int offset)
        {
            // The MTF state
            int numInUse = 0;
            MoveToFront moveFrontTransform = new MoveToFront();
            bool[] inUseGroups = new bool[16];
            for (int i = 0; i < 16; ++i)
            {
                inUseGroups[i] = bitstream.Read(1) != 0;
            }

            for (int i = 0; i < 256; ++i)
            {
                if (inUseGroups[i / 16])
                {
                    if (bitstream.Read(1) != 0)
                    {
                        moveFrontTransform.Set(numInUse, (byte)i);
                        numInUse++;
                    }
                }
            }

            // Initialize 'virtual' Huffman tree from bitstream
            BZip2CombinedHuffmanTrees huffmanTree = new BZip2CombinedHuffmanTrees(bitstream, numInUse + 2);

            // Main loop reading data
            int readBytes = 0;
            while (true)
            {
                uint symbol = huffmanTree.NextSymbol();

                if (symbol < 2)
                {
                    // RLE, with length stored in a binary-style format
                    uint runLength = 0;
                    int bitShift = 0;
                    while (symbol < 2)
                    {
                        runLength += (symbol + 1) << bitShift;
                        bitShift++;

                        symbol = huffmanTree.NextSymbol();
                    }

                    byte b = moveFrontTransform.Head;
                    while (runLength > 0)
                    {
                        buffer[offset + readBytes] = b;
                        ++readBytes;
                        --runLength;
                    }
                }

                if (symbol <= numInUse)
                {
                    // Single byte
                    byte b = moveFrontTransform.GetAndMove((int)symbol - 1);
                    buffer[offset + readBytes] = b;
                    ++readBytes;
                }
                else if (symbol == numInUse + 1)
                {
                    // End of block marker
                    return readBytes;
                }
                else
                {
                    throw new InvalidDataException("Invalid symbol from Huffman table");
                }
            }
        }
    }
}
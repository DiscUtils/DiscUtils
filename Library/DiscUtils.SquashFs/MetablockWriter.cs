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
using System.IO.Compression;
using DiscUtils.Compression;
using DiscUtils.Streams;

namespace DiscUtils.SquashFs
{
    internal sealed class MetablockWriter : IDisposable
    {
        private MemoryStream _buffer;

        private readonly byte[] _currentBlock;
        private int _currentBlockNum;
        private int _currentOffset;

        public MetablockWriter()
        {
            _currentBlock = new byte[8 * 1024];
            _buffer = new MemoryStream();
        }

        public MetadataRef Position
        {
            get { return new MetadataRef(_currentBlockNum, _currentOffset); }
        }

        public void Dispose()
        {
            if (_buffer != null)
            {
                _buffer.Dispose();
                _buffer = null;
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            int totalStored = 0;

            while (totalStored < count)
            {
                int toCopy = Math.Min(_currentBlock.Length - _currentOffset, count - totalStored);
                Array.Copy(buffer, offset + totalStored, _currentBlock, _currentOffset, toCopy);
                _currentOffset += toCopy;
                totalStored += toCopy;

                if (_currentOffset == _currentBlock.Length)
                {
                    NextBlock();
                    _currentOffset = 0;
                }
            }
        }

        internal void Persist(Stream output)
        {
            if (_currentOffset > 0)
            {
                NextBlock();
            }

            output.Write(_buffer.ToArray(), 0, (int)_buffer.Length);
        }

        internal long DistanceFrom(MetadataRef startPos)
        {
            return (_currentBlockNum - startPos.Block) * VfsSquashFileSystemReader.MetadataBufferSize
                   + (_currentOffset - startPos.Offset);
        }

        private void NextBlock()
        {
            MemoryStream compressed = new MemoryStream();
            using (ZlibStream compStream = new ZlibStream(compressed, CompressionMode.Compress, true))
            {
                compStream.Write(_currentBlock, 0, _currentOffset);
            }

            byte[] writeData;
            ushort writeLen;
            if (compressed.Length < _currentOffset)
            {
                writeData = compressed.ToArray();
                writeLen = (ushort)compressed.Length;
            }
            else
            {
                writeData = _currentBlock;
                writeLen = (ushort)(_currentOffset | 0x8000);
            }

            byte[] header = new byte[2];
            EndianUtilities.WriteBytesLittleEndian(writeLen, header, 0);
            _buffer.Write(header, 0, 2);
            _buffer.Write(writeData, 0, writeLen & 0x7FFF);

            ++_currentBlockNum;
        }
    }
}
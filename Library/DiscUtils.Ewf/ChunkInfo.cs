//
// Copyright (c) 2013, Adam Bridge
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

namespace DiscUtils.Ewf
{
    class ChunkInfo
    {
        int _fileIndex, _bytesInChunk;
        long _start, _fileOffset, _length;
        bool _isCompressed;

        /// <summary>
        /// The offset of the emulated stream that this chunk represents.
        /// </summary>
        public long Start { get { return _start; } }

        /// <summary>
        /// The number of bytes of the emulated stream that this chunk holds.
        /// </summary>
        public long Length { get { return _length; } }

        /// <summary>
        /// Pointer to the segment file in which this chunk resides.
        /// </summary>
        public int FileIndex { get { return _fileIndex; } }

        /// <summary>
        /// The offset in the segment file at which this chunk starts.
        /// </summary>
        public long FileOffset { get { return _fileOffset; } }

        /// <summary>
        /// The number of bytes stored in the segment file for this chunk.
        /// </summary>
        public int BytesInChunk { get { return _bytesInChunk; } }

        /// <summary>
        /// <c>true</c> if the chunk in the segment file is compressed, <c>false</c> otherwise.
        /// </summary>
        public bool IsCompressed { get { return _isCompressed; } }

        public ChunkInfo(int fileIndex, long fileOffset, long start, long length, bool isCompressed, int bytesInChunk)
        {
            if (fileOffset < 89)
            {
                throw new ArgumentException("Fileoffset for chunk cannot be less than 89");
            }

            if (start < 0)
            {
                throw new ArgumentException("Start of chunk cannot be negative");
            }

            if (length < 0)
            {
                throw new ArgumentException("Chunk length cannot be negative");
            }

            _fileIndex = fileIndex;
            _fileOffset = fileOffset;
            _start = start;
            _length = length;
            _isCompressed = isCompressed;
            _bytesInChunk = bytesInChunk;            
        }
    }
}

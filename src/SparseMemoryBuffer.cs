//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.Collections.Generic;
using System.Linq;

namespace DiscUtils
{
    /// <summary>
    /// A sparse in-memory buffer.
    /// </summary>
    /// <remarks>This class is useful for storing large sparse buffers in memory, unused
    /// chunks of the buffer are not stored (assumed to be zero).</remarks>
    public sealed class SparseMemoryBuffer
    {
        private Dictionary<int, byte[]> _buffers;
        private int _chunkSize;

        private long _capacity;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="chunkSize">The size of each allocation chunk</param>
        public SparseMemoryBuffer(int chunkSize)
        {
            _chunkSize = chunkSize;
            _buffers = new Dictionary<int, byte[]>();
        }

        /// <summary>
        /// Reads a section of the sparse buffer into a byte array.
        /// </summary>
        /// <param name="pos">The offset within the sparse buffer to start reading.</param>
        /// <param name="buffer">The destination byte array.</param>
        /// <param name="offset">The start offset within the destination buffer.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The actual number of bytes read</returns>
        public int Read(long pos, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count && (pos + totalRead) < _capacity)
            {
                int chunk = (int)(pos / _chunkSize);
                int chunkOffset = (int)(pos % _chunkSize);
                int numToRead = (int)Math.Min(Math.Min(_chunkSize - chunkOffset, _capacity - (pos + totalRead)), count - totalRead);

                if (_buffers.Count <= chunk || _buffers[chunk] == null)
                {
                    Array.Clear(buffer, offset + totalRead, numToRead);
                }
                else
                {
                    Array.Copy(_buffers[chunk], chunkOffset, buffer, offset + totalRead, numToRead);
                }

                totalRead += numToRead;
                pos += numToRead;
            }

            return totalRead;
        }

        /// <summary>
        /// Writes a byte array into the sparse buffer.
        /// </summary>
        /// <param name="pos">The start offset within the sparse buffer.</param>
        /// <param name="buffer">The source byte array.</param>
        /// <param name="offset">The start offset within the source byte array.</param>
        /// <param name="count">The number of bytes to write.</param>
        public void Write(long pos, byte[] buffer, int offset, int count)
        {
            int totalWritten = 0;

            while (totalWritten < count)
            {
                int chunk = (int)(pos / _chunkSize);
                int chunkOffset = (int)(pos % _chunkSize);
                int numToWrite = (int)Math.Min(_chunkSize - chunkOffset, count - totalWritten);

                if (_buffers.Count <= chunk || _buffers[chunk] == null)
                {
                    _buffers[chunk] = new byte[_chunkSize];
                }
                Array.Copy(buffer, offset + totalWritten, _buffers[chunk], chunkOffset, numToWrite);

                totalWritten += numToWrite;
                pos += numToWrite;
            }

            _capacity = Math.Max(_capacity, pos + totalWritten);
        }

        /// <summary>
        /// Gets the current capacity of the sparse buffer (number of logical bytes stored).
        /// </summary>
        public long Capacity
        {
            get { return _capacity; }
        }

        /// <summary>
        /// Sets the capacity of the sparse buffer, truncating if appropriate.
        /// </summary>
        /// <param name="value">The desired capacity of the buffer.</param>
        /// <remarks>This method does not allocate any chunks, it merely records the logical
        /// capacity of the sparse buffer.  Writes beyond the specified capacity will increase
        /// the capacity.</remarks>
        public void SetCapacity(long value)
        {
            _capacity = value;
        }

        /// <summary>
        /// Gets the size of each allocation chunk.
        /// </summary>
        public int ChunkSize
        {
            get { return _chunkSize; }
        }

        /// <summary>
        /// Gets the (sorted) list of allocated chunks, as chunk indexes.
        /// </summary>
        /// <returns>An enumeration of chunk indexes</returns>
        /// <remarks>This method returns chunks as an index rather than absolute stream position.
        /// For example, if ChunkSize is 16KB, and the first 32KB of the buffer is actually stored,
        /// this method will return 0 and 1.  This indicates the first and second chunks are stored.</remarks>
        public IEnumerable<int> AllocatedChunks
        {
            get
            {
                return from entry in _buffers
                       orderby entry.Key
                       select entry.Key;
            }
        }
    }
}

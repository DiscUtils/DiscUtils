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
using System.Collections.Generic;

namespace DiscUtils.Streams
{
    /// <summary>
    /// A sparse in-memory buffer.
    /// </summary>
    /// <remarks>This class is useful for storing large sparse buffers in memory, unused
    /// chunks of the buffer are not stored (assumed to be zero).</remarks>
    public sealed class SparseMemoryBuffer : Buffer
    {
        private readonly Dictionary<int, byte[]> _buffers;

        private long _capacity;

        /// <summary>
        /// Initializes a new instance of the SparseMemoryBuffer class.
        /// </summary>
        /// <param name="chunkSize">The size of each allocation chunk.</param>
        public SparseMemoryBuffer(int chunkSize)
        {
            ChunkSize = chunkSize;
            _buffers = new Dictionary<int, byte[]>();
        }

        /// <summary>
        /// Gets the (sorted) list of allocated chunks, as chunk indexes.
        /// </summary>
        /// <returns>An enumeration of chunk indexes.</returns>
        /// <remarks>This method returns chunks as an index rather than absolute stream position.
        /// For example, if ChunkSize is 16KB, and the first 32KB of the buffer is actually stored,
        /// this method will return 0 and 1.  This indicates the first and second chunks are stored.</remarks>
        public IEnumerable<int> AllocatedChunks
        {
            get
            {
                List<int> keys = new List<int>(_buffers.Keys);
                keys.Sort();
                return keys;
            }
        }

        /// <summary>
        /// Indicates this stream can be read (always <c>true</c>).
        /// </summary>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// Indicates this stream can be written (always <c>true</c>).
        /// </summary>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the current capacity of the sparse buffer (number of logical bytes stored).
        /// </summary>
        public override long Capacity
        {
            get { return _capacity; }
        }

        /// <summary>
        /// Gets the size of each allocation chunk.
        /// </summary>
        public int ChunkSize { get; }

        /// <summary>
        /// Accesses this memory buffer as an infinite byte array.
        /// </summary>
        /// <param name="pos">The buffer position to read.</param>
        /// <returns>The byte stored at this position (or Zero if not explicitly stored).</returns>
        public byte this[long pos]
        {
            get
            {
                byte[] buffer = new byte[1];
                if (Read(pos, buffer, 0, 1) != 0)
                {
                    return buffer[0];
                }
                return 0;
            }

            set
            {
                byte[] buffer = new byte[1];
                buffer[0] = value;
                Write(pos, buffer, 0, 1);
            }
        }

        /// <summary>
        /// Reads a section of the sparse buffer into a byte array.
        /// </summary>
        /// <param name="pos">The offset within the sparse buffer to start reading.</param>
        /// <param name="buffer">The destination byte array.</param>
        /// <param name="offset">The start offset within the destination buffer.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (count > 0 && pos < _capacity)
            {
                int chunk = (int)(pos / ChunkSize);
                int chunkOffset = (int)(pos % ChunkSize);
                int numToRead = (int)Math.Min(Math.Min(ChunkSize - chunkOffset, _capacity - pos), count);

                if (!_buffers.TryGetValue(chunk, out byte[] chunkBuffer))
                {
                    Array.Clear(buffer, offset, numToRead);
                }
                else
                {
                    Array.Copy(chunkBuffer, chunkOffset, buffer, offset, numToRead);
                }

                totalRead += numToRead;
                offset += numToRead;
                count -= numToRead;
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
        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int chunk = (int)(pos / ChunkSize);
                int chunkOffset = (int)(pos % ChunkSize);
                int numToWrite = Math.Min(ChunkSize - chunkOffset, count);

                if (!_buffers.TryGetValue(chunk, out byte[] chunkBuffer))
                {
                    chunkBuffer = new byte[ChunkSize];
                    _buffers[chunk] = chunkBuffer;
                }

                Array.Copy(buffer, offset, chunkBuffer, chunkOffset, numToWrite);

                offset += numToWrite;
                count -= numToWrite;
                pos += numToWrite;
            }

            _capacity = Math.Max(_capacity, pos);
        }

        /// <summary>
        /// Clears bytes from the buffer.
        /// </summary>
        /// <param name="pos">The start offset within the buffer.</param>
        /// <param name="count">The number of bytes to clear.</param>
        public override void Clear(long pos, int count)
        {
            while (count > 0)
            {
                int chunk = (int)(pos / ChunkSize);
                int chunkOffset = (int)(pos % ChunkSize);
                int numToClear = Math.Min(ChunkSize - chunkOffset, count);

                if (_buffers.TryGetValue(chunk, out byte[] chunkBuffer))
                {
                    if (chunkOffset == 0 && numToClear == ChunkSize)
                    {
                        _buffers.Remove(chunk);
                    }
                    else
                    {
                        Array.Clear(chunkBuffer, chunkOffset, numToClear);
                    }
                }

                count -= numToClear;
                pos += numToClear;
            }

            _capacity = Math.Max(_capacity, pos);
        }

        /// <summary>
        /// Sets the capacity of the sparse buffer, truncating if appropriate.
        /// </summary>
        /// <param name="value">The desired capacity of the buffer.</param>
        /// <remarks>This method does not allocate any chunks, it merely records the logical
        /// capacity of the sparse buffer.  Writes beyond the specified capacity will increase
        /// the capacity.</remarks>
        public override void SetCapacity(long value)
        {
            _capacity = value;
        }

        /// <summary>
        /// Gets the parts of a buffer that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest.</param>
        /// <param name="count">The number of bytes of interest.</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            long end = start + count;
            foreach (int chunk in AllocatedChunks)
            {
                long chunkStart = chunk * (long)ChunkSize;
                long chunkEnd = chunkStart + ChunkSize;
                if (chunkEnd > start && chunkStart < end)
                {
                    long extentStart = Math.Max(start, chunkStart);
                    yield return new StreamExtent(extentStart, Math.Min(chunkEnd, end) - extentStart);
                }
            }
        }
    }
}
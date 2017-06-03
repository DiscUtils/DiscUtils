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

namespace DiscUtils.Streams
{
    /// <summary>
    /// Statistical information about the effectiveness of a BlockCache instance.
    /// </summary>
    public sealed class BlockCacheStatistics
    {
        /// <summary>
        /// Gets the number of free blocks in the read cache.
        /// </summary>
        public int FreeReadBlocks { get; internal set; }

        /// <summary>
        /// Gets the number of requested 'large' reads, as defined by the LargeReadSize setting.
        /// </summary>
        public long LargeReadsIn { get; internal set; }

        /// <summary>
        /// Gets the number of times a read request was serviced (in part or whole) from the cache.
        /// </summary>
        public long ReadCacheHits { get; internal set; }

        /// <summary>
        /// Gets the number of time a read request was serviced (in part or whole) from the wrapped stream.
        /// </summary>
        public long ReadCacheMisses { get; internal set; }

        /// <summary>
        /// Gets the total number of requested reads.
        /// </summary>
        public long TotalReadsIn { get; internal set; }

        /// <summary>
        /// Gets the total number of reads passed on by the cache.
        /// </summary>
        public long TotalReadsOut { get; internal set; }

        /// <summary>
        /// Gets the total number of requested writes.
        /// </summary>
        public long TotalWritesIn { get; internal set; }

        /// <summary>
        /// Gets the number of requested unaligned reads.
        /// </summary>
        /// <remarks>Unaligned reads are reads where the read doesn't start on a multiple of
        /// the block size.</remarks>
        public long UnalignedReadsIn { get; internal set; }

        /// <summary>
        /// Gets the number of requested unaligned writes.
        /// </summary>
        /// <remarks>Unaligned writes are writes where the write doesn't start on a multiple of
        /// the block size.</remarks>
        public long UnalignedWritesIn { get; internal set; }
    }
}
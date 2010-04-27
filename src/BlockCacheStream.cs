//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.IO;

namespace DiscUtils
{
    /// <summary>
    /// Settings controlling BlockCache instances.
    /// </summary>
    public sealed class BlockCacheSettings
    {
        /// <summary>
        /// Creates a new instance, with default settings.
        /// </summary>
        public BlockCacheSettings()
        {
            BlockSize = (int)(4 * Sizes.OneKiB);
            ReadCacheSize = 4 * Sizes.OneMiB;
            LargeReadSize = 64 * Sizes.OneKiB;
            OptimumReadSize = (int)(64 * Sizes.OneKiB);
        }

        internal BlockCacheSettings(BlockCacheSettings settings)
        {
            BlockSize = settings.BlockSize;
            ReadCacheSize = settings.ReadCacheSize;
            LargeReadSize = settings.LargeReadSize;
            OptimumReadSize = settings.OptimumReadSize;
        }

        /// <summary>
        /// The size (in bytes) of each cached block.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// The size (in bytes) of the read cache.
        /// </summary>
        public long ReadCacheSize { get; set; }

        /// <summary>
        /// The maximum read size that will be cached, and not bypass the cache.
        /// </summary>
        /// <remarks>Large reads are not cached, on the assumption they will not
        /// be repeated.  This setting controls what is considered 'large'.
        /// Any read that is more than this many bytes will not be cached.</remarks>
        public long LargeReadSize { get; set; }

        /// <summary>
        /// The optimum size of a read to the wrapped stream.
        /// </summary>
        /// <remarks>This value must be a multiple of BlockSize</remarks>
        public int OptimumReadSize { get; set; }
    }

    /// <summary>
    /// Statistical information about the effectiveness of a BlockCache instance.
    /// </summary>
    public sealed class BlockCacheStatistics
    {
        /// <summary>
        /// The number of requested 'large' reads, as defined by the LargeReadSize setting.
        /// </summary>
        public long LargeReadsIn { get; internal set; }

        /// <summary>
        /// The number of requested unaligned reads.
        /// </summary>
        /// <remarks>Unaligned reads are reads where the read doesn't start on a multiple of
        /// the block size.</remarks>
        public long UnalignedReadsIn { get; internal set; }

        /// <summary>
        /// The total number of requested reads.
        /// </summary>
        public long TotalReadsIn { get; internal set; }

        /// <summary>
        /// The total number of reads passed on by the cache.
        /// </summary>
        public long TotalReadsOut { get; internal set; }

        /// <summary>
        /// The number of times a read request was serviced (in part or whole) from the cache.
        /// </summary>
        public long ReadCacheHits { get; internal set; }

        /// <summary>
        /// The number of time a read request was serviced (in part or whole) from the wrapped stream.
        /// </summary>
        public long ReadCacheMisses { get; internal set; }

        /// <summary>
        /// The number of requested unaligned writes.
        /// </summary>
        /// <remarks>Unaligned writes are writes where the write doesn't start on a multiple of
        /// the block size.</remarks>
        public long UnalignedWritesIn { get; internal set; }

        /// <summary>
        /// The total number of requested writes.
        /// </summary>
        public long TotalWritesIn { get; internal set; }

        /// <summary>
        /// The number of free blocks in the read cache.
        /// </summary>
        public int FreeReadBlocks { get; internal set; }
    }

    /// <summary>
    /// A stream implementing a block-oriented read cache.
    /// </summary>
    public sealed class BlockCacheStream : SparseStream
    {
        private SparseStream _wrappedStream;
        private Ownership _ownWrapped;
        private BlockCacheSettings _settings;
        private BlockCacheStatistics _stats;

        private long _position;
        private bool _atEof;

        private Dictionary<long, CacheBlock> _blocks;
        private LinkedList<CacheBlock> _lru;
        private List<CacheBlock> _freeBlocks;
        private int _blocksCreated;
        private int _totalBlocks;
        private byte[] _readBuffer;
        private int _blocksInReadBuffer;

        /// <summary>
        /// Creates a new instance, with default settings.
        /// </summary>
        /// <param name="toWrap">The stream to wrap</param>
        /// <param name="ownership">Whether to assume ownership of <c>toWrap</c></param>
        public BlockCacheStream(SparseStream toWrap, Ownership ownership)
            : this(toWrap, ownership, new BlockCacheSettings())
        {
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="toWrap">The stream to wrap</param>
        /// <param name="ownership">Whether to assume ownership of <c>toWrap</c></param>
        /// <param name="settings">The cache settings</param>
        public BlockCacheStream(SparseStream toWrap, Ownership ownership, BlockCacheSettings settings)
        {
            if (!toWrap.CanRead)
            {
                throw new ArgumentException("The wrapped stream does not support reading", "toWrap");
            }
            if (!toWrap.CanSeek)
            {
                throw new ArgumentException("The wrapped stream does not support seeking", "toWrap");
            }


            _wrappedStream = toWrap;
            _ownWrapped = ownership;
            _settings = new BlockCacheSettings(settings);

            if (_settings.OptimumReadSize % _settings.BlockSize != 0)
            {
                throw new ArgumentException("Invalid settings, OptimumReadSize must be a multiple of BlockSize", "settings");
            }
            _readBuffer = new byte[_settings.OptimumReadSize];
            _blocksInReadBuffer = _settings.OptimumReadSize / _settings.BlockSize;

            _totalBlocks = (int)(_settings.ReadCacheSize / _settings.BlockSize);

            _stats = new BlockCacheStatistics();
            _stats.FreeReadBlocks = _totalBlocks;

            _blocks = new Dictionary<long, CacheBlock>();
            _lru = new LinkedList<CacheBlock>();
            _freeBlocks = new List<CacheBlock>(_totalBlocks);
        }

        /// <summary>
        /// Disposes of this instance, freeing up associated resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> if invoked from <c>Dispose</c>, else <c>false</c>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_wrappedStream != null && _ownWrapped == Ownership.Dispose)
                {
                    _wrappedStream.Dispose();
                }
                _wrappedStream = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Gets the parts of the stream that are stored.
        /// </summary>
        /// <remarks>This may be an empty enumeration if all bytes are zero.</remarks>
        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckDisposed();
                return _wrappedStream.Extents;
            }
        }

        /// <summary>
        /// Gets the parts of a stream that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest</param>
        /// <param name="count">The number of bytes of interest</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes</returns>
        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            CheckDisposed();
            return _wrappedStream.GetExtentsInRange(start, count);
        }

        /// <summary>
        /// Gets an indication as to whether the stream can be read.
        /// </summary>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// Gets an indication as to whether the stream position can be changed.
        /// </summary>
        public override bool CanSeek
        {
            get { return true; }
        }

        /// <summary>
        /// Gets an indication as to whether the stream can be written to.
        /// </summary>
        public override bool CanWrite
        {
            get { return _wrappedStream.CanWrite; }
        }

        /// <summary>
        /// Flushes the stream.
        /// </summary>
        public override void Flush()
        {
            CheckDisposed();
            _wrappedStream.Flush();
        }

        /// <summary>
        /// Gets the length of the stream.
        /// </summary>
        public override long Length
        {
            get
            {
                CheckDisposed();
                return _wrappedStream.Length;
            }
        }

        /// <summary>
        /// Gets and sets the current stream position.
        /// </summary>
        public override long Position
        {
            get
            {
                CheckDisposed();
                return _position;
            }
            set
            {
                CheckDisposed();
                _position = value;
            }
        }

        /// <summary>
        /// Reads data from the stream.
        /// </summary>
        /// <param name="buffer">The buffer to fill</param>
        /// <param name="offset">The buffer offset to start from</param>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The number of bytes read</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            long readStartPos = _position;

            if (_position >= Length)
            {
                if (_atEof)
                {
                    throw new IOException("Attempt to read beyond end of stream");
                }
                else
                {
                    _atEof = true;
                    return 0;
                }
            }

            _stats.TotalReadsIn++;

            if (count > _settings.LargeReadSize)
            {
                _stats.LargeReadsIn++;
                _stats.TotalReadsOut++;
                _wrappedStream.Position = _position;
                int numRead = _wrappedStream.Read(buffer, offset, count);
                _position = _wrappedStream.Position;

                if (_position >= Length)
                {
                    _atEof = true;
                }

                return numRead;
            }

            int totalBytesRead = 0;
            bool servicedFromCache = false;
            bool servicedOutsideCache = false;
            int blockSize = _settings.BlockSize;

            long firstBlock = _position / blockSize;
            int offsetInNextBlock = (int)(_position % blockSize);
            long endBlock = Utilities.Ceil(Math.Min(_position + count, Length), blockSize);
            int numBlocks = (int)(endBlock - firstBlock);

            if (offsetInNextBlock != 0)
            {
                _stats.UnalignedReadsIn++;
            }

            int blocksRead = 0;
            while (blocksRead < numBlocks)
            {
                // Read from the cache as much as possible
                while (blocksRead < numBlocks && _blocks.ContainsKey(firstBlock + blocksRead))
                {
                    CacheBlock block = _blocks[firstBlock + blocksRead];
                    int bytesToRead = Math.Min(count - totalBytesRead, block.Data.Length - offsetInNextBlock);

                    Array.Copy(block.Data, offsetInNextBlock, buffer, offset + totalBytesRead, bytesToRead);
                    offsetInNextBlock = 0;
                    totalBytesRead += bytesToRead;
                    _position += bytesToRead;
                    blocksRead++;

                    servicedFromCache = true;
                }

                // Now handle a sequence of (one or more) blocks that are not cached
                if (blocksRead < numBlocks && !_blocks.ContainsKey(firstBlock + blocksRead))
                {
                    servicedOutsideCache = true;

                    // Figure out how many blocks to read from the wrapped stream
                    int blocksToRead = 0;
                    while (blocksRead + blocksToRead < numBlocks
                        && blocksToRead < _blocksInReadBuffer
                        && !_blocks.ContainsKey(firstBlock + blocksRead + blocksToRead))
                    {
                        ++blocksToRead;
                    }

                    // Allow for the end of the stream not being block-aligned
                    long readPosition = (firstBlock + blocksRead) * (long)blockSize;
                    int bytesToRead = (int)Math.Min(blocksToRead * blockSize, Length - readPosition);

                    // Do the read
                    _stats.TotalReadsOut++;
                    _wrappedStream.Position = readPosition;
                    int bytesRead = Utilities.ReadFully(_wrappedStream, _readBuffer, 0, bytesToRead);
                    if (bytesRead != bytesToRead)
                    {
                        throw new IOException("Short read before end of stream");
                    }

                    // Cache the read blocks
                    for(int i = 0; i < blocksToRead; ++i)
                    {
                        int copyBytes = Math.Min(blockSize, bytesToRead - i * blockSize);
                        CacheBlock block = GetFreeBlock();
                        block.Block = firstBlock + blocksRead + i;
                        Array.Copy(_readBuffer, i * blockSize, block.Data, 0, copyBytes);

                        if (copyBytes < blockSize)
                        {
                            Array.Clear(_readBuffer, copyBytes, blockSize - copyBytes);
                        }

                        StoreBlock(block);
                    }
                    blocksRead += blocksToRead;

                    // Propogate the data onto the caller
                    int bytesToCopy = Math.Min(count - totalBytesRead, bytesRead - offsetInNextBlock);
                    Array.Copy(_readBuffer, offsetInNextBlock, buffer, offset + totalBytesRead, bytesToCopy);
                    totalBytesRead += bytesToCopy;
                    _position += bytesToCopy;
                    offsetInNextBlock = 0;
                }
            }

            if (_position >= Length)
            {
                _atEof = true;
            }

            if (servicedFromCache)
            {
                _stats.ReadCacheHits++;
            }
            if (servicedOutsideCache)
            {
                _stats.ReadCacheMisses++;
            }

            return totalBytesRead;
        }

        /// <summary>
        /// Moves the stream position.
        /// </summary>
        /// <param name="offset">The origin-relative location</param>
        /// <param name="origin">The base location</param>
        /// <returns>The new absolute stream position</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            long effectiveOffset = offset;
            if (origin == SeekOrigin.Current)
            {
                effectiveOffset += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                effectiveOffset += Length;
            }

            _atEof = false;

            if (effectiveOffset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            else
            {
                _position = effectiveOffset;
                return _position;
            }
        }

        /// <summary>
        /// Sets the length of the stream.
        /// </summary>
        /// <param name="value">The new length</param>
        public override void SetLength(long value)
        {
            CheckDisposed();
            _wrappedStream.SetLength(value);
        }

        /// <summary>
        /// Writes data to the stream at the current location.
        /// </summary>
        /// <param name="buffer">The data to write</param>
        /// <param name="offset">The first byte to write from buffer</param>
        /// <param name="count">The number of bytes to write</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            _stats.TotalWritesIn++;

            long startPos = _position;

            int blockSize = _settings.BlockSize;
            long firstBlock = _position / blockSize;
            long endBlock = Utilities.Ceil(Math.Min(_position + count, Length), blockSize);
            int numBlocks = (int)(endBlock - firstBlock);

            try
            {
                _wrappedStream.Position = _position;
                _wrappedStream.Write(buffer, offset, count);
            }
            catch
            {
                InvalidateBlocks(firstBlock, numBlocks);
                throw;
            }


            int offsetInNextBlock = (int)(_position % blockSize);

            if (offsetInNextBlock != 0)
            {
                _stats.UnalignedWritesIn++;
            }


            // For each block touched, if it's cached, update it
            int bytesProcessed = 0;
            for (int i = 0; i < numBlocks; ++i)
            {
                int bufferPos = offset + bytesProcessed;
                int bytesThisBlock = Math.Min(count - bufferPos, blockSize - offsetInNextBlock);

                if (_blocks.ContainsKey(firstBlock + i))
                {
                    CacheBlock block = _blocks[firstBlock + i];
                    Array.Copy(buffer, bufferPos, block.Data, offsetInNextBlock, bytesThisBlock);
                }

                offsetInNextBlock = 0;
                bytesProcessed += bytesThisBlock;
            }

            _position += count;
        }

        /// <summary>
        /// Gets the performance statistics for this instance.
        /// </summary>
        public BlockCacheStatistics Statistics
        {
            get { return _stats; }
        }

        private void CheckDisposed()
        {
            if (_wrappedStream == null)
            {
                throw new ObjectDisposedException("BlockCacheStream");
            }
        }

        private void StoreBlock(CacheBlock block)
        {
            _blocks[block.Block] = block;
            _lru.AddFirst(block);
        }

        private CacheBlock GetFreeBlock()
        {
            if (_freeBlocks.Count > 0)
            {
                int idx = _freeBlocks.Count - 1;
                CacheBlock block = _freeBlocks[idx];
                _freeBlocks.RemoveAt(idx);
                _stats.FreeReadBlocks--;
                return block;
            }
            else if (_blocksCreated < _totalBlocks)
            {
                _blocksCreated++;
                _stats.FreeReadBlocks--;
                return new CacheBlock(_settings.BlockSize);
            }
            else
            {
                CacheBlock block = _lru.Last.Value;
                _lru.RemoveLast();
                _blocks.Remove(block.Block);
                return block;
            }
        }

        private void InvalidateBlocks(long firstBlock, int numBlocks)
        {
            for (long i = firstBlock; i < (firstBlock + numBlocks); ++i)
            {
                if (_blocks.ContainsKey(i))
                {
                    CacheBlock block = _blocks[i];
                    _blocks.Remove(i);
                    _lru.Remove(block);
                    _freeBlocks.Add(block);
                    _stats.FreeReadBlocks++;
                }
            }
        }


        private sealed class CacheBlock : IEquatable<CacheBlock>
        {
            public long Block { get; set; }
            public byte[] Data { get; private set; }

            public CacheBlock(int size)
            {
                Data = new byte[size];
            }

            #region IEquatable<CacheBlock> Members

            public bool Equals(CacheBlock other)
            {
                return Block == other.Block;
            }

            #endregion
        }
    }
}

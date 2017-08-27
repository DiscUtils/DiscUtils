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
using System.IO;

namespace DiscUtils.Streams
{
    /// <summary>
    /// A stream implementing a block-oriented read cache.
    /// </summary>
    public sealed class BlockCacheStream : SparseStream
    {
        private bool _atEof;
        private readonly int _blocksInReadBuffer;

        private readonly BlockCache<Block> _cache;
        private readonly Ownership _ownWrapped;

        private long _position;
        private readonly byte[] _readBuffer;
        private readonly BlockCacheSettings _settings;
        private readonly BlockCacheStatistics _stats;
        private SparseStream _wrappedStream;

        /// <summary>
        /// Initializes a new instance of the BlockCacheStream class.
        /// </summary>
        /// <param name="toWrap">The stream to wrap.</param>
        /// <param name="ownership">Whether to assume ownership of <c>toWrap</c>.</param>
        public BlockCacheStream(SparseStream toWrap, Ownership ownership)
            : this(toWrap, ownership, new BlockCacheSettings()) {}

        /// <summary>
        /// Initializes a new instance of the BlockCacheStream class.
        /// </summary>
        /// <param name="toWrap">The stream to wrap.</param>
        /// <param name="ownership">Whether to assume ownership of <c>toWrap</c>.</param>
        /// <param name="settings">The cache settings.</param>
        public BlockCacheStream(SparseStream toWrap, Ownership ownership, BlockCacheSettings settings)
        {
            if (!toWrap.CanRead)
            {
                throw new ArgumentException("The wrapped stream does not support reading", nameof(toWrap));
            }

            if (!toWrap.CanSeek)
            {
                throw new ArgumentException("The wrapped stream does not support seeking", nameof(toWrap));
            }

            _wrappedStream = toWrap;
            _ownWrapped = ownership;
            _settings = new BlockCacheSettings(settings);

            if (_settings.OptimumReadSize % _settings.BlockSize != 0)
            {
                throw new ArgumentException("Invalid settings, OptimumReadSize must be a multiple of BlockSize",
                    nameof(settings));
            }

            _readBuffer = new byte[_settings.OptimumReadSize];
            _blocksInReadBuffer = _settings.OptimumReadSize / _settings.BlockSize;

            int totalBlocks = (int)(_settings.ReadCacheSize / _settings.BlockSize);

            _cache = new BlockCache<Block>(_settings.BlockSize, totalBlocks);
            _stats = new BlockCacheStatistics();
            _stats.FreeReadBlocks = totalBlocks;
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
        /// Gets the performance statistics for this instance.
        /// </summary>
        public BlockCacheStatistics Statistics
        {
            get
            {
                _stats.FreeReadBlocks = _cache.FreeBlockCount;
                return _stats;
            }
        }

        /// <summary>
        /// Gets the parts of a stream that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest.</param>
        /// <param name="count">The number of bytes of interest.</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            CheckDisposed();
            return _wrappedStream.GetExtentsInRange(start, count);
        }

        /// <summary>
        /// Reads data from the stream.
        /// </summary>
        /// <param name="buffer">The buffer to fill.</param>
        /// <param name="offset">The buffer offset to start from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The number of bytes read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (_position >= Length)
            {
                if (_atEof)
                {
                    throw new IOException("Attempt to read beyond end of stream");
                }
                _atEof = true;
                return 0;
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
            long endBlock = MathUtilities.Ceil(Math.Min(_position + count, Length), blockSize);
            int numBlocks = (int)(endBlock - firstBlock);

            if (offsetInNextBlock != 0)
            {
                _stats.UnalignedReadsIn++;
            }

            int blocksRead = 0;
            while (blocksRead < numBlocks)
            {
                Block block;

                // Read from the cache as much as possible
                while (blocksRead < numBlocks && _cache.TryGetBlock(firstBlock + blocksRead, out block))
                {
                    int bytesToRead = Math.Min(count - totalBytesRead, block.Available - offsetInNextBlock);

                    Array.Copy(block.Data, offsetInNextBlock, buffer, offset + totalBytesRead, bytesToRead);
                    offsetInNextBlock = 0;
                    totalBytesRead += bytesToRead;
                    _position += bytesToRead;
                    blocksRead++;

                    servicedFromCache = true;
                }

                // Now handle a sequence of (one or more) blocks that are not cached
                if (blocksRead < numBlocks && !_cache.ContainsBlock(firstBlock + blocksRead))
                {
                    servicedOutsideCache = true;

                    // Figure out how many blocks to read from the wrapped stream
                    int blocksToRead = 0;
                    while (blocksRead + blocksToRead < numBlocks
                           && blocksToRead < _blocksInReadBuffer
                           && !_cache.ContainsBlock(firstBlock + blocksRead + blocksToRead))
                    {
                        ++blocksToRead;
                    }

                    // Allow for the end of the stream not being block-aligned
                    long readPosition = (firstBlock + blocksRead) * blockSize;
                    int bytesRead = (int)Math.Min(blocksToRead * (long)blockSize, Length - readPosition);

                    // Do the read
                    _stats.TotalReadsOut++;
                    _wrappedStream.Position = readPosition;
                    StreamUtilities.ReadExact(_wrappedStream, _readBuffer, 0, bytesRead);

                    // Cache the read blocks
                    for (int i = 0; i < blocksToRead; ++i)
                    {
                        int copyBytes = Math.Min(blockSize, bytesRead - i * blockSize);
                        block = _cache.GetBlock(firstBlock + blocksRead + i);
                        Array.Copy(_readBuffer, i * blockSize, block.Data, 0, copyBytes);
                        block.Available = copyBytes;

                        if (copyBytes < blockSize)
                        {
                            Array.Clear(_readBuffer, i * blockSize + copyBytes, blockSize - copyBytes);
                        }
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

            if (_position >= Length && totalBytesRead == 0)
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
        /// Flushes the stream.
        /// </summary>
        public override void Flush()
        {
            CheckDisposed();
            _wrappedStream.Flush();
        }

        /// <summary>
        /// Moves the stream position.
        /// </summary>
        /// <param name="offset">The origin-relative location.</param>
        /// <param name="origin">The base location.</param>
        /// <returns>The new absolute stream position.</returns>
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
            _position = effectiveOffset;
            return _position;
        }

        /// <summary>
        /// Sets the length of the stream.
        /// </summary>
        /// <param name="value">The new length.</param>
        public override void SetLength(long value)
        {
            CheckDisposed();
            _wrappedStream.SetLength(value);
        }

        /// <summary>
        /// Writes data to the stream at the current location.
        /// </summary>
        /// <param name="buffer">The data to write.</param>
        /// <param name="offset">The first byte to write from buffer.</param>
        /// <param name="count">The number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            _stats.TotalWritesIn++;

            int blockSize = _settings.BlockSize;
            long firstBlock = _position / blockSize;
            long endBlock = MathUtilities.Ceil(Math.Min(_position + count, Length), blockSize);
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
                int bytesThisBlock = Math.Min(count - bytesProcessed, blockSize - offsetInNextBlock);

                Block block;
                if (_cache.TryGetBlock(firstBlock + i, out block))
                {
                    Array.Copy(buffer, bufferPos, block.Data, offsetInNextBlock, bytesThisBlock);
                    block.Available = Math.Max(block.Available, offsetInNextBlock + bytesThisBlock);
                }

                offsetInNextBlock = 0;
                bytesProcessed += bytesThisBlock;
            }

            _position += count;
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

        private void CheckDisposed()
        {
            if (_wrappedStream == null)
            {
                throw new ObjectDisposedException("BlockCacheStream");
            }
        }

        private void InvalidateBlocks(long firstBlock, int numBlocks)
        {
            for (long i = firstBlock; i < firstBlock + numBlocks; ++i)
            {
                _cache.ReleaseBlock(i);
            }
        }
    }
}
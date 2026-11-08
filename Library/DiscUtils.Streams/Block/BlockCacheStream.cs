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
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

/// <summary>
/// A stream implementing a block-oriented read cache.
/// </summary>
public sealed class BlockCacheStream : SparseStream
{
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
        : this(toWrap, ownership, new BlockCacheSettings()) { }

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

        _readBuffer = StreamUtilities.GetUninitializedArray<byte>(_settings.OptimumReadSize);
        _blocksInReadBuffer = _settings.OptimumReadSize / _settings.BlockSize;

        var totalBlocks = (int)(_settings.ReadCacheSize / _settings.BlockSize);

        _cache = new BlockCache<Block>(_settings.BlockSize, totalBlocks);
        _stats = new BlockCacheStatistics
        {
            FreeReadBlocks = totalBlocks
        };
    }

    /// <summary>
    /// Gets an indication as to whether the stream can be read.
    /// </summary>
    public override bool CanRead => true;

    /// <summary>
    /// Gets an indication as to whether the stream position can be changed.
    /// </summary>
    public override bool CanSeek => true;

    /// <summary>
    /// Gets an indication as to whether the stream can be written to.
    /// </summary>
    public override bool CanWrite => _wrappedStream is { CanWrite: true };

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

        if (_position > Length)
        {
            throw new IOException("Attempt to read beyond end of stream");
        }

        if (_position == Length)
        {
            return 0;
        }

        _stats.TotalReadsIn++;

        if (count > _settings.LargeReadSize)
        {
            _stats.LargeReadsIn++;
            _stats.TotalReadsOut++;
            _wrappedStream.Position = _position;
            var numRead = _wrappedStream.Read(buffer, offset, count);
            _position = _wrappedStream.Position;

            return numRead;
        }

        var totalBytesRead = 0;
        var servicedFromCache = false;
        var servicedOutsideCache = false;
        var blockSize = _settings.BlockSize;

        var firstBlock = _position / blockSize;
        var offsetInNextBlock = (int)(_position % blockSize);
        var endBlock = MathUtilities.Ceil(Math.Min(_position + count, Length), blockSize);
        var numBlocks = (int)(endBlock - firstBlock);

        if (offsetInNextBlock != 0)
        {
            _stats.UnalignedReadsIn++;
        }

        var blocksRead = 0;
        while (blocksRead < numBlocks)
        {
            Block? block;

            // Read from the cache as much as possible
            while (blocksRead < numBlocks && _cache.TryGetBlock(firstBlock + blocksRead, out block))
            {
                var bytesToRead = Math.Min(count - totalBytesRead, block.Available - offsetInNextBlock);

                System.Buffer.BlockCopy(block.Data, offsetInNextBlock, buffer, offset + totalBytesRead, bytesToRead);
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
                var blocksToRead = 0;
                while (blocksRead + blocksToRead < numBlocks
                       && blocksToRead < _blocksInReadBuffer
                       && !_cache.ContainsBlock(firstBlock + blocksRead + blocksToRead))
                {
                    ++blocksToRead;
                }

                // Allow for the end of the stream not being block-aligned
                var readPosition = (firstBlock + blocksRead) * blockSize;
                var bytesRead = (int)Math.Min(blocksToRead * (long)blockSize, Length - readPosition);

                // Do the read
                _stats.TotalReadsOut++;
                _wrappedStream.Position = readPosition;
                _wrappedStream.ReadExactly(_readBuffer, 0, bytesRead);

                // Cache the read blocks
                for (var i = 0; i < blocksToRead; ++i)
                {
                    var copyBytes = Math.Min(blockSize, bytesRead - i * blockSize);
                    block = _cache.GetBlock(firstBlock + blocksRead + i);
                    System.Buffer.BlockCopy(_readBuffer, i * blockSize, block.Data, 0, copyBytes);
                    block.Available = copyBytes;

                    if (copyBytes < blockSize)
                    {
                        Array.Clear(_readBuffer, i * blockSize + copyBytes, blockSize - copyBytes);
                    }
                }

                blocksRead += blocksToRead;

                // Propogate the data onto the caller
                var bytesToCopy = Math.Min(count - totalBytesRead, bytesRead - offsetInNextBlock);
                System.Buffer.BlockCopy(_readBuffer, offsetInNextBlock, buffer, offset + totalBytesRead, bytesToCopy);
                totalBytesRead += bytesToCopy;
                _position += bytesToCopy;
                offsetInNextBlock = 0;
            }
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
    /// Reads data from the stream.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns>The number of bytes read.</returns>
    public override int Read(Span<byte> buffer)
    {
        CheckDisposed();

        if (_position > Length)
        {
            throw new IOException("Attempt to read beyond end of stream");
        }

        if (_position == Length)
        {
            return 0;
        }

        _stats.TotalReadsIn++;

        if (buffer.Length > _settings.LargeReadSize)
        {
            _stats.LargeReadsIn++;
            _stats.TotalReadsOut++;
            _wrappedStream.Position = _position;
            var numRead = _wrappedStream.Read(buffer);
            _position = _wrappedStream.Position;

            return numRead;
        }

        var totalBytesRead = 0;
        var servicedFromCache = false;
        var servicedOutsideCache = false;
        var blockSize = _settings.BlockSize;

        var firstBlock = _position / blockSize;
        var offsetInNextBlock = (int)(_position % blockSize);
        var endBlock = MathUtilities.Ceil(Math.Min(_position + buffer.Length, Length), blockSize);
        var numBlocks = (int)(endBlock - firstBlock);

        if (offsetInNextBlock != 0)
        {
            _stats.UnalignedReadsIn++;
        }

        var blocksRead = 0;
        while (blocksRead < numBlocks)
        {
            Block? block;

            // Read from the cache as much as possible
            while (blocksRead < numBlocks && _cache.TryGetBlock(firstBlock + blocksRead, out block))
            {
                var bytesToRead = Math.Min(buffer.Length - totalBytesRead, block.Available - offsetInNextBlock);

                block.Data.AsSpan(offsetInNextBlock, bytesToRead).CopyTo(buffer[totalBytesRead..]);
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
                var blocksToRead = 0;
                while (blocksRead + blocksToRead < numBlocks
                       && blocksToRead < _blocksInReadBuffer
                       && !_cache.ContainsBlock(firstBlock + blocksRead + blocksToRead))
                {
                    ++blocksToRead;
                }

                // Allow for the end of the stream not being block-aligned
                var readPosition = (firstBlock + blocksRead) * blockSize;
                var bytesRead = (int)Math.Min(blocksToRead * (long)blockSize, Length - readPosition);

                // Do the read
                _stats.TotalReadsOut++;
                _wrappedStream.Position = readPosition;
                _wrappedStream.ReadExactly(_readBuffer, 0, bytesRead);

                // Cache the read blocks
                for (var i = 0; i < blocksToRead; ++i)
                {
                    var copyBytes = Math.Min(blockSize, bytesRead - i * blockSize);
                    block = _cache.GetBlock(firstBlock + blocksRead + i);
                    System.Buffer.BlockCopy(_readBuffer, i * blockSize, block.Data, 0, copyBytes);
                    block.Available = copyBytes;

                    if (copyBytes < blockSize)
                    {
                        Array.Clear(_readBuffer, i * blockSize + copyBytes, blockSize - copyBytes);
                    }
                }

                blocksRead += blocksToRead;

                // Propogate the data onto the caller
                var bytesToCopy = Math.Min(buffer.Length - totalBytesRead, bytesRead - offsetInNextBlock);
                _readBuffer.AsSpan(offsetInNextBlock, bytesToCopy).CopyTo(buffer[totalBytesRead..]);
                totalBytesRead += bytesToCopy;
                _position += bytesToCopy;
                offsetInNextBlock = 0;
            }
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
    /// Reads data from the stream.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of bytes read.</returns>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckDisposed();

        if (_position > Length)
        {
            throw new IOException("Attempt to read beyond end of stream");
        }

        if (_position == Length)
        {
            return 0;
        }

        _stats.TotalReadsIn++;

        if (buffer.Length > _settings.LargeReadSize)
        {
            _stats.LargeReadsIn++;
            _stats.TotalReadsOut++;
            _wrappedStream.Position = _position;
            var numRead = await _wrappedStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position = _wrappedStream.Position;

            return numRead;
        }

        var totalBytesRead = 0;
        var servicedFromCache = false;
        var servicedOutsideCache = false;
        var blockSize = _settings.BlockSize;

        var firstBlock = _position / blockSize;
        var offsetInNextBlock = (int)(_position % blockSize);
        var endBlock = MathUtilities.Ceil(Math.Min(_position + buffer.Length, Length), blockSize);
        var numBlocks = (int)(endBlock - firstBlock);

        if (offsetInNextBlock != 0)
        {
            _stats.UnalignedReadsIn++;
        }

        var blocksRead = 0;
        while (blocksRead < numBlocks)
        {
            Block? block;

            // Read from the cache as much as possible
            while (blocksRead < numBlocks && _cache.TryGetBlock(firstBlock + blocksRead, out block))
            {
                var bytesToRead = Math.Min(buffer.Length - totalBytesRead, block.Available - offsetInNextBlock);

                block.Data.AsMemory(offsetInNextBlock, bytesToRead).CopyTo(buffer[totalBytesRead..]);
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
                var blocksToRead = 0;
                while (blocksRead + blocksToRead < numBlocks
                       && blocksToRead < _blocksInReadBuffer
                       && !_cache.ContainsBlock(firstBlock + blocksRead + blocksToRead))
                {
                    ++blocksToRead;
                }

                // Allow for the end of the stream not being block-aligned
                var readPosition = (firstBlock + blocksRead) * blockSize;
                var bytesRead = (int)Math.Min(blocksToRead * (long)blockSize, Length - readPosition);

                // Do the read
                _stats.TotalReadsOut++;
                _wrappedStream.Position = readPosition;
                await _wrappedStream.ReadExactlyAsync(_readBuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

                // Cache the read blocks
                for (var i = 0; i < blocksToRead; ++i)
                {
                    var copyBytes = Math.Min(blockSize, bytesRead - i * blockSize);
                    block = _cache.GetBlock(firstBlock + blocksRead + i);
                    System.Buffer.BlockCopy(_readBuffer, i * blockSize, block.Data, 0, copyBytes);
                    block.Available = copyBytes;

                    if (copyBytes < blockSize)
                    {
                        Array.Clear(_readBuffer, i * blockSize + copyBytes, blockSize - copyBytes);
                    }
                }

                blocksRead += blocksToRead;

                // Propogate the data onto the caller
                var bytesToCopy = Math.Min(buffer.Length - totalBytesRead, bytesRead - offsetInNextBlock);
                _readBuffer.AsMemory(offsetInNextBlock, bytesToCopy).CopyTo(buffer[totalBytesRead..]);
                totalBytesRead += bytesToCopy;
                _position += bytesToCopy;
                offsetInNextBlock = 0;
            }
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

        if (CanWrite)
        {
            _wrappedStream.Flush();
        }
    }

    public override void Flush(bool flushToDisk)
    {
        CheckDisposed();

        if (CanWrite)
        {
            _wrappedStream.Flush(flushToDisk);
        }
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

        var effectiveOffset = offset;
        if (origin == SeekOrigin.Current)
        {
            effectiveOffset += _position;
        }
        else if (origin == SeekOrigin.End)
        {
            effectiveOffset += Length;
        }

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

        var blockSize = _settings.BlockSize;
        var firstBlock = _position / blockSize;
        var endBlock = MathUtilities.Ceil(Math.Min(_position + count, Length), blockSize);
        var numBlocks = (int)(endBlock - firstBlock);

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

        var offsetInNextBlock = (int)(_position % blockSize);
        if (offsetInNextBlock != 0)
        {
            _stats.UnalignedWritesIn++;
        }

        // For each block touched, if it's cached, update it
        var bytesProcessed = 0;
        for (var i = 0; i < numBlocks; ++i)
        {
            var bufferPos = offset + bytesProcessed;
            var bytesThisBlock = Math.Min(count - bytesProcessed, blockSize - offsetInNextBlock);

            if (_cache.TryGetBlock(firstBlock + i, out var block))
            {
                System.Buffer.BlockCopy(buffer, bufferPos, block.Data, offsetInNextBlock, bytesThisBlock);
                block.Available = Math.Max(block.Available, offsetInNextBlock + bytesThisBlock);
            }

            offsetInNextBlock = 0;
            bytesProcessed += bytesThisBlock;
        }

        _position += count;
    }

    /// <summary>
    /// Writes data to the stream at the current location.
    /// </summary>
    /// <param name="buffer">The data to write.</param>
    /// <param name="cancellationToken"></param>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckDisposed();

        _stats.TotalWritesIn++;

        var blockSize = _settings.BlockSize;
        var firstBlock = _position / blockSize;
        var endBlock = MathUtilities.Ceil(Math.Min(_position + buffer.Length, Length), blockSize);
        var numBlocks = (int)(endBlock - firstBlock);

        try
        {
            _wrappedStream.Position = _position;
            await _wrappedStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            InvalidateBlocks(firstBlock, numBlocks);
            throw;
        }

        var offsetInNextBlock = (int)(_position % blockSize);
        if (offsetInNextBlock != 0)
        {
            _stats.UnalignedWritesIn++;
        }

        // For each block touched, if it's cached, update it
        var bytesProcessed = 0;
        for (var i = 0; i < numBlocks; ++i)
        {
            var bufferPos = bytesProcessed;
            var bytesThisBlock = Math.Min(buffer.Length - bytesProcessed, blockSize - offsetInNextBlock);

            if (_cache.TryGetBlock(firstBlock + i, out var block))
            {
                buffer.Slice(bufferPos, bytesThisBlock).CopyTo(block.Data.AsMemory(offsetInNextBlock, bytesThisBlock));
                block.Available = Math.Max(block.Available, offsetInNextBlock + bytesThisBlock);
            }

            offsetInNextBlock = 0;
            bytesProcessed += bytesThisBlock;
        }

        _position += buffer.Length;
    }

    /// <summary>
    /// Writes data to the stream at the current location.
    /// </summary>
    /// <param name="buffer">The data to write.</param>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CheckDisposed();

        _stats.TotalWritesIn++;

        var blockSize = _settings.BlockSize;
        var firstBlock = _position / blockSize;
        var endBlock = MathUtilities.Ceil(Math.Min(_position + buffer.Length, Length), blockSize);
        var numBlocks = (int)(endBlock - firstBlock);

        try
        {
            _wrappedStream.Position = _position;
            _wrappedStream.Write(buffer);
        }
        catch
        {
            InvalidateBlocks(firstBlock, numBlocks);
            throw;
        }

        var offsetInNextBlock = (int)(_position % blockSize);
        if (offsetInNextBlock != 0)
        {
            _stats.UnalignedWritesIn++;
        }

        // For each block touched, if it's cached, update it
        var bytesProcessed = 0;
        for (var i = 0; i < numBlocks; ++i)
        {
            var bufferPos = bytesProcessed;
            var bytesThisBlock = Math.Min(buffer.Length - bytesProcessed, blockSize - offsetInNextBlock);

            if (_cache.TryGetBlock(firstBlock + i, out var block))
            {
                buffer.Slice(bufferPos, bytesThisBlock).CopyTo(block.Data.AsSpan(offsetInNextBlock, bytesThisBlock));
                block.Available = Math.Max(block.Available, offsetInNextBlock + bytesThisBlock);
            }

            offsetInNextBlock = 0;
            bytesProcessed += bytesThisBlock;
        }

        _position += buffer.Length;
    }

    /// <summary>
    /// Disposes of this instance, freeing up associated resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> if invoked from <c>Dispose</c>, else <c>false</c>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_wrappedStream != null)
            {
                if (_ownWrapped == Ownership.Dispose)
                {
                    _wrappedStream.Dispose();
                }
                else if (_wrappedStream.CanWrite)
                {
                    _wrappedStream.Flush();
                }
            }

            _wrappedStream = null!;
        }

        base.Dispose(disposing);
    }

    private void CheckDisposed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_wrappedStream is null, this);
#else
        if (_wrappedStream == null)
        {
            throw new ObjectDisposedException("BlockCacheStream");
        }
#endif
    }

    private void InvalidateBlocks(long firstBlock, int numBlocks)
    {
        for (var i = firstBlock; i < firstBlock + numBlocks; ++i)
        {
            _cache.ReleaseBlock(i);
        }
    }
}

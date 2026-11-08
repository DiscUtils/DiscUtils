//
// Copyright (c) 2008-2012, Kenneth Bell
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

using DiscUtils.Internal;
using DiscUtils.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Vhdx;

public sealed class ContentStream : MappedStream
{
    private bool _atEof;
    private readonly Stream _batStream;
    private readonly bool? _canWrite;

    private readonly ObjectCache<int, Chunk> _chunks;
    private readonly FileParameters _fileParameters;
    private readonly SparseStream _fileStream;
    private readonly FreeSpaceTable _freeSpaceTable;
    private readonly long _length;
    private readonly Metadata _metadata;
    private readonly Ownership _ownsParent;
    private SparseStream _parentStream;
    private long _position;

    internal ContentStream(SparseStream fileStream, bool? canWrite, Stream batStream, FreeSpaceTable freeSpaceTable,
                         Metadata metadata, long length, SparseStream parentStream, Ownership ownsParent)
    {
        _fileStream = fileStream;
        _canWrite = canWrite;
        _batStream = batStream;
        _freeSpaceTable = freeSpaceTable;
        _metadata = metadata;
        _fileParameters = _metadata.FileParameters;
        _length = length;
        _parentStream = parentStream;
        _ownsParent = ownsParent;

        _chunks = new ObjectCache<int, Chunk>();
    }

    public override bool CanRead => _parentStream is not null;

    public override bool CanSeek => _parentStream is not null;

    public override bool CanWrite => _parentStream is not null && (_canWrite ?? _fileStream.CanWrite);

    public override IEnumerable<StreamExtent> Extents
    {
        get
        {
            CheckDisposed();

            // For now, report the complete file contents
            return GetExtentsInRange(0, Length);
        }
    }

    public override long Length
    {
        get
        {
            CheckDisposed();
            return _length;
        }
    }

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
            _atEof = false;
            _position = value;
        }
    }

    public override void Flush()
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to flush to read-only VHDX");
        }

        _fileStream.Flush();
    }

    public override void Flush(bool flushToDisk)
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to flush to read-only VHDX");
        }

        _fileStream.Flush(flushToDisk);
    }

    public override IEnumerable<StreamExtent> MapContent(long start, long length)
    {
        CheckDisposed();

        throw new NotImplementedException();
    }

    public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
    {
        CheckDisposed();

        return
            StreamExtent.Intersect(
                StreamExtent.Union(GetExtentsRaw(start, count), _parentStream.GetExtentsInRange(start, count)),
                new StreamExtent(start, count));
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        CheckDisposed();

        if (_atEof || _position > _length)
        {
            _atEof = true;
            throw new IOException("Attempt to read beyond end of file");
        }

        if (_position == _length)
        {
            _atEof = true;
            return 0;
        }

        if (_position % _metadata.LogicalSectorSize != 0 || count % _metadata.LogicalSectorSize != 0)
        {
            throw new IOException("Unaligned read");
        }

        var totalToRead = (int)Math.Min(_length - _position, count);
        var totalRead = 0;

        while (totalRead < totalToRead)
        {
            var chunk = GetChunk(_position + totalRead, out _, out var blockIndex, out var sectorIndex);

            var blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
            var blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

            var blockStatus = chunk.GetBlockStatus(blockIndex);
            if (blockStatus == PayloadBlockStatus.FullyPresent)
            {
                _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                var read = _fileStream.ReadMaximum(buffer, offset + totalRead,
                    Math.Min(blockBytesRemaining, totalToRead - totalRead));

                totalRead += read;
            }
            else if (blockStatus == PayloadBlockStatus.PartiallyPresent)
            {
                var bitmap = chunk.GetBlockBitmap(blockIndex);

                var numSectors = bitmap.ContiguousBits(sectorIndex, out var present);
                var toRead = (int)Math.Min(numSectors * _metadata.LogicalSectorSize, totalToRead - totalRead);
                int read;

                if (present)
                {
                    _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                    read = _fileStream.ReadMaximum(buffer, offset + totalRead, toRead);
                }
                else
                {
                    _parentStream.Position = _position + totalRead;
                    read = _parentStream.ReadMaximum(buffer, offset + totalRead, toRead);
                }

                totalRead += read;
            }
            else if (blockStatus == PayloadBlockStatus.NotPresent)
            {
                _parentStream.Position = _position + totalRead;
                var read = _parentStream.ReadMaximum(buffer, offset + totalRead, Math.Min(blockBytesRemaining, totalToRead - totalRead));

                totalRead += read;
            }
            else
            {
                var zeroed = Math.Min(blockBytesRemaining, totalToRead - totalRead);
                Array.Clear(buffer, offset + totalRead, zeroed);
                totalRead += zeroed;
            }
        }

        _position += totalRead;
        return totalRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckDisposed();

        if (_atEof || _position > _length)
        {
            _atEof = true;
            throw new IOException("Attempt to read beyond end of file");
        }

        if (_position == _length)
        {
            _atEof = true;
            return 0;
        }

        if (_position % _metadata.LogicalSectorSize != 0 || buffer.Length % _metadata.LogicalSectorSize != 0)
        {
            throw new IOException("Unaligned read");
        }

        var totalToRead = (int)Math.Min(_length - _position, buffer.Length);
        var totalRead = 0;

        while (totalRead < totalToRead)
        {
            var chunk = GetChunk(_position + totalRead, out _, out var blockIndex, out var sectorIndex);

            var blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
            var blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

            var blockStatus = chunk.GetBlockStatus(blockIndex);
            if (blockStatus == PayloadBlockStatus.FullyPresent)
            {
                _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                var read = await _fileStream.ReadMaximumAsync(buffer.Slice(totalRead, Math.Min(blockBytesRemaining, totalToRead - totalRead)), cancellationToken).ConfigureAwait(false);

                totalRead += read;
            }
            else if (blockStatus == PayloadBlockStatus.PartiallyPresent)
            {
                var bitmap = await chunk.GetBlockBitmapAsync(blockIndex, cancellationToken).ConfigureAwait(false);

                var numSectors = bitmap.ContiguousBits(sectorIndex, out var present);
                var toRead = (int)Math.Min(numSectors * _metadata.LogicalSectorSize, totalToRead - totalRead);
                int read;

                if (present)
                {
                    _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                    read = await _fileStream.ReadMaximumAsync(buffer.Slice(totalRead, toRead), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _parentStream.Position = _position + totalRead;
                    read = await _parentStream.ReadMaximumAsync(buffer.Slice(totalRead, toRead), cancellationToken).ConfigureAwait(false);
                }

                totalRead += read;
            }
            else if (blockStatus == PayloadBlockStatus.NotPresent)
            {
                _parentStream.Position = _position + totalRead;
                var read = await _parentStream.ReadMaximumAsync(buffer.Slice(totalRead, Math.Min(blockBytesRemaining, totalToRead - totalRead)), cancellationToken).ConfigureAwait(false);

                totalRead += read;
            }
            else
            {
                var zeroed = Math.Min(blockBytesRemaining, totalToRead - totalRead);
                buffer.Span.Slice(totalRead, zeroed).Clear();
                totalRead += zeroed;
            }
        }

        _position += totalRead;
        return totalRead;
    }

    public override int Read(Span<byte> buffer)
    {
        CheckDisposed();

        if (_atEof || _position > _length)
        {
            _atEof = true;
            throw new IOException("Attempt to read beyond end of file");
        }

        if (_position == _length)
        {
            _atEof = true;
            return 0;
        }

        if (_position % _metadata.LogicalSectorSize != 0 || buffer.Length % _metadata.LogicalSectorSize != 0)
        {
            throw new IOException("Unaligned read");
        }

        var totalToRead = (int)Math.Min(_length - _position, buffer.Length);
        var totalRead = 0;

        while (totalRead < totalToRead)
        {
            var chunk = GetChunk(_position + totalRead, out _, out var blockIndex, out var sectorIndex);

            var blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
            var blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

            var blockStatus = chunk.GetBlockStatus(blockIndex);
            if (blockStatus == PayloadBlockStatus.FullyPresent)
            {
                _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                var read = _fileStream.ReadMaximum(buffer.Slice(totalRead, Math.Min(blockBytesRemaining, totalToRead - totalRead)));

                totalRead += read;
            }
            else if (blockStatus == PayloadBlockStatus.PartiallyPresent)
            {
                var bitmap = chunk.GetBlockBitmap(blockIndex);

                var numSectors = bitmap.ContiguousBits(sectorIndex, out var present);
                var toRead = (int)Math.Min(numSectors * _metadata.LogicalSectorSize, totalToRead - totalRead);
                int read;

                if (present)
                {
                    _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
                    read = _fileStream.ReadMaximum(buffer.Slice(totalRead, toRead));
                }
                else
                {
                    _parentStream.Position = _position + totalRead;
                    read = _parentStream.ReadMaximum(buffer.Slice(totalRead, toRead));
                }

                totalRead += read;
            }
            else if (blockStatus == PayloadBlockStatus.NotPresent)
            {
                _parentStream.Position = _position + totalRead;
                var read = _parentStream.ReadMaximum(buffer.Slice(totalRead, Math.Min(blockBytesRemaining, totalToRead - totalRead)));

                totalRead += read;
            }
            else
            {
                var zeroed = Math.Min(blockBytesRemaining, totalToRead - totalRead);
                buffer.Slice(totalRead, zeroed).Clear();
                totalRead += zeroed;
            }
        }

        _position += totalRead;
        return totalRead;
    }

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
            effectiveOffset += _length;
        }

        _atEof = false;

        if (effectiveOffset < 0)
        {
            throw new IOException("Attempt to move before beginning of disk");
        }

        _position = effectiveOffset;
        return _position;
    }

    public override void SetLength(long value)
    {
        CheckDisposed();

        if (Length != value)
        {
            throw new NotImplementedException("Cannot modify virtual length of vhdx image");
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to write to read-only VHDX");
        }

        if (_position % _metadata.LogicalSectorSize != 0 || count % _metadata.LogicalSectorSize != 0)
        {
            throw new IOException("Unaligned write");
        }

        var totalWritten = 0;

        while (totalWritten < count)
        {
            var chunk = GetChunk(_position + totalWritten, out _, out var blockIndex, out var sectorIndex);

            var blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
            var blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

            var blockStatus = chunk.GetBlockStatus(blockIndex);
            if (blockStatus is not PayloadBlockStatus.FullyPresent and not PayloadBlockStatus.PartiallyPresent)
            {
                blockStatus = chunk.AllocateSpaceForBlock(blockIndex);
            }

            var toWrite = Math.Min(blockBytesRemaining, count - totalWritten);
            _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
            _fileStream.Write(buffer, offset + totalWritten, toWrite);

            if (blockStatus == PayloadBlockStatus.PartiallyPresent)
            {
                var bitmap = chunk.GetBlockBitmap(blockIndex);
                var changed = bitmap.MarkBitsAllocated(sectorIndex, (int)(toWrite / _metadata.LogicalSectorSize));

                if (changed)
                {
                    chunk.WriteBlockBitmap(blockIndex);
                }
            }

            totalWritten += toWrite;
        }

        _position += totalWritten;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to write to read-only VHDX");
        }

        if (_position % _metadata.LogicalSectorSize != 0 || buffer.Length % _metadata.LogicalSectorSize != 0)
        {
            throw new IOException("Unaligned write");
        }

        var totalWritten = 0;

        while (totalWritten < buffer.Length)
        {
            var chunk = GetChunk(_position + totalWritten, out _, out var blockIndex, out var sectorIndex);

            var blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
            var blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

            var blockStatus = chunk.GetBlockStatus(blockIndex);
            if (blockStatus is not PayloadBlockStatus.FullyPresent and not PayloadBlockStatus.PartiallyPresent)
            {
                blockStatus = await chunk.AllocateSpaceForBlockAsync(blockIndex, cancellationToken).ConfigureAwait(false);
            }

            var toWrite = Math.Min(blockBytesRemaining, buffer.Length - totalWritten);
            _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
            await _fileStream.WriteAsync(buffer.Slice(totalWritten, toWrite), cancellationToken).ConfigureAwait(false);

            if (blockStatus == PayloadBlockStatus.PartiallyPresent)
            {
                var bitmap = await chunk.GetBlockBitmapAsync(blockIndex, cancellationToken).ConfigureAwait(false);
                var changed = bitmap.MarkBitsAllocated(sectorIndex, (int)(toWrite / _metadata.LogicalSectorSize));

                if (changed)
                {
                    await chunk.WriteBlockBitmapAsync(blockIndex, cancellationToken).ConfigureAwait(false);
                }
            }

            totalWritten += toWrite;
        }

        _position += totalWritten;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to write to read-only VHDX");
        }

        if (_position % _metadata.LogicalSectorSize != 0 || buffer.Length % _metadata.LogicalSectorSize != 0)
        {
            throw new IOException("Unaligned write");
        }

        var totalWritten = 0;

        while (totalWritten < buffer.Length)
        {
            var chunk = GetChunk(_position + totalWritten, out _, out var blockIndex, out var sectorIndex);

            var blockOffset = (int)(sectorIndex * _metadata.LogicalSectorSize);
            var blockBytesRemaining = (int)(_fileParameters.BlockSize - blockOffset);

            var blockStatus = chunk.GetBlockStatus(blockIndex);
            if (blockStatus is not PayloadBlockStatus.FullyPresent and not PayloadBlockStatus.PartiallyPresent)
            {
                blockStatus = chunk.AllocateSpaceForBlock(blockIndex);
            }

            var toWrite = Math.Min(blockBytesRemaining, buffer.Length - totalWritten);
            _fileStream.Position = chunk.GetBlockPosition(blockIndex) + blockOffset;
            _fileStream.Write(buffer.Slice(totalWritten, toWrite));

            if (blockStatus == PayloadBlockStatus.PartiallyPresent)
            {
                var bitmap = chunk.GetBlockBitmap(blockIndex);
                var changed = bitmap.MarkBitsAllocated(sectorIndex, (int)(toWrite / _metadata.LogicalSectorSize));

                if (changed)
                {
                    chunk.WriteBlockBitmap(blockIndex);
                }
            }

            totalWritten += toWrite;
        }

        _position += totalWritten;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (_parentStream != null)
                {
                    if (_ownsParent == Ownership.Dispose)
                    {
                        _parentStream.Dispose();
                    }

                    _parentStream = null;
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private IEnumerable<StreamExtent> GetExtentsRaw(long start, long count)
    {
        var chunkSize = (1L << 23) * _metadata.LogicalSectorSize;

        var pos = MathUtilities.RoundDown(start, chunkSize);

        while (pos < start + count)
        {
            var chunk = GetChunk(pos, out _, out _, out _);

            var thisChunkSize = Math.Min(chunkSize, start + count - pos);

            var chunkRatio = (int)(thisChunkSize / _metadata.FileParameters.BlockSize);

            for (var i = 0; i < chunkRatio; ++i)
            {
                switch (chunk.GetBlockStatus(i))
                {
                    case PayloadBlockStatus.NotPresent:
                    case PayloadBlockStatus.Undefined:
                    case PayloadBlockStatus.Unmapped:
                    case PayloadBlockStatus.Zero:
                        break;
                    default:
                        yield return
                            new StreamExtent(pos + i * _metadata.FileParameters.BlockSize,
                                _metadata.FileParameters.BlockSize);
                        break;
                }
            }

            pos += chunkSize;
        }
    }

    public long ChunkSize => (1L << 23) * _metadata.LogicalSectorSize;

    public Chunk GetChunk(long position, out int chunk, out int block, out int sector)
    {
        var chunkSize = (1L << 23) * _metadata.LogicalSectorSize;
        var chunkRatio = (int)(chunkSize / _metadata.FileParameters.BlockSize);

        chunk = (int)(position / chunkSize);
        var chunkOffset = position % chunkSize;

        block = (int)(chunkOffset / _fileParameters.BlockSize);
        var blockOffset = (int)(chunkOffset % _fileParameters.BlockSize);

        sector = (int)(blockOffset / _metadata.LogicalSectorSize);

        var result = _chunks[chunk];
        if (result == null)
        {
            result = new Chunk(_batStream, _fileStream, _freeSpaceTable, _fileParameters, chunk, chunkRatio);
            _chunks[chunk] = result;
        }

        return result;
    }

    private void CheckDisposed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_parentStream is null, this);
#else
        if (_parentStream is null)
        {
            throw new ObjectDisposedException(nameof(ContentStream), "Attempt to use closed stream");
        }
#endif
    }
}

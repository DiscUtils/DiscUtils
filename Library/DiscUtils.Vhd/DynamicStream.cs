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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;

namespace DiscUtils.Vhd;

public class DynamicStream : MappedStream
{
    private bool _atEof;
    private uint[] _blockAllocationTable;
    private readonly byte[][] _blockBitmaps;
    private readonly int _blockBitmapSize;
    private readonly DynamicHeader _dynamicHeader;
    private readonly Stream _fileStream;
    private byte[] _footerCache;
    private readonly long _length;
    private bool _newBlocksAllocated;
    private long _nextBlockStart;
    private readonly Ownership _ownsParentStream;
    private SparseStream _parentStream;

    private long _position;

    internal DynamicStream(Stream fileStream, DynamicHeader dynamicHeader, long length, SparseStream parentStream,
                         Ownership ownsParentStream)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentNullException.ThrowIfNull(dynamicHeader);
        ArgumentNullException.ThrowIfNull(parentStream);

        _fileStream = fileStream;
        _dynamicHeader = dynamicHeader;
        _parentStream = parentStream;
#else
        _fileStream = fileStream
            ?? throw new ArgumentNullException(nameof(fileStream));

        _dynamicHeader = dynamicHeader
            ?? throw new ArgumentNullException(nameof(dynamicHeader));

        _parentStream = parentStream
            ?? throw new ArgumentNullException(nameof(parentStream));

#endif

#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(length);
#else
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Negative lengths not allowed");
        }
#endif

        _length = length;
        _ownsParentStream = ownsParentStream;

        _blockBitmaps = new byte[_dynamicHeader.MaxTableEntries][];
        _blockBitmapSize =
            (int)
            MathUtilities.RoundUp(MathUtilities.Ceil(_dynamicHeader.BlockSize, Sizes.Sector * 8), Sizes.Sector);

        ReadBlockAllocationTable();

        // Detect where next block should go (cope if the footer is missing)
        _fileStream.Position = MathUtilities.RoundDown(_fileStream.Length, Sizes.Sector) - Sizes.Sector;
        Span<byte> footerBytes = stackalloc byte[Sizes.Sector];
        _fileStream.ReadExactly(footerBytes);
        var footer = Footer.FromBytes(footerBytes);
        _nextBlockStart = _fileStream.Position - (footer.IsValid() ? Sizes.Sector : 0);
    }

    public bool AutoCommitFooter
    {
        get;

        set
        {
            field = value;
            if (field)
            {
                UpdateFooter();
            }
        }
    } = true;

    public uint BlockSize => _dynamicHeader.BlockSize;

    public override bool CanRead => _parentStream is not null;

    public override bool CanSeek => _parentStream is not null;

    public override bool CanWrite => _parentStream is not null && _fileStream.CanWrite;

    public override IEnumerable<StreamExtent> Extents => GetExtentsInRange(0, Length);

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
        if (CanWrite)
        {
            _fileStream.Flush();
        }
    }

    public override void Flush(bool flushToDisk)
    {
        CheckDisposed();
        if (CanWrite)
        {
            FlushWrappedStream(_fileStream, flushToDisk);
        }
    }

    public override IEnumerable<StreamExtent> MapContent(long start, long length)
    {
        var position = start;
        var maxToRead = (int)Math.Min(length, _length - position);
        var numRead = 0;

        while (numRead < maxToRead)
        {
            var block = position / _dynamicHeader.BlockSize;
            var offsetInBlock = (uint)(position % _dynamicHeader.BlockSize);

            if (PopulateBlockBitmap(block))
            {
                var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);
                var offsetInSector = (int)(offsetInBlock % Sizes.Sector);
                var toRead = (int)Math.Min(maxToRead - numRead, _dynamicHeader.BlockSize - offsetInBlock);
                var blockBitmap = _parentStream is ZeroStream ? AllAllocatedBlockBitmap : _blockBitmaps[block];

                // 512 - offsetInSector);

                if (offsetInSector != 0 || toRead < Sizes.Sector)
                {
                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((blockBitmap[sectorInBlock / 8] & mask) != 0)
                    {
                        var extentStart = (_blockAllocationTable[block] + sectorInBlock) *
                                           Sizes.Sector + _blockBitmapSize + offsetInSector;
                        yield return new StreamExtent(extentStart, toRead);
                    }

                    numRead += toRead;
                    position += toRead;
                }
                else
                {
                    // Processing at least one whole sector, read as many as possible
                    var toReadSectors = toRead / Sizes.Sector;

                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    var readFromParent = (blockBitmap[sectorInBlock / 8] & mask) == 0;

                    var numSectors = 1;
                    while (numSectors < toReadSectors)
                    {
                        mask = (byte)(1 << (7 - (sectorInBlock + numSectors) & 0x7));
                        if ((blockBitmap[(sectorInBlock + numSectors) / 8] & mask) == 0 != readFromParent)
                        {
                            break;
                        }

                        ++numSectors;
                    }

                    toRead = numSectors * Sizes.Sector;

                    if (!readFromParent)
                    {
                        var extentStart = (_blockAllocationTable[block] + sectorInBlock) *
                                           Sizes.Sector + _blockBitmapSize;
                        yield return new StreamExtent(extentStart, toRead);
                    }

                    numRead += toRead;
                    position += toRead;
                }
            }
            else
            {
                var toRead = Math.Min(maxToRead - numRead, (int)(_dynamicHeader.BlockSize - offsetInBlock));
                numRead += toRead;
                position += toRead;
            }
        }
    }

    public AllocationBitmap GetBlockBitmap(int block)
    {
        if (!PopulateBlockBitmap(block))
        {
            return null;
        }

        var data = _blockBitmaps[block];

        return new AllocationBitmap(data, 0, (int)(_dynamicHeader.BlockSize / Sizes.Sector / 8));
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

        var maxToRead = (int)Math.Min(count, _length - _position);
        var numRead = 0;

        while (numRead < maxToRead)
        {
            var block = _position / _dynamicHeader.BlockSize;
            var offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

            if (PopulateBlockBitmap(block))
            {
                var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);
                var offsetInSector = (int)(offsetInBlock % Sizes.Sector);
                var toRead = (int)Math.Min(maxToRead - numRead, _dynamicHeader.BlockSize - offsetInBlock);
                var blockBitmap = _parentStream is ZeroStream ? AllAllocatedBlockBitmap : _blockBitmaps[block];

                // 512 - offsetInSector);

                if (offsetInSector != 0 || toRead < Sizes.Sector)
                {
                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));

                    if ((blockBitmap[sectorInBlock / 8] & mask) != 0)
                    {
                        _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) *
                                               Sizes.Sector + _blockBitmapSize + offsetInSector;
                        _fileStream.ReadExactly(buffer, offset + numRead, toRead);
                    }
                    else
                    {
                        _parentStream.Position = _position;
                        _parentStream.ReadExactly(buffer, offset + numRead, toRead);
                    }

                    numRead += toRead;
                    _position += toRead;
                }
                else
                {
                    // Processing at least one whole sector, read as many as possible
                    var toReadSectors = toRead / Sizes.Sector;

                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    var readFromParent = (blockBitmap[sectorInBlock / 8] & mask) == 0;

                    var numSectors = 1;
                    while (numSectors < toReadSectors)
                    {
                        mask = (byte)(1 << (7 - (sectorInBlock + numSectors) & 0x7));
                        if ((blockBitmap[(sectorInBlock + numSectors) / 8] & mask) == 0 != readFromParent)
                        {
                            break;
                        }

                        ++numSectors;
                    }

                    toRead = numSectors * Sizes.Sector;

                    if (readFromParent)
                    {
                        _parentStream.Position = _position;
                        _parentStream.ReadExactly(buffer, offset + numRead, toRead);
                    }
                    else
                    {
                        _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) *
                                               Sizes.Sector + _blockBitmapSize;
                        _fileStream.ReadExactly(buffer, offset + numRead, toRead);
                    }

                    numRead += toRead;
                    _position += toRead;
                }
            }
            else
            {
                var toRead = Math.Min(maxToRead - numRead, (int)(_dynamicHeader.BlockSize - offsetInBlock));
                _parentStream.Position = _position;
                _parentStream.ReadExactly(buffer, offset + numRead, toRead);
                numRead += toRead;
                _position += toRead;
            }
        }

        return numRead;
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

        var maxToRead = (int)Math.Min(buffer.Length, _length - _position);
        var numRead = 0;

        while (numRead < maxToRead)
        {
            var block = _position / _dynamicHeader.BlockSize;
            var offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

            if (await PopulateBlockBitmapAsync(block, cancellationToken).ConfigureAwait(false))
            {
                var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);
                var offsetInSector = (int)(offsetInBlock % Sizes.Sector);
                var toRead = (int)Math.Min(maxToRead - numRead, _dynamicHeader.BlockSize - offsetInBlock);
                var blockBitmap = _parentStream is ZeroStream ? AllAllocatedBlockBitmap : _blockBitmaps[block];

                // 512 - offsetInSector);

                if (offsetInSector != 0 || toRead < Sizes.Sector)
                {
                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((blockBitmap[sectorInBlock / 8] & mask) != 0)
                    {
                        _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) *
                                               Sizes.Sector + _blockBitmapSize + offsetInSector;
                        await _fileStream.ReadExactlyAsync(buffer.Slice(numRead, toRead), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _parentStream.Position = _position;
                        await _parentStream.ReadExactlyAsync(buffer.Slice(numRead, toRead), cancellationToken).ConfigureAwait(false);
                    }

                    numRead += toRead;
                    _position += toRead;
                }
                else
                {
                    // Processing at least one whole sector, read as many as possible
                    var toReadSectors = toRead / Sizes.Sector;

                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    var readFromParent = (blockBitmap[sectorInBlock / 8] & mask) == 0;

                    var numSectors = 1;
                    while (numSectors < toReadSectors)
                    {
                        mask = (byte)(1 << (7 - (sectorInBlock + numSectors) & 0x7));
                        if ((blockBitmap[(sectorInBlock + numSectors) / 8] & mask) == 0 != readFromParent)
                        {
                            break;
                        }

                        ++numSectors;
                    }

                    toRead = numSectors * Sizes.Sector;

                    if (readFromParent)
                    {
                        _parentStream.Position = _position;
                        await _parentStream.ReadExactlyAsync(buffer.Slice(numRead, toRead), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) *
                                               Sizes.Sector + _blockBitmapSize;
                        await _fileStream.ReadExactlyAsync(buffer.Slice(numRead, toRead), cancellationToken).ConfigureAwait(false);
                    }

                    numRead += toRead;
                    _position += toRead;
                }
            }
            else
            {
                var toRead = Math.Min(maxToRead - numRead, (int)(_dynamicHeader.BlockSize - offsetInBlock));
                _parentStream.Position = _position;
                await _parentStream.ReadExactlyAsync(buffer.Slice(numRead, toRead), cancellationToken).ConfigureAwait(false);
                numRead += toRead;
                _position += toRead;
            }
        }

        return numRead;
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

        var maxToRead = (int)Math.Min(buffer.Length, _length - _position);
        var numRead = 0;

        while (numRead < maxToRead)
        {
            var block = _position / _dynamicHeader.BlockSize;
            var offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

            if (PopulateBlockBitmap(block))
            {
                var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);
                var offsetInSector = (int)(offsetInBlock % Sizes.Sector);
                var toRead = (int)Math.Min(maxToRead - numRead, _dynamicHeader.BlockSize - offsetInBlock);
                var blockBitmap = _parentStream is ZeroStream ? AllAllocatedBlockBitmap : _blockBitmaps[block];

                // 512 - offsetInSector);

                if (offsetInSector != 0 || toRead < Sizes.Sector)
                {
                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((blockBitmap[sectorInBlock / 8] & mask) != 0)
                    {
                        _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) *
                                               Sizes.Sector + _blockBitmapSize + offsetInSector;
                        _fileStream.ReadExactly(buffer.Slice(numRead, toRead));
                    }
                    else
                    {
                        _parentStream.Position = _position;
                        _parentStream.ReadExactly(buffer.Slice(numRead, toRead));
                    }

                    numRead += toRead;
                    _position += toRead;
                }
                else
                {
                    // Processing at least one whole sector, read as many as possible
                    var toReadSectors = toRead / Sizes.Sector;

                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    var readFromParent = (blockBitmap[sectorInBlock / 8] & mask) == 0;

                    var numSectors = 1;
                    while (numSectors < toReadSectors)
                    {
                        mask = (byte)(1 << (7 - (sectorInBlock + numSectors) & 0x7));
                        if ((blockBitmap[(sectorInBlock + numSectors) / 8] & mask) == 0 != readFromParent)
                        {
                            break;
                        }

                        ++numSectors;
                    }

                    toRead = numSectors * Sizes.Sector;

                    if (readFromParent)
                    {
                        _parentStream.Position = _position;
                        _parentStream.ReadExactly(buffer.Slice(numRead, toRead));
                    }
                    else
                    {
                        _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) *
                                               Sizes.Sector + _blockBitmapSize;
                        _fileStream.ReadExactly(buffer.Slice(numRead, toRead));
                    }

                    numRead += toRead;
                    _position += toRead;
                }
            }
            else
            {
                var toRead = Math.Min(maxToRead - numRead, (int)(_dynamicHeader.BlockSize - offsetInBlock));
                _parentStream.Position = _position;
                _parentStream.ReadExactly(buffer.Slice(numRead, toRead));
                numRead += toRead;
                _position += toRead;
            }
        }

        return numRead;
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
            throw new NotImplementedException("Cannot modify virtual length of vhd image");
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new IOException("Attempt to write to read-only stream");
        }

        if (_position + count > _length)
        {
            throw new IOException("Attempt to write beyond end of the stream");
        }

        var numWritten = 0;

        Span<byte> sectorBuffer = stackalloc byte[Sizes.Sector];

        while (numWritten < count)
        {
            var block = _position / _dynamicHeader.BlockSize;
            var offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

            if (!PopulateBlockBitmap(block))
            {
                AllocateBlock(block);
            }

            var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);
            var offsetInSector = (int)(offsetInBlock % Sizes.Sector);
            var toWrite = (int)Math.Min(count - numWritten, _dynamicHeader.BlockSize - offsetInBlock);

            var blockBitmapDirty = false;

            // Need to read - we're not handling a full sector
            if (offsetInSector != 0 || toWrite < Sizes.Sector)
            {
                // Reduce the write to just the end of the current sector
                toWrite = Math.Min(count - numWritten, Sizes.Sector - offsetInSector);

                var sectorMask = (byte)(1 << (7 - (sectorInBlock & 0x7)));

                var sectorStart = (_blockAllocationTable[block] + sectorInBlock) * Sizes.Sector +
                                   _blockBitmapSize;

                // Get the existing sector data (if any), or otherwise the parent's content
                if (_parentStream is ZeroStream ||
                    (_blockBitmaps[block][sectorInBlock / 8] & sectorMask) != 0)
                {
                    _fileStream.Position = sectorStart;
                    _fileStream.ReadExactly(sectorBuffer);
                }
                else
                {
                    _parentStream.Position = _position / Sizes.Sector * Sizes.Sector;
                    _parentStream.ReadExactly(sectorBuffer);
                }

                // Overlay as much data as we have for this sector
                buffer.AsSpan(offset + numWritten, toWrite).CopyTo(sectorBuffer[offsetInSector..]);

                // Write the sector back
                _fileStream.Position = sectorStart;
                _fileStream.Write(sectorBuffer);

                // Update the in-memory block bitmap
                if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) == 0)
                {
                    _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                    blockBitmapDirty = true;
                }
            }
            else
            {
                // Processing at least one whole sector, just write (after making sure to trim any partial sectors from the end)...
                toWrite = toWrite / Sizes.Sector * Sizes.Sector;

                _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) * Sizes.Sector +
                                       _blockBitmapSize;
                _fileStream.Write(buffer, offset + numWritten, toWrite);

                // Update all of the bits in the block bitmap
                for (var i = offset; i < offset + toWrite; i += Sizes.Sector)
                {
                    var sectorMask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) == 0)
                    {
                        _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                        blockBitmapDirty = true;
                    }

                    sectorInBlock++;
                }
            }

            if (blockBitmapDirty)
            {
                WriteBlockBitmap(block);
            }

            numWritten += toWrite;
            _position += toWrite;
        }

        _atEof = false;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new IOException("Attempt to write to read-only stream");
        }

        if (_position + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of the stream");
        }

        var numWritten = 0;

        while (numWritten < buffer.Length)
        {
            var block = _position / _dynamicHeader.BlockSize;
            var offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

            if (!await PopulateBlockBitmapAsync(block, cancellationToken).ConfigureAwait(false))
            {
                await AllocateBlockAsync(block, cancellationToken).ConfigureAwait(false);
            }

            var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);
            var offsetInSector = (int)(offsetInBlock % Sizes.Sector);
            var toWrite = (int)Math.Min(buffer.Length - numWritten, _dynamicHeader.BlockSize - offsetInBlock);

            var blockBitmapDirty = false;

            // Need to read - we're not handling a full sector
            if (offsetInSector != 0 || toWrite < Sizes.Sector)
            {
                // Reduce the write to just the end of the current sector
                toWrite = Math.Min(buffer.Length - numWritten, Sizes.Sector - offsetInSector);

                var sectorMask = (byte)(1 << (7 - (sectorInBlock & 0x7)));

                var sectorStart = (_blockAllocationTable[block] + sectorInBlock) * Sizes.Sector +
                                   _blockBitmapSize;

                // Get the existing sector data (if any), or otherwise the parent's content
                var sectorBuffer = ArrayPool<byte>.Shared.Rent(Sizes.Sector);
                try
                {
                    if (_parentStream is ZeroStream ||
                        (_blockBitmaps[block][sectorInBlock / 8] & sectorMask) != 0)
                    {
                        _fileStream.Position = sectorStart;
                        await _fileStream.ReadExactlyAsync(sectorBuffer.AsMemory(0, Sizes.Sector), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _parentStream.Position = _position / Sizes.Sector * Sizes.Sector;
                        await _parentStream.ReadExactlyAsync(sectorBuffer.AsMemory(0, Sizes.Sector), cancellationToken).ConfigureAwait(false);
                    }

                    // Overlay as much data as we have for this sector
                    buffer.Slice(numWritten, toWrite).CopyTo(sectorBuffer.AsMemory(offsetInSector));

                    // Write the sector back
                    _fileStream.Position = sectorStart;
                    await _fileStream.WriteAsync(sectorBuffer.AsMemory(0, Sizes.Sector), cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(sectorBuffer);
                }

                // Update the in-memory block bitmap
                if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) == 0)
                {
                    _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                    blockBitmapDirty = true;
                }
            }
            else
            {
                // Processing at least one whole sector, just write (after making sure to trim any partial sectors from the end)...
                toWrite = toWrite / Sizes.Sector * Sizes.Sector;

                _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) * Sizes.Sector +
                                       _blockBitmapSize;
                await _fileStream.WriteAsync(buffer.Slice(numWritten, toWrite), cancellationToken).ConfigureAwait(false);

                // Update all of the bits in the block bitmap
                for (var i = 0; i < toWrite; i += Sizes.Sector)
                {
                    var sectorMask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) == 0)
                    {
                        _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                        blockBitmapDirty = true;
                    }

                    sectorInBlock++;
                }
            }

            if (blockBitmapDirty)
            {
                await WriteBlockBitmapAsync(block, cancellationToken).ConfigureAwait(false);
            }

            numWritten += toWrite;
            _position += toWrite;
        }

        _atEof = false;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CheckDisposed();

        if (!CanWrite)
        {
            throw new IOException("Attempt to write to read-only stream");
        }

        if (_position + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of the stream");
        }

        var numWritten = 0;

        Span<byte> sectorBuffer = stackalloc byte[Sizes.Sector];

        while (numWritten < buffer.Length)
        {
            var block = _position / _dynamicHeader.BlockSize;
            var offsetInBlock = (uint)(_position % _dynamicHeader.BlockSize);

            if (!PopulateBlockBitmap(block))
            {
                AllocateBlock(block);
            }

            var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);
            var offsetInSector = (int)(offsetInBlock % Sizes.Sector);
            var toWrite = (int)Math.Min(buffer.Length - numWritten, _dynamicHeader.BlockSize - offsetInBlock);

            var blockBitmapDirty = false;

            // Need to read - we're not handling a full sector
            if (offsetInSector != 0 || toWrite < Sizes.Sector)
            {
                // Reduce the write to just the end of the current sector
                toWrite = Math.Min(buffer.Length - numWritten, Sizes.Sector - offsetInSector);

                var sectorMask = (byte)(1 << (7 - (sectorInBlock & 0x7)));

                var sectorStart = (_blockAllocationTable[block] + sectorInBlock) * Sizes.Sector +
                                   _blockBitmapSize;

                // Get the existing sector data (if any), or otherwise the parent's content
                if (_parentStream is ZeroStream ||
                    (_blockBitmaps[block][sectorInBlock / 8] & sectorMask) != 0)
                {
                    _fileStream.Position = sectorStart;
                    _fileStream.ReadExactly(sectorBuffer);
                }
                else
                {
                    _parentStream.Position = _position / Sizes.Sector * Sizes.Sector;
                    _parentStream.ReadExactly(sectorBuffer);
                }

                // Overlay as much data as we have for this sector
                buffer.Slice(numWritten, toWrite).CopyTo(sectorBuffer[offsetInSector..]);

                // Write the sector back
                _fileStream.Position = sectorStart;
                _fileStream.Write(sectorBuffer);

                // Update the in-memory block bitmap
                if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) == 0)
                {
                    _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                    blockBitmapDirty = true;
                }
            }
            else
            {
                // Processing at least one whole sector, just write (after making sure to trim any partial sectors from the end)...
                toWrite = toWrite / Sizes.Sector * Sizes.Sector;

                _fileStream.Position = (_blockAllocationTable[block] + sectorInBlock) * Sizes.Sector +
                                       _blockBitmapSize;
                _fileStream.Write(buffer.Slice(numWritten, toWrite));

                // Update all of the bits in the block bitmap
                for (var i = 0; i < toWrite; i += Sizes.Sector)
                {
                    var sectorMask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((_blockBitmaps[block][sectorInBlock / 8] & sectorMask) == 0)
                    {
                        _blockBitmaps[block][sectorInBlock / 8] |= sectorMask;
                        blockBitmapDirty = true;
                    }

                    sectorInBlock++;
                }
            }

            if (blockBitmapDirty)
            {
                WriteBlockBitmap(block);
            }

            numWritten += toWrite;
            _position += toWrite;
        }

        _atEof = false;
    }

    public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
    {
        CheckDisposed();

        var maxCount = Math.Min(Length, start + count) - start;
        if (maxCount < 0)
        {
            return [];
        }

        var parentExtents = _parentStream.GetExtentsInRange(start, maxCount);

        var result = StreamExtent.Union(LayerExtents(start, maxCount), parentExtents);
        result = StreamExtent.Intersect(result, new StreamExtent(start, maxCount));
        return result;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                UpdateFooter();

                if (_ownsParentStream == Ownership.Dispose && _parentStream != null)
                {
                    _parentStream.Dispose();
                    _parentStream = null;
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private IEnumerable<StreamExtent> LayerExtents(long start, long count)
    {
        var maxPos = start + count;
        var pos = FindNextPresentSector(MathUtilities.RoundDown(start, Sizes.Sector), maxPos);
        while (pos < maxPos)
        {
            var end = FindNextAbsentSector(pos, maxPos);
            yield return new StreamExtent(pos, end - pos);

            pos = FindNextPresentSector(end, maxPos);
        }
    }

    private long FindNextPresentSector(long pos, long maxPos)
    {
        var foundStart = false;
        while (pos < maxPos && !foundStart)
        {
            var block = pos / _dynamicHeader.BlockSize;

            if (!PopulateBlockBitmap(block))
            {
                pos += _dynamicHeader.BlockSize;
            }
            else
            {
                var offsetInBlock = (uint)(pos % _dynamicHeader.BlockSize);
                var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);

                if (_blockBitmaps[block][sectorInBlock / 8] == 0)
                {
                    pos += (8 - (sectorInBlock & 0x7)) * Sizes.Sector;
                }
                else
                {
                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((_blockBitmaps[block][sectorInBlock / 8] & mask) != 0)
                    {
                        foundStart = true;
                    }
                    else
                    {
                        pos += Sizes.Sector;
                    }
                }
            }
        }

        return Math.Min(pos, maxPos);
    }

    private long FindNextAbsentSector(long pos, long maxPos)
    {
        var foundEnd = false;
        while (pos < maxPos && !foundEnd)
        {
            var block = pos / _dynamicHeader.BlockSize;

            if (!PopulateBlockBitmap(block))
            {
                foundEnd = true;
            }
            else
            {
                var offsetInBlock = (uint)(pos % _dynamicHeader.BlockSize);
                var sectorInBlock = (int)(offsetInBlock / Sizes.Sector);

                if (_blockBitmaps[block][sectorInBlock / 8] == 0xFF)
                {
                    pos += (8 - (sectorInBlock & 0x7)) * Sizes.Sector;
                }
                else
                {
                    var mask = (byte)(1 << (7 - (sectorInBlock & 0x7)));
                    if ((_blockBitmaps[block][sectorInBlock / 8] & mask) == 0)
                    {
                        foundEnd = true;
                    }
                    else
                    {
                        pos += Sizes.Sector;
                    }
                }
            }
        }

        return Math.Min(pos, maxPos);
    }

    private void ReadBlockAllocationTable()
    {
        _fileStream.Position = _dynamicHeader.TableOffset;
        var data = _fileStream.ReadExactly(_dynamicHeader.MaxTableEntries * 4);

        var bat = new uint[_dynamicHeader.MaxTableEntries];
        for (var i = 0; i < _dynamicHeader.MaxTableEntries; ++i)
        {
            bat[i] = EndianUtilities.ToUInt32BigEndian(data, i * 4);
        }

        _blockAllocationTable = bat;
    }

    private byte[] AllAllocatedBlockBitmap
    {
        get
        {
            if (field == null)
            {
                var newArray = StreamUtilities.GetUninitializedArray<byte>(_blockBitmapSize);
                newArray.AsSpan().Fill(0xff);
                field = newArray;
            }

            return field;
        }

        set;
    }

    private bool PopulateBlockBitmap(long block)
    {
        if (_blockBitmaps[block] != null)
        {
            // Nothing to do...
            return true;
        }

        if (_blockAllocationTable[block] == uint.MaxValue)
        {
            // No such block stored...
            return false;
        }

        // Read in bitmap
        _fileStream.Position = (long)_blockAllocationTable[block] * Sizes.Sector;
        _blockBitmaps[block] = _fileStream.ReadExactly(_blockBitmapSize);

        return true;
    }

    private async ValueTask<bool> PopulateBlockBitmapAsync(long block, CancellationToken cancellationToken)
    {
        if (_blockBitmaps[block] != null)
        {
            // Nothing to do...
            return true;
        }

        if (_blockAllocationTable[block] == uint.MaxValue)
        {
            // No such block stored...
            return false;
        }

        // Read in bitmap
        _fileStream.Position = (long)_blockAllocationTable[block] * Sizes.Sector;
        _blockBitmaps[block] = await _fileStream.ReadExactlyAsync(_blockBitmapSize, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private void AllocateBlock(long block)
    {
        if (_blockAllocationTable[block] != uint.MaxValue)
        {
            throw new ArgumentException("Attempt to allocate existing block");
        }

        _newBlocksAllocated = true;
        var newBlockStart = _nextBlockStart;

        // Create and write new sector bitmap
        var bitmap = new byte[_blockBitmapSize];
        _fileStream.Position = newBlockStart;
        _fileStream.Write(bitmap, 0, _blockBitmapSize);
        _blockBitmaps[block] = bitmap;

        _nextBlockStart += _blockBitmapSize + _dynamicHeader.BlockSize;
        if (_fileStream.Length < _nextBlockStart)
        {
            _fileStream.SetLength(_nextBlockStart);
        }

        // Update the BAT entry for the new block
        Span<byte> entryBuffer = stackalloc byte[4];
        EndianUtilities.WriteBytesBigEndian((uint)(newBlockStart / 512), entryBuffer);
        _fileStream.Position = _dynamicHeader.TableOffset + block * 4;
        _fileStream.Write(entryBuffer);
        _blockAllocationTable[block] = (uint)(newBlockStart / 512);

        if (AutoCommitFooter)
        {
            UpdateFooter();
        }
    }

    private async ValueTask AllocateBlockAsync(long block, CancellationToken cancellationToken)
    {
        if (_blockAllocationTable[block] != uint.MaxValue)
        {
            throw new ArgumentException("Attempt to allocate existing block");
        }

        _newBlocksAllocated = true;
        var newBlockStart = _nextBlockStart;

        // Create and write new sector bitmap
        var bitmap = new byte[_blockBitmapSize];
        _fileStream.Position = newBlockStart;
        await _fileStream.WriteAsync(bitmap.AsMemory(0, _blockBitmapSize), cancellationToken).ConfigureAwait(false);
        _blockBitmaps[block] = bitmap;

        _nextBlockStart += _blockBitmapSize + _dynamicHeader.BlockSize;
        if (_fileStream.Length < _nextBlockStart)
        {
            _fileStream.SetLength(_nextBlockStart);
        }

        // Update the BAT entry for the new block
        var entryBuffer = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            EndianUtilities.WriteBytesBigEndian((uint)(newBlockStart / 512), entryBuffer);
            _fileStream.Position = _dynamicHeader.TableOffset + block * 4;
            await _fileStream.WriteAsync(entryBuffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(entryBuffer);
        }

        _blockAllocationTable[block] = (uint)(newBlockStart / 512);

        if (AutoCommitFooter)
        {
            await UpdateFooterAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void WriteBlockBitmap(long block)
    {
        _fileStream.Position = (long)_blockAllocationTable[block] * Sizes.Sector;
        _fileStream.Write(_blockBitmaps[block], 0, _blockBitmapSize);
    }

    private ValueTask WriteBlockBitmapAsync(long block, CancellationToken cancellationToken)
    {
        _fileStream.Position = (long)_blockAllocationTable[block] * Sizes.Sector;
        return _fileStream.WriteAsync(_blockBitmaps[block].AsMemory(0, _blockBitmapSize), cancellationToken);
    }

    private void CheckDisposed()
    {
        if (_parentStream == null)
        {
            throw new ObjectDisposedException("DynamicStream", "Attempt to use closed stream");
        }
    }

    private void UpdateFooter()
    {
        if (_newBlocksAllocated)
        {
            // Update the footer at the end of the file (if we allocated new blocks).
            if (_footerCache == null)
            {
                _fileStream.Position = 0;
                _footerCache = _fileStream.ReadExactly(Sizes.Sector);
            }

            _fileStream.Position = _nextBlockStart;
            _fileStream.Write(_footerCache, 0, _footerCache.Length);
        }
    }

    private async ValueTask UpdateFooterAsync(CancellationToken cancellationToken)
    {
        if (_newBlocksAllocated)
        {
            // Update the footer at the end of the file (if we allocated new blocks).
            if (_footerCache == null)
            {
                _fileStream.Position = 0;
                _footerCache = await _fileStream.ReadExactlyAsync(Sizes.Sector, cancellationToken).ConfigureAwait(false);
            }

            _fileStream.Position = _nextBlockStart;
            await _fileStream.WriteAsync(_footerCache, cancellationToken).ConfigureAwait(false);
        }
    }
}

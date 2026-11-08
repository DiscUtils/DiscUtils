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
using DiscUtils.Streams;
using LTRData.Extensions.Buffers;

namespace DiscUtils.Iscsi;

internal class DiskStream : SparseStream
{
    private readonly int _blockSize;

    private readonly long _length;
    private readonly long _lun;
    private long _position;
    private readonly Session _session;

    // Set by a write and cleared by a flush, so a flush with nothing written costs no round trip.
    private bool _unflushedWrites;

    // The target rejected SYNCHRONIZE CACHE as an unknown command: it has no volatile cache to flush.
    private bool _noSynchronizeCache;

    public DiskStream(Session session, long lun, FileAccess access)
    {
        _session = session;
        _lun = lun;

        var capacity = session.GetCapacity(lun);
        _blockSize = capacity.BlockSize;
        _length = capacity.LogicalBlockCount * capacity.BlockSize;
        CanWrite = access != FileAccess.Read;
        CanRead = access != FileAccess.Write;
    }

    public override bool CanRead { get; }

    public override bool CanSeek => true;

    public override bool CanWrite { get; }

    public override IEnumerable<StreamExtent> Extents
        => SingleValueEnumerable.Get(new StreamExtent(0, _length));

    public int BlockSize => _blockSize;

    public override long Length => _length;

    public override long Position
    {
        get => _position;

        set => _position = value;
    }

    /// <summary>
    /// Makes the target put the writes it acknowledged on stable storage (SCSI SYNCHRONIZE CACHE). A target may
    /// acknowledge a write while it is still in a volatile cache; until this returns, a power loss on the target can
    /// lose it or keep a later write without an earlier one. Does nothing if nothing was written since the last flush.
    /// </summary>
    public override void Flush()
    {
        if (!_unflushedWrites)
        {
            return;
        }

        if (!_noSynchronizeCache)
        {
            try
            {
                _session.SynchronizeCache(_lun);
            }
            catch (ScsiCommandException e) when (IsUnknownCommand(e))
            {
                _noSynchronizeCache = true;
            }
        }

        _unflushedWrites = false;
    }

    /// <inheritdoc cref="Flush"/>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (!_unflushedWrites)
        {
            return;
        }

        if (!_noSynchronizeCache)
        {
            try
            {
                await _session.SynchronizeCacheAsync(_lun, cancellationToken).ConfigureAwait(false);
            }
            catch (ScsiCommandException e) when (IsUnknownCommand(e))
            {
                _noSynchronizeCache = true;
            }
        }

        _unflushedWrites = false;
    }

    // ILLEGAL REQUEST / INVALID COMMAND OPERATION CODE: a target without a write cache need not implement the command.
    private static bool IsUnknownCommand(ScsiCommandException e)
        => e.TryGetSenseData(out var sense)
           && sense.SenseKey == ScsiSenseKey.IllegalRequest
           && sense.AdditionalSenseCode == 0x20;

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(Span<byte> buffer)
    {
        if (!CanRead)
        {
            throw new InvalidOperationException("Attempt to read from read-only stream");
        }

        if (_position % BlockSize != 0
            || buffer.Length % BlockSize != 0)
        {
            throw new ArgumentException("I/O not aligned to block boundaries is not supported");
        }

        var maxToRead = Math.Min((int)Math.Min(_length - _position, buffer.Length), _session.ActiveConnection.MaxInitiatorTransmitDataSegmentLength);

        var firstBlock = _position / _blockSize;
        var lastBlock = MathUtilities.Ceil(_position + maxToRead, _blockSize);

        return _session.Read(_lun, firstBlock, checked((int)(lastBlock - firstBlock)), buffer[..maxToRead]);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!CanRead)
        {
            throw new InvalidOperationException("Attempt to read from read-only stream");
        }

        if (_position % BlockSize != 0
            || buffer.Length % BlockSize != 0)
        {
            throw new ArgumentException("I/O not aligned to block boundaries is not supported");
        }

        var maxToRead = Math.Min((int)Math.Min(_length - _position, buffer.Length), _session.ActiveConnection.MaxInitiatorTransmitDataSegmentLength);

        var firstBlock = _position / _blockSize;
        var lastBlock = MathUtilities.Ceil(_position + maxToRead, _blockSize);

        return _session.ReadAsync(_lun, firstBlock, checked((int)(lastBlock - firstBlock)), buffer[..maxToRead], cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var effectiveOffset = offset;
        if (origin == SeekOrigin.Current)
        {
            effectiveOffset += _position;
        }
        else if (origin == SeekOrigin.End)
        {
            effectiveOffset += _length;
        }

        if (effectiveOffset < 0)
        {
            throw new ArgumentException("Attempt to move before beginning of disk");
        }

        _position = effectiveOffset;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
        => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!CanWrite)
        {
            throw new IOException("Attempt to write to read-only stream");
        }

        if (_position + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of stream");
        }

        if (_position % BlockSize != 0
            || buffer.Length % BlockSize != 0)
        {
            throw new ArgumentException("I/O not aligned to block boundaries is not supported");
        }

        var maxBytesPerWrite = _session.ImmediateData
            ? _session.FirstBurstLength
            : (_session.ActiveConnection.MaxTargetReceiveDataSegmentLength ?? _session.ActiveConnection.MaxInitiatorTransmitDataSegmentLength);

        _unflushedWrites = true;
        var numWritten = 0;

        while (numWritten < buffer.Length)
        {
            checked
            {
                var currentBlock = _position / _blockSize;
                var maxWrite = Math.Min(buffer.Length - numWritten, maxBytesPerWrite);
                var numBlocks = MathUtilities.Ceil(maxWrite, _blockSize);

                var written = _session.Write(_lun, currentBlock, (int)numBlocks, _blockSize, buffer[numWritten..]);

                if (written == 0)
                {
                    throw new IOException($"Incomplete write, wrote {numWritten} bytes of requested {buffer.Length}");
                }

                numWritten += written;
                _position += written;
            }
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!CanWrite)
        {
            throw new IOException("Attempt to write to read-only stream");
        }

        if (_position + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of stream");
        }

        if (_position % BlockSize != 0
            || buffer.Length % BlockSize != 0)
        {
            throw new ArgumentException("I/O not aligned to block boundaries is not supported");
        }

        var maxBytesPerWrite = _session.ImmediateData
            ? _session.FirstBurstLength
            : (_session.ActiveConnection.MaxTargetReceiveDataSegmentLength ?? _session.ActiveConnection.MaxInitiatorTransmitDataSegmentLength);

        _unflushedWrites = true;
        var numWritten = 0;

        while (numWritten < buffer.Length)
        {
            checked
            {
                var currentBlock = _position / _blockSize;
                var maxWrite = Math.Min(buffer.Length - numWritten, maxBytesPerWrite);
                var numBlocks = MathUtilities.Ceil(maxWrite, _blockSize);

                var written = await _session.WriteAsync(_lun, currentBlock, (int)numBlocks, _blockSize, buffer[numWritten..], cancellationToken).ConfigureAwait(false);

                if (written == 0)
                {
                    throw new IOException($"Incomplete write, wrote {numWritten} bytes of requested {buffer.Length}");
                }

                numWritten += written;
                _position += written;
            }
        }
    }
}
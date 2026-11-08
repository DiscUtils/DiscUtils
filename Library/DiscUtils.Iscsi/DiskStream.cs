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
    /// Requests that the target synchronize its write cache with stable storage (SCSI SYNCHRONIZE CACHE).
    /// This covers the entire LUN, including writes made through other streams or before this stream was opened.
    /// A failed or unsupported synchronization is reported to the caller.
    /// </summary>
    public override void Flush() => Flush(flushToDisk: true);

    /// <summary>
    /// Requests a target-wide cache synchronization. iSCSI has no separate buffered data in this stream to flush,
    /// so both overloads preserve the established <see cref="Flush()"/> behavior and issue SYNCHRONIZE CACHE.
    /// In particular, <paramref name="flushToDisk"/> set to <c>true</c> is an unconditional durability barrier,
    /// even if this stream has not written any data itself.
    /// </summary>
    public override void Flush(bool flushToDisk) => _session.SynchronizeCache(_lun);

    /// <summary>Asynchronously synchronizes the whole LUN, even if this stream has not written anything.</summary>
    public override async Task FlushAsync(CancellationToken cancellationToken)
        => await _session.SynchronizeCacheAsync(_lun, cancellationToken).ConfigureAwait(false);

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

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

using LTRData.Extensions.Buffers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

public class StripedStream : SparseStream
{
    private readonly bool _canRead;
    private readonly bool _canWrite;
    private readonly long _length;
    private readonly Ownership _ownsWrapped;

    private long _position;
    private readonly long _stripeSize;
    private List<SparseStream> _wrapped;

    public StripedStream(long stripeSize, Ownership ownsWrapped, IEnumerable<SparseStream> wrapped)
    {
        _wrapped = [.. wrapped];
        _stripeSize = stripeSize;
        _ownsWrapped = ownsWrapped;

        _canRead = _wrapped[0].CanRead;
        _canWrite = _wrapped[0].CanWrite;
        var subStreamLength = _wrapped[0].Length;

        foreach (var stream in _wrapped)
        {
            if (stream.CanRead != _canRead || stream.CanWrite != _canWrite)
            {
                throw new ArgumentException("All striped streams must have the same read/write permissions",
                    nameof(wrapped));
            }

            if (stream.Length != subStreamLength)
            {
                throw new ArgumentException("All striped streams must have the same length", nameof(wrapped));
            }
        }

        _length = subStreamLength * _wrapped.Count;
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this))
        {
            return virtualPosition;
        }

        var stripe = virtualPosition / _stripeSize;
        var stripeOffset = virtualPosition % _stripeSize;

        var streamIdx = (int)(stripe % _wrapped.Count);
        var streamStripe = stripe / _wrapped.Count;

        var targetStream = _wrapped[streamIdx];

        var targetStreamPosition = streamStripe * _stripeSize + stripeOffset;

        return targetStream.GetPositionInBaseStream(baseStream, targetStreamPosition);
    }

    public override bool CanRead => _canRead;

    public override bool CanSeek => true;

    public override bool CanWrite => _canWrite;

    // Temporary, indicate there are no 'unstored' extents.
    // Consider combining extent information from all wrapped streams in future.
    public override IEnumerable<StreamExtent> Extents
        => SingleValueEnumerable.Get(new StreamExtent(0, _length));

    public override long Length => _length;

    public override long Position
    {
        get => _position;

        set => _position = value;
    }

    public override void Flush()
    {
        foreach (var stream in _wrapped)
        {
            stream.Flush();
        }
    }

    public override void Flush(bool flushToDisk)
    {
        foreach (var stream in _wrapped)
        {
            stream.Flush(flushToDisk);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!CanRead)
        {
            throw new InvalidOperationException("Attempt to read to non-readable stream");
        }

        var maxToRead = (int)Math.Min(_length - _position, count);

        var totalRead = 0;
        while (totalRead < maxToRead)
        {
            var stripe = _position / _stripeSize;
            var stripeOffset = _position % _stripeSize;
            var stripeToRead = (int)Math.Min(maxToRead - totalRead, _stripeSize - stripeOffset);

            var streamIdx = (int)(stripe % _wrapped.Count);
            var streamStripe = stripe / _wrapped.Count;

            var targetStream = _wrapped[streamIdx];
            targetStream.Position = streamStripe * _stripeSize + stripeOffset;

            var numRead = targetStream.Read(buffer, offset + totalRead, stripeToRead);
            _position += numRead;
            totalRead += numRead;
        }

        return totalRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!CanRead)
        {
            throw new InvalidOperationException("Attempt to read to non-readable stream");
        }

        var maxToRead = (int)Math.Min(_length - _position, buffer.Length);

        var totalRead = 0;
        while (totalRead < maxToRead)
        {
            var stripe = _position / _stripeSize;
            var stripeOffset = _position % _stripeSize;
            var stripeToRead = (int)Math.Min(maxToRead - totalRead, _stripeSize - stripeOffset);

            var streamIdx = (int)(stripe % _wrapped.Count);
            var streamStripe = stripe / _wrapped.Count;

            var targetStream = _wrapped[streamIdx];
            targetStream.Position = streamStripe * _stripeSize + stripeOffset;

            var numRead = await targetStream.ReadAsync(buffer.Slice(totalRead, stripeToRead), cancellationToken).ConfigureAwait(false);
            _position += numRead;
            totalRead += numRead;
        }

        return totalRead;
    }

    public override int Read(Span<byte> buffer)
    {
        if (!CanRead)
        {
            throw new InvalidOperationException("Attempt to read to non-readable stream");
        }

        var maxToRead = (int)Math.Min(_length - _position, buffer.Length);

        var totalRead = 0;
        while (totalRead < maxToRead)
        {
            var stripe = _position / _stripeSize;
            var stripeOffset = _position % _stripeSize;
            var stripeToRead = (int)Math.Min(maxToRead - totalRead, _stripeSize - stripeOffset);

            var streamIdx = (int)(stripe % _wrapped.Count);
            var streamStripe = stripe / _wrapped.Count;

            var targetStream = _wrapped[streamIdx];
            targetStream.Position = streamStripe * _stripeSize + stripeOffset;

            var numRead = targetStream.Read(buffer.Slice(totalRead, stripeToRead));
            _position += numRead;
            totalRead += numRead;
        }

        return totalRead;
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
            throw new IOException("Attempt to move before beginning of stream");
        }

        _position = effectiveOffset;
        return _position;
    }

    public override void SetLength(long value)
    {
        if (value != _length)
        {
            throw new InvalidOperationException("Changing the stream length is not permitted for striped streams");
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to write to read-only stream");
        }

        if (_position + count > _length)
        {
            throw new IOException("Attempt to write beyond end of stream");
        }

        var totalWritten = 0;
        while (totalWritten < count)
        {
            var stripe = _position / _stripeSize;
            var stripeOffset = _position % _stripeSize;
            var stripeToWrite = (int)Math.Min(count - totalWritten, _stripeSize - stripeOffset);

            var streamIdx = (int)(stripe % _wrapped.Count);
            var streamStripe = stripe / _wrapped.Count;

            var targetStream = _wrapped[streamIdx];
            targetStream.Position = streamStripe * _stripeSize + stripeOffset;
            targetStream.Write(buffer, offset + totalWritten, stripeToWrite);

            _position += stripeToWrite;
            totalWritten += stripeToWrite;
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to write to read-only stream");
        }

        if (_position + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of stream");
        }

        var totalWritten = 0;
        while (totalWritten < buffer.Length)
        {
            var stripe = _position / _stripeSize;
            var stripeOffset = _position % _stripeSize;
            var stripeToWrite = (int)Math.Min(buffer.Length - totalWritten, _stripeSize - stripeOffset);

            var streamIdx = (int)(stripe % _wrapped.Count);
            var streamStripe = stripe / _wrapped.Count;

            var targetStream = _wrapped[streamIdx];
            targetStream.Position = streamStripe * _stripeSize + stripeOffset;
            await targetStream.WriteAsync(buffer.Slice(totalWritten, stripeToWrite), cancellationToken).ConfigureAwait(false);

            _position += stripeToWrite;
            totalWritten += stripeToWrite;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Attempt to write to read-only stream");
        }

        if (_position + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of stream");
        }

        var totalWritten = 0;
        while (totalWritten < buffer.Length)
        {
            var stripe = _position / _stripeSize;
            var stripeOffset = _position % _stripeSize;
            var stripeToWrite = (int)Math.Min(buffer.Length - totalWritten, _stripeSize - stripeOffset);

            var streamIdx = (int)(stripe % _wrapped.Count);
            var streamStripe = stripe / _wrapped.Count;

            var targetStream = _wrapped[streamIdx];
            targetStream.Position = streamStripe * _stripeSize + stripeOffset;
            targetStream.Write(buffer.Slice(totalWritten, stripeToWrite));

            _position += stripeToWrite;
            totalWritten += stripeToWrite;
        }
    }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_wrapped.Select(stream => stream.FlushAsync(cancellationToken)));

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && _wrapped != null)
            {
                foreach (var stream in _wrapped)
                {
                    if (_ownsWrapped == Ownership.Dispose)
                    {
                        stream.Dispose();
                    }
                    else if (stream.CanWrite)
                    {
                        stream.Flush();
                    }
                }

                _wrapped = null!;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}

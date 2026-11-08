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

using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Buffers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

public class SubStream : MappedStream
{
    private readonly long _first;
    private readonly Ownership _ownsParent;

    public Stream Parent { get; }

    public SubStream(Stream parent, long first, long length)
    {
        Parent = parent;
        _first = first;
        MaximumLength = length;
        _ownsParent = Ownership.None;
    }

    public SubStream(Stream parent, Ownership ownsParent, long first, long length)
    {
        Parent = parent;
        _ownsParent = ownsParent;
        _first = first;
        MaximumLength = length;
    }

    public override bool CanRead => Parent is { CanRead: true };

    public override bool CanSeek => Parent is { CanSeek: true };

    public override bool CanWrite => Parent is { CanWrite: true };

    public override IEnumerable<StreamExtent> Extents
    {
        get
        {
            var parentAsSparse = Parent as SparseStream;
            if (parentAsSparse is not null)
            {
                return OffsetExtents(parentAsSparse.GetExtentsInRange(_first, MaximumLength));
            }

            return SingleValueEnumerable.Get(new StreamExtent(0, MaximumLength));
        }
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this))
        {
            return virtualPosition;
        }

        if (Parent is CompatibilityStream baseCompatStream)
        {
            return baseCompatStream.GetPositionInBaseStream(baseStream, _first + virtualPosition);
        }

        return _first + virtualPosition;
    }

    /// <summary>
    /// Current length of the stream. This may be less than the maximum length
    /// if the underlying stream is shorter than the defined substream.
    /// </summary>
    public override long Length => Math.Min(MaximumLength, Parent.Length - _first);

    /// <summary>
    /// Gets the maximum length allowed for the current stream.
    /// </summary>
    public long MaximumLength { get; }

    public override long Position { get; set; }

    public override IEnumerable<StreamExtent> MapContent(long start, long length)
        => SingleValueEnumerable.Get(new StreamExtent(start + _first, length));

    public override void Flush()
    {
        Parent.Flush();
    }

    public override void Flush(bool flushToDisk)
    {
        FlushWrappedStream(Parent, flushToDisk);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
        }

        if (Position < 0 || Position == MaximumLength)
        {
            return 0;
        }

        if (Position > MaximumLength)
        {
            throw new EndOfStreamException("Attempt to read beyond end of substream");
        }

        Parent.Position = _first + Position;
        var numRead = Parent.Read(buffer, offset,
            (int)Math.Min(count, Math.Min(MaximumLength - Position, int.MaxValue)));
        Position += numRead;
        return numRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (Position < 0 || Position == MaximumLength)
        {
            return 0;
        }

        if (Position > MaximumLength)
        {
            throw new EndOfStreamException("Attempt to read beyond end of substream");
        }

        Parent.Position = _first + Position;
        var numRead = await Parent.ReadAsync(buffer[..(int)Math.Min(buffer.Length, Math.Min(MaximumLength - Position, int.MaxValue))], cancellationToken).ConfigureAwait(false);
        Position += numRead;
        return numRead;
    }

    public override int Read(Span<byte> buffer)
    {
        if (Position < 0 || Position == MaximumLength)
        {
            return 0;
        }

        if (Position > MaximumLength)
        {
            throw new EndOfStreamException("Attempt to read beyond end of substream");
        }

        Parent.Position = _first + Position;
        var numRead = Parent.Read(buffer[..(int)Math.Min(buffer.Length, Math.Min(MaximumLength - Position, int.MaxValue))]);
        Position += numRead;
        return numRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var absNewPos = offset;
        if (origin == SeekOrigin.Current)
        {
            absNewPos += Position;
        }
        else if (origin == SeekOrigin.End)
        {
            absNewPos += MaximumLength;
        }

        if (absNewPos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Attempt to move before start of stream");
        }

        Position = absNewPos;
        return Position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("Attempt to change length of a substream");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        if (Position < 0 || Position + count > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of substream");
        }

        Parent.Position = _first + Position;
        Parent.Write(buffer, offset, count);
        Position += count;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        if (Position < 0 || Position + count > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of substream");
        }

        Parent.Position = _first + Position;
        await Parent.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        Position += count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (Position < 0 || Position + buffer.Length > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer), "Attempt to write beyond end of substream");
        }

        Parent.Position = _first + Position;
        await Parent.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        Position += buffer.Length;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (Position < 0 || Position + buffer.Length > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer), "Attempt to write beyond end of substream");
        }

        Parent.Position = _first + Position;
        Parent.Write(buffer);
        Position += buffer.Length;
    }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        Parent.FlushAsync(cancellationToken);

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (_ownsParent == Ownership.Dispose)
                {
                    Parent.Dispose();
                }
                else if (Parent.CanWrite)
                {
                    Parent.Flush();
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private IEnumerable<StreamExtent> OffsetExtents(IEnumerable<StreamExtent> src)
    {
        foreach (var e in src)
        {
            yield return new StreamExtent(e.Start - _first, e.Length);
        }
    }
}

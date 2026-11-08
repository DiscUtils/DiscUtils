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

using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Buffers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

/// <summary>
/// Base class for streams that wrap another stream.
/// </summary>
/// <typeparam name="T">The type of stream to wrap.</typeparam>
/// <remarks>
/// Provides the default implementation of methods &amp; properties, so
/// wrapping streams need only override the methods they need to intercept.
/// </remarks>
public class WrappingMappedStream<T> : MappedStream
    where T : Stream
{
    private readonly List<StreamExtent>? _extents;
    private readonly Ownership _ownership;

    public WrappingMappedStream(T toWrap, Ownership ownership, IEnumerable<StreamExtent>? extents)
    {
        WrappedStream = toWrap;
        _ownership = ownership;
        if (extents != null)
        {
            _extents = [.. extents];
        }
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this)
            || WrappedStream is not CompatibilityStream baseCompatStream)
        {
            return virtualPosition;
        }

        return baseCompatStream.GetPositionInBaseStream(baseStream, virtualPosition);
    }

    public override bool CanRead => WrappedStream is { CanRead: true };

    public override bool CanSeek => WrappedStream is { CanSeek: true };

    public override bool CanWrite => WrappedStream is { CanWrite: true };

    public override IEnumerable<StreamExtent> Extents
    {
        get
        {
            if (_extents != null)
            {
                return _extents;
            }

            if (WrappedStream is SparseStream sparse)
            {
                return sparse.Extents;
            }

            return SingleValueEnumerable.Get(new StreamExtent(0, WrappedStream.Length));
        }
    }

    public override long Length => WrappedStream.Length;

    public override long Position
    {
        get => WrappedStream.Position;
        set => WrappedStream.Position = value;
    }

    public T WrappedStream { get; private set; }

    public override IEnumerable<StreamExtent> MapContent(long start, long length)
    {
        if (WrappedStream is MappedStream mapped)
        {
            return mapped.MapContent(start, length);
        }

        return SingleValueEnumerable.Get(new StreamExtent(start, length));
    }

    public override void Flush()
    {
        WrappedStream.Flush();
    }

    public override void Flush(bool flushToDisk)
    {
        FlushWrappedStream(WrappedStream, flushToDisk);
    }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        WrappedStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        return WrappedStream.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WrappedStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return WrappedStream.ReadAsync(buffer, cancellationToken);
    }

    public override int Read(Span<byte> buffer)
    {
        return WrappedStream.Read(buffer);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return WrappedStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        WrappedStream.SetLength(value);
    }

    public override void Clear(int count)
    {
        if (WrappedStream is SparseStream sparse)
        {
            sparse.Clear(count);
        }
        else
        {
            base.Clear(count);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        WrappedStream.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WrappedStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        return WrappedStream.WriteAsync(buffer, cancellationToken);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        WrappedStream.Write(buffer);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (WrappedStream != null)
                {
                    if (_ownership == Ownership.Dispose)
                    {
                        WrappedStream.Dispose();
                    }
                    else if (WrappedStream.CanWrite)
                    {
                        WrappedStream.Flush();
                    }
                }

                WrappedStream = null!;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}

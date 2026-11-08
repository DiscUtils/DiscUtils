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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

/// <summary>
/// Base class for streams that wrap another stream.
/// </summary>
/// <remarks>
/// Provides the default implementation of methods &amp; properties, so
/// wrapping streams need only override the methods they need to intercept.
/// </remarks>
public class WrappingStream : SparseStream
{
    private readonly Ownership _ownership;
    private SparseStream _wrapped;

    public WrappingStream(SparseStream toWrap, Ownership ownership)
    {
        _wrapped = toWrap;
        _ownership = ownership;
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this))
        {
            return virtualPosition;
        }

        return _wrapped.GetPositionInBaseStream(baseStream, virtualPosition);
    }

    public override bool CanRead => _wrapped is { CanRead: true };

    public override bool CanSeek => _wrapped is { CanSeek: true };

    public override bool CanWrite => _wrapped is { CanWrite: true };

    public override IEnumerable<StreamExtent> Extents => _wrapped.Extents;

    public override long Length => _wrapped.Length;

    public override long Position
    {
        get => _wrapped.Position;
        set => _wrapped.Position = value;
    }

    public override void Flush()
    {
        _wrapped.Flush();
    }

    public override void Flush(bool flushToDisk)
    {
        _wrapped.Flush(flushToDisk);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _wrapped.Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _wrapped.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _wrapped.ReadAsync(buffer, cancellationToken);
    }

    public override int Read(Span<byte> buffer)
    {
        return _wrapped.Read(buffer);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _wrapped.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _wrapped.SetLength(value);
    }

    public override void Clear(int count)
    {
        _wrapped.Clear(count);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _wrapped.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _wrapped.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        return _wrapped.WriteAsync(buffer, cancellationToken);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _wrapped.Write(buffer);
    }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _wrapped.FlushAsync(cancellationToken);

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (_wrapped != null)
                {
                    if (_ownership == Ownership.Dispose)
                    {
                        _wrapped.Dispose();
                    }
                    else if (_wrapped.CanWrite)
                    {
                        _wrapped.Flush();
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

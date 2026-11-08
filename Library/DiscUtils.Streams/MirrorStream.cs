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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

public class MirrorStream : SparseStream
{
    private readonly bool _canRead;
    private readonly bool _canSeek;
    private readonly bool _canWrite;
    private readonly long _length;
    private readonly Ownership _ownsWrapped;
    private List<SparseStream> _wrapped;

    public MirrorStream(Ownership ownsWrapped, IEnumerable<SparseStream> wrapped)
    {
        _wrapped = [.. wrapped];
        _ownsWrapped = ownsWrapped;

        _canRead = _wrapped[0].CanRead;
        _canWrite = _wrapped[0].CanWrite;
        _canSeek = _wrapped[0].CanSeek;
        _length = _wrapped[0].Length;

        foreach (var stream in _wrapped)
        {
            if (stream.CanRead != _canRead || stream.CanWrite != _canWrite || stream.CanSeek != _canSeek)
            {
                throw new ArgumentException("All mirrored streams must have the same read/write/seek permissions",
                    nameof(wrapped));
            }

            if (stream.Length != _length)
            {
                throw new ArgumentException("All mirrored streams must have the same length", nameof(wrapped));
            }
        }
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this))
        {
            return virtualPosition;
        }

        return _wrapped[0].GetPositionInBaseStream(baseStream, virtualPosition);
    }

    public override bool CanRead => _canRead;

    public override bool CanSeek => _canSeek;

    public override bool CanWrite => _canWrite;

    public override IEnumerable<StreamExtent> Extents => _wrapped[0].Extents;

    public override long Length => _length;

    public override long Position
    {
        get => _wrapped[0].Position;

        set => _wrapped[0].Position = value;
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
        return _wrapped[0].Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _wrapped[0].ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _wrapped[0].ReadAsync(buffer, cancellationToken);
    }

    public override int Read(Span<byte> buffer)
    {
        return _wrapped[0].Read(buffer);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _wrapped[0].Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        if (value != _length)
        {
            throw new InvalidOperationException("Changing the stream length is not permitted for mirrored streams");
        }
    }

    public override void Clear(int count)
    {
        var pos = _wrapped[0].Position;

        if (pos + count > _length)
        {
            throw new IOException("Attempt to clear beyond end of mirrored stream");
        }

        foreach (var stream in _wrapped)
        {
            stream.Position = pos;
            stream.Clear(count);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var pos = _wrapped[0].Position;

        if (pos + count > _length)
        {
            throw new IOException("Attempt to write beyond end of mirrored stream");
        }

        foreach (var stream in _wrapped)
        {
            stream.Position = pos;
            stream.Write(buffer, offset, count);
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var pos = _wrapped[0].Position;

        if (pos + count > _length)
        {
            throw new IOException("Attempt to write beyond end of mirrored stream");
        }

        return Task.WhenAll(_wrapped.Select(stream =>
        {
            stream.Position = pos;
            return stream.WriteAsync(buffer, offset, count, cancellationToken);
        }));
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var pos = _wrapped[0].Position;

        if (pos + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of mirrored stream");
        }

        return new(Task.WhenAll(_wrapped.Select(stream =>
        {
            stream.Position = pos;
            return stream.WriteAsync(buffer, cancellationToken).AsTask();
        })));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var pos = _wrapped[0].Position;

        if (pos + buffer.Length > _length)
        {
            throw new IOException("Attempt to write beyond end of mirrored stream");
        }

        foreach (var stream in _wrapped)
        {
            stream.Position = pos;
            stream.Write(buffer);
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

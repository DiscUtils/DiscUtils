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
/// The concatenation of multiple streams (read-only, for now).
/// </summary>
public class ConcatStream : SparseStream
{
    private readonly bool _canWrite;
    private readonly Ownership _ownsStreams;

    private long _position;
    private List<SparseStream> _streams;

    public ConcatStream(Ownership ownsStreams, IEnumerable<SparseStream> streams)
    {
        _ownsStreams = ownsStreams;
        _streams = [.. streams];

        // Only allow writes if all streams can be written
        _canWrite = true;
        foreach (var stream in streams)
        {
            if (!stream.CanWrite)
            {
                _canWrite = false;
            }
        }
    }

    public override bool CanRead => _streams is not null;

    public override bool CanSeek => _streams is not null;

    public override bool CanWrite => _streams is not null && _canWrite;

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this))
        {
            return virtualPosition;
        }

        var activeStreamIndex = GetStream(virtualPosition, out var activeStreamStartPos);

        var activeStream = _streams[activeStreamIndex];

        var basePosition = virtualPosition - activeStreamStartPos;

        return activeStream.GetPositionInBaseStream(baseStream, basePosition);
    }

    public override IEnumerable<StreamExtent> Extents
    {
        get
        {
            CheckDisposed();

            long pos = 0;
            for (var i = 0; i < _streams.Count; ++i)
            {
                foreach (var extent in _streams[i].Extents)
                {
                    yield return new StreamExtent(extent.Start + pos, extent.Length);
                }

                pos += _streams[i].Length;
            }
        }
    }

    public override long Length
    {
        get
        {
            CheckDisposed();
            long length = 0;
            for (var i = 0; i < _streams.Count; ++i)
            {
                length += _streams[i].Length;
            }

            return length;
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
            _position = value;
        }
    }

    public override void Flush()
    {
        CheckDisposed();
        for (var i = 0; i < _streams.Count; ++i)
        {
            _streams[i].Flush();
        }
    }

    public override void Flush(bool flushToDisk)
    {
        CheckDisposed();
        for (var i = 0; i < _streams.Count; ++i)
        {
            _streams[i].Flush(flushToDisk);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        CheckDisposed();

        var totalRead = 0;
        int numRead;
        do
        {
            var activeStream = GetActiveStream(out var activeStreamStartPos);

            _streams[activeStream].Position = _position - activeStreamStartPos;

            numRead = _streams[activeStream].Read(buffer, offset + totalRead, count - totalRead);

            totalRead += numRead;
            _position += numRead;
        } while (numRead != 0);

        return totalRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckDisposed();

        var totalRead = 0;
        var numRead = 0;

        do
        {
            long activeStreamStartPos;
            var activeStream = GetActiveStream(out activeStreamStartPos);

            _streams[activeStream].Position = _position - activeStreamStartPos;

            numRead = await _streams[activeStream].ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);

            totalRead += numRead;
            _position += numRead;
        } while (numRead != 0);

        return totalRead;
    }

    public override int Read(Span<byte> buffer)
    {
        CheckDisposed();

        var totalRead = 0;
        var numRead = 0;

        do
        {
            long activeStreamStartPos;
            var activeStream = GetActiveStream(out activeStreamStartPos);

            _streams[activeStream].Position = _position - activeStreamStartPos;

            numRead = _streams[activeStream].Read(buffer[totalRead..]);

            totalRead += numRead;
            _position += numRead;
        } while (numRead != 0);

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
            effectiveOffset += Length;
        }

        if (effectiveOffset < 0)
        {
            throw new IOException("Attempt to move before beginning of disk");
        }

        Position = effectiveOffset;
        return Position;
    }

    public override void SetLength(long value)
    {
        CheckDisposed();

        var lastStream = GetStream(Length, out var lastStreamOffset);
        if (value < lastStreamOffset)
        {
            throw new IOException($"Unable to reduce stream length to less than {lastStreamOffset}");
        }

        _streams[lastStream].SetLength(value - lastStreamOffset);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckDisposed();

        var totalWritten = 0;
        while (totalWritten != count)
        {
            // Offset of the stream = streamOffset
            var streamIdx = GetActiveStream(out var streamOffset);

            // Offset within the stream = streamPos
            var streamPos = _position - streamOffset;
            _streams[streamIdx].Position = streamPos;

            // Write (limited to the stream's length), except for final stream - that may be
            // extendable
            int numToWrite;
            if (streamIdx == _streams.Count - 1)
            {
                numToWrite = count - totalWritten;
            }
            else
            {
                numToWrite = (int)Math.Min(count - totalWritten, _streams[streamIdx].Length - streamPos);
            }

            _streams[streamIdx].Write(buffer, offset + totalWritten, numToWrite);

            totalWritten += numToWrite;
            _position += numToWrite;
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckDisposed();

        var totalWritten = 0;
        while (totalWritten != buffer.Length)
        {
            // Offset of the stream = streamOffset
            long streamOffset;
            var streamIdx = GetActiveStream(out streamOffset);

            // Offset within the stream = streamPos
            var streamPos = _position - streamOffset;
            _streams[streamIdx].Position = streamPos;

            // Write (limited to the stream's length), except for final stream - that may be
            // extendable
            int numToWrite;
            if (streamIdx == _streams.Count - 1)
            {
                numToWrite = buffer.Length - totalWritten;
            }
            else
            {
                numToWrite = (int)Math.Min(buffer.Length - totalWritten, _streams[streamIdx].Length - streamPos);
            }

            await _streams[streamIdx].WriteAsync(buffer.Slice(totalWritten, numToWrite), cancellationToken).ConfigureAwait(false);

            totalWritten += numToWrite;
            _position += numToWrite;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CheckDisposed();

        var totalWritten = 0;
        while (totalWritten != buffer.Length)
        {
            // Offset of the stream = streamOffset
            long streamOffset;
            var streamIdx = GetActiveStream(out streamOffset);

            // Offset within the stream = streamPos
            var streamPos = _position - streamOffset;
            _streams[streamIdx].Position = streamPos;

            // Write (limited to the stream's length), except for final stream - that may be
            // extendable
            int numToWrite;
            if (streamIdx == _streams.Count - 1)
            {
                numToWrite = buffer.Length - totalWritten;
            }
            else
            {
                numToWrite = (int)Math.Min(buffer.Length - totalWritten, _streams[streamIdx].Length - streamPos);
            }

            _streams[streamIdx].Write(buffer.Slice(totalWritten, numToWrite));

            totalWritten += numToWrite;
            _position += numToWrite;
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && _streams != null)
            {
                foreach (var stream in _streams)
                {
                    if (_ownsStreams == Ownership.Dispose)
                    {
                        stream.Dispose();
                    }
                    else if (stream.CanWrite)
                    {
                        stream.Flush();
                    }
                }

                _streams = null!;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private int GetActiveStream(out long startPos)
    {
        return GetStream(_position, out startPos);
    }

    private int GetStream(long targetPos, out long streamStartPos)
    {
        // Find the stream that _position is within
        streamStartPos = 0;
        var focusStream = 0;
        while (focusStream < _streams.Count - 1 && streamStartPos + _streams[focusStream].Length <= targetPos)
        {
            streamStartPos += _streams[focusStream].Length;
            focusStream++;
        }

        return focusStream;
    }

    private void CheckDisposed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_streams is null, this);
#else
        if (_streams == null)
        {
            throw new ObjectDisposedException("ConcatStream");
        }
#endif
    }
}

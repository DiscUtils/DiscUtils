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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiscUtils.Streams;

/// <summary>
/// A wrapper stream that enables you to take a snapshot, pushing changes into a side buffer.
/// </summary>
/// <remarks>Once a snapshot is taken, you can discard subsequent changes or merge them back
/// into the wrapped stream.</remarks>
public sealed class SnapshotStream : SparseStream
{
    private Stream _baseStream;

    private readonly Ownership _baseStreamOwnership;

    /// <summary>
    /// Records which byte ranges in diffStream hold changes.
    /// </summary>
    /// <remarks>Can't use _diffStream's own tracking because that's based on it's
    /// internal block size, not on the _actual_ bytes stored.</remarks>
    private List<StreamExtent>? _diffExtents;

    /// <summary>
    /// Captures changes to the base stream (when enabled).
    /// </summary>
    private SparseMemoryStream? _diffStream;

    /// <summary>
    /// Indicates that no writes should be permitted.
    /// </summary>
    private bool _frozen;

    private long _position;

    /// <summary>
    /// The saved stream position (if the diffStream is active).
    /// </summary>
    private long _savedPosition;

    /// <summary>
    /// Initializes a new instance of the SnapshotStream class.
    /// </summary>
    /// <param name="baseStream">The stream to wrap.</param>
    /// <param name="owns">Indicates if this stream should control the lifetime of baseStream.</param>
    public SnapshotStream(Stream baseStream, Ownership owns)
    {
        _baseStream = baseStream;
        _baseStreamOwnership = owns;
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this)
            || _baseStream is not CompatibilityStream baseCompatStream)
        {
            return virtualPosition;
        }

        return baseCompatStream.GetPositionInBaseStream(baseStream, virtualPosition);
    }

    /// <summary>
    /// Gets an indication as to whether the stream can be read.
    /// </summary>
    public override bool CanRead => _baseStream.CanRead;

    /// <summary>
    /// Gets an indication as to whether the stream position can be changed.
    /// </summary>
    public override bool CanSeek => _baseStream.CanSeek;

    /// <summary>
    /// Gets an indication as to whether the stream can be written to.
    /// </summary>
    /// <remarks>This property is orthogonal to Freezing/Thawing, it's
    /// perfectly possible for a stream to be frozen and this method
    /// return <c>true</c>.</remarks>
    public override bool CanWrite => !_frozen && (_diffStream != null || _baseStream.CanWrite);

    /// <summary>
    /// Returns an enumeration over the parts of the stream that contain real data.
    /// </summary>
    public override IEnumerable<StreamExtent> Extents
    {
        get
        {
            if (_baseStream is not SparseStream sparseBase)
            {
                return SingleValueEnumerable.Get(new StreamExtent(0, Length));
            }

            if (_diffExtents is null)
            {
                return sparseBase.Extents;
            }

            return StreamExtent.Union(sparseBase.Extents, _diffExtents);
        }
    }

    /// <summary>
    /// Gets the length of the stream.
    /// </summary>
    public override long Length
    {
        get
        {
            if (_diffStream != null)
            {
                return _diffStream.Length;
            }

            return _baseStream.Length;
        }
    }

    /// <summary>
    /// Gets and sets the current stream position.
    /// </summary>
    public override long Position
    {
        get => _position;

        set => _position = value;
    }

    /// <summary>
    /// Prevents any write operations to the stream.
    /// </summary>
    /// <remarks>Useful to prevent changes whilst inspecting the stream.</remarks>
    public void Freeze()
    {
        _frozen = true;
    }

    /// <summary>
    /// Re-permits write operations to the stream.
    /// </summary>
    public void Thaw()
    {
        _frozen = false;
    }

    /// <summary>
    /// Takes a snapshot of the current stream contents.
    /// </summary>
    public void Snapshot()
    {
        if (_diffStream != null)
        {
            throw new InvalidOperationException("Already have a snapshot");
        }

        _savedPosition = _position;

        _diffExtents = [];
        _diffStream = new SparseMemoryStream();
        _diffStream.SetLength(_baseStream.Length);
    }

    /// <summary>
    /// Reverts to a previous snapshot, discarding any changes made to the stream.
    /// </summary>
    public void RevertToSnapshot()
    {
        if (_diffStream is null || _diffExtents is null)
        {
            throw new InvalidOperationException("No snapshot");
        }

        _diffStream = null;
        _diffExtents = null;

        _position = _savedPosition;
    }

    /// <summary>
    /// Discards the snapshot any changes made after the snapshot was taken are kept.
    /// </summary>
    public void ForgetSnapshot()
    {
        if (_diffStream is null || _diffExtents is null)
        {
            throw new InvalidOperationException("No snapshot");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            foreach (var extent in _diffExtents)
            {
                _diffStream.Position = extent.Start;
                _baseStream.Position = extent.Start;

                var totalRead = 0;
                while (totalRead < extent.Length)
                {
                    var toRead = (int)Math.Min(extent.Length - totalRead, 8192);

                    var read = _diffStream.Read(buffer, 0, toRead);
                    _baseStream.Write(buffer, 0, read);

                    totalRead += read;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        _diffStream = null;
        _diffExtents = null;
    }

    /// <summary>
    /// Flushes the stream.
    /// </summary>
    public override void Flush()
    {
        CheckFrozen();

        _baseStream.Flush();
    }

    public override void Flush(bool flushToDisk)
    {
        CheckFrozen();

        FlushWrappedStream(_baseStream, flushToDisk);
    }

    /// <summary>
    /// Flushes the stream.
    /// </summary>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        CheckFrozen();

        return _baseStream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Reads data from the stream.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <param name="offset">The buffer offset to start from.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The number of bytes read.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        int numRead;

        if (_diffStream is null || _diffExtents is null)
        {
            _baseStream.Position = _position;
            numRead = _baseStream.Read(buffer, offset, count);
        }
        else
        {
            if (_position > _diffStream.Length)
            {
                throw new IOException("Attempt to read beyond end of file");
            }

            var toRead = (int)Math.Min(count, _diffStream.Length - _position);

            // If the read is within the base stream's range, then touch it first to get the
            // (potentially) stale data.
            if (_position < _baseStream.Length)
            {
                var baseToRead = (int)Math.Min(toRead, _baseStream.Length - _position);
                _baseStream.Position = _position;

                var totalBaseRead = 0;
                while (totalBaseRead < baseToRead)
                {
                    totalBaseRead += _baseStream.Read(buffer, offset + totalBaseRead, baseToRead - totalBaseRead);
                }
            }

            // Now overlay any data from the overlay stream (if any)
            var overlayExtents = StreamExtent.Intersect(_diffExtents,
                new StreamExtent(_position, toRead));
            foreach (var extent in overlayExtents)
            {
                _diffStream.Position = extent.Start;
                var overlayNumRead = 0;
                while (overlayNumRead < extent.Length)
                {
                    overlayNumRead += _diffStream.Read(
                        buffer,
                        (int)(offset + (extent.Start - _position) + overlayNumRead),
                        (int)(extent.Length - overlayNumRead));
                }
            }

            numRead = toRead;
        }

        _position += numRead;

        return numRead;
    }

    /// <summary>
    /// Reads data from the stream.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The number of bytes read.</returns>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int numRead;

        if (_diffStream is null || _diffExtents is null)
        {
            _baseStream.Position = _position;
            numRead = await _baseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (_position > _diffStream.Length)
            {
                throw new IOException("Attempt to read beyond end of file");
            }

            var toRead = (int)Math.Min(buffer.Length, _diffStream.Length - _position);

            // If the read is within the base stream's range, then touch it first to get the
            // (potentially) stale data.
            if (_position < _baseStream.Length)
            {
                var baseToRead = (int)Math.Min(toRead, _baseStream.Length - _position);
                _baseStream.Position = _position;

                var totalBaseRead = 0;
                while (totalBaseRead < baseToRead)
                {
                    totalBaseRead += await _baseStream.ReadAsync(buffer[totalBaseRead..baseToRead], cancellationToken).ConfigureAwait(false);
                }
            }

            // Now overlay any data from the overlay stream (if any)
            var overlayExtents = StreamExtent.Intersect(_diffExtents,
                new StreamExtent(_position, toRead));
            foreach (var extent in overlayExtents)
            {
                _diffStream.Position = extent.Start;
                var overlayNumRead = 0;
                while (overlayNumRead < extent.Length)
                {
                    overlayNumRead += await _diffStream.ReadAsync(
                        buffer.Slice(
                        (int)((extent.Start - _position) + overlayNumRead),
                        (int)(extent.Length - overlayNumRead)), cancellationToken).ConfigureAwait(false);
                }
            }

            numRead = toRead;
        }

        _position += numRead;

        return numRead;
    }

    /// <summary>
    /// Reads data from the stream.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns>The number of bytes read.</returns>
    public override int Read(Span<byte> buffer)
    {
        int numRead;

        if (_diffStream is null || _diffExtents is null)
        {
            _baseStream.Position = _position;
            numRead = _baseStream.Read(buffer);
        }
        else
        {
            if (_position > _diffStream.Length)
            {
                throw new IOException("Attempt to read beyond end of file");
            }

            var toRead = (int)Math.Min(buffer.Length, _diffStream.Length - _position);

            // If the read is within the base stream's range, then touch it first to get the
            // (potentially) stale data.
            if (_position < _baseStream.Length)
            {
                var baseToRead = (int)Math.Min(toRead, _baseStream.Length - _position);
                _baseStream.Position = _position;

                var totalBaseRead = 0;
                while (totalBaseRead < baseToRead)
                {
                    totalBaseRead += _baseStream.Read(buffer[totalBaseRead..baseToRead]);
                }
            }

            // Now overlay any data from the overlay stream (if any)
            var overlayExtents = StreamExtent.Intersect(_diffExtents,
                new StreamExtent(_position, toRead));
            foreach (var extent in overlayExtents)
            {
                _diffStream.Position = extent.Start;
                var overlayNumRead = 0;
                while (overlayNumRead < extent.Length)
                {
                    overlayNumRead += _diffStream.Read(
                        buffer.Slice(
                        (int)((extent.Start - _position) + overlayNumRead),
                        (int)(extent.Length - overlayNumRead)));
                }
            }

            numRead = toRead;
        }

        _position += numRead;

        return numRead;
    }

    /// <summary>
    /// Moves the stream position.
    /// </summary>
    /// <param name="offset">The origin-relative location.</param>
    /// <param name="origin">The base location.</param>
    /// <returns>The new absolute stream position.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        CheckFrozen();

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

        _position = effectiveOffset;
        return _position;
    }

    /// <summary>
    /// Sets the length of the stream.
    /// </summary>
    /// <param name="value">The new length.</param>
    public override void SetLength(long value)
    {
        CheckFrozen();

        if (_diffStream != null)
        {
            _diffStream.SetLength(value);
        }
        else
        {
            _baseStream.SetLength(value);
        }
    }

    /// <summary>
    /// Writes data to the stream at the current location.
    /// </summary>
    /// <param name="buffer">The data to write.</param>
    /// <param name="offset">The first byte to write from buffer.</param>
    /// <param name="count">The number of bytes to write.</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckFrozen();

        if (_diffStream is not null && _diffExtents is not null)
        {
            _diffStream.Position = _position;
            _diffStream.Write(buffer, offset, count);

            // Beware of Linq's delayed model - force execution now by placing into a list.
            // Without this, large execution chains can build up (v. slow) and potential for stack overflow.
            _diffExtents =
                [.. StreamExtent.Union(_diffExtents, new StreamExtent(_position, count))];

            _position += count;
        }
        else
        {
            _baseStream.Position = _position;
            _baseStream.Write(buffer, offset, count);
            _position += count;
        }
    }

    /// <summary>
    /// Writes data to the stream at the current location.
    /// </summary>
    /// <param name="buffer">The data to write.</param>
    /// <param name="cancellationToken"></param>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        CheckFrozen();

        if (_diffStream is not null && _diffExtents is not null)
        {
            _diffStream.Position = _position;
            await _diffStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

            // Beware of Linq's delayed model - force execution now by placing into a list.
            // Without this, large execution chains can build up (v. slow) and potential for stack overflow.
            _diffExtents =
                [.. StreamExtent.Union(_diffExtents, new StreamExtent(_position, buffer.Length))];

            _position += buffer.Length;
        }
        else
        {
            _baseStream.Position = _position;
            await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position += buffer.Length;
        }
    }

    /// <summary>
    /// Writes data to the stream at the current location.
    /// </summary>
    /// <param name="buffer">The data to write.</param>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CheckFrozen();

        if (_diffStream is not null && _diffExtents is not null)
        {
            _diffStream.Position = _position;
            _diffStream.Write(buffer);

            // Beware of Linq's delayed model - force execution now by placing into a list.
            // Without this, large execution chains can build up (v. slow) and potential for stack overflow.
            _diffExtents =
                [.. StreamExtent.Union(_diffExtents, new StreamExtent(_position, buffer.Length))];

            _position += buffer.Length;
        }
        else
        {
            _baseStream.Position = _position;
            _baseStream.Write(buffer);
            _position += buffer.Length;
        }
    }

    /// <summary>
    /// Disposes of this instance.
    /// </summary>
    /// <param name="disposing"><c>true</c> if called from Dispose(), else <c>false</c>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_baseStream != null)
            {
                if (_baseStreamOwnership == Ownership.Dispose)
                {
                    _baseStream.Dispose();
                }
                else if (_baseStream.CanWrite)
                {
                    _baseStream.Flush();
                }
            }

            _baseStream = null!;

            _diffStream?.Dispose();

            _diffStream = null;

            _diffExtents = null;
        }

        base.Dispose(disposing);
    }

    private void CheckFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("The stream is frozen");
        }
    }
}

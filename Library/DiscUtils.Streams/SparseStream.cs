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
/// Represents a sparse stream.
/// </summary>
/// <remarks>A sparse stream is a logically contiguous stream where some parts of the stream
/// aren't stored.  The unstored parts are implicitly zero-byte ranges.</remarks>
public abstract class SparseStream : CompatibilityStream
{
    public event EventHandler? Disposing;

    public event EventHandler? Disposed;

    /// <summary>
    /// Gets the parts of the stream that are stored.
    /// </summary>
    /// <remarks>This may be an empty enumeration if all bytes are zero.</remarks>
    public abstract IEnumerable<StreamExtent> Extents { get; }

    /// <summary>
    /// Flushes pending changes, requesting synchronization with backing storage when supported.
    /// </summary>
    /// <param name="flushToDisk">Whether to request a disk flush from supporting backing streams.</param>
    /// <remarks>
    /// The default implementation calls <see cref="Flush()"/>. A stream without a stronger flush
    /// contract cannot guarantee that its data has reached durable storage.
    /// </remarks>
    public virtual void Flush(bool flushToDisk) => Flush();

    /// <summary>Forwards a flush without bypassing intervening sparse-stream wrappers.</summary>
    protected static void FlushWrappedStream(Stream wrapped, bool flushToDisk)
    {
        if (wrapped is SparseStream sparse)
        {
            sparse.Flush(flushToDisk);
        }
        else if (wrapped is FileStream file)
        {
            file.Flush(flushToDisk);
        }
        else
        {
            wrapped.Flush();
        }
    }

    /// <summary>
    /// Converts any stream into a sparse stream.
    /// </summary>
    /// <param name="stream">The stream to convert.</param>
    /// <param name="takeOwnership"><c>true</c> to have the new stream dispose the wrapped
    /// stream when it is disposed.</param>
    /// <returns>A sparse stream.</returns>
    /// <remarks>The returned stream has the entire wrapped stream as a
    /// single extent.</remarks>
    public static SparseStream FromStream(Stream stream, Ownership takeOwnership)
    {
        return new SparseWrapperStream(stream, takeOwnership, extents: null);
    }

    /// <summary>
    /// Converts any stream into a sparse stream.
    /// </summary>
    /// <param name="stream">The stream to convert.</param>
    /// <param name="takeOwnership"><c>true</c> to have the new stream dispose the wrapped
    /// stream when it is disposed.</param>
    /// <param name="extents">The set of extents actually stored in <c>stream</c>.</param>
    /// <returns>A sparse stream.</returns>
    /// <remarks>The returned stream has the entire wrapped stream as a
    /// single extent.</remarks>
    public static SparseStream FromStream(Stream stream, Ownership takeOwnership, IEnumerable<StreamExtent> extents)
    {
        return new SparseWrapperStream(stream, takeOwnership, extents);
    }

    /// <summary>
    /// Efficiently pumps data from a sparse stream to another stream.
    /// </summary>
    /// <param name="inStream">The sparse stream to pump from.</param>
    /// <param name="outStream">The stream to pump to.</param>
    /// <remarks><paramref name="outStream"/> must support seeking.</remarks>
    public static void Pump(Stream inStream, Stream outStream)
    {
        Pump(inStream, outStream, Sizes.Sector);
    }

    /// <summary>
    /// Efficiently pumps data from a sparse stream to another stream.
    /// </summary>
    /// <param name="inStream">The stream to pump from.</param>
    /// <param name="outStream">The stream to pump to.</param>
    /// <param name="chunkSize">The smallest sequence of zero bytes that will be skipped when writing to <paramref name="outStream"/>.</param>
    /// <remarks><paramref name="outStream"/> must support seeking.</remarks>
    public static void Pump(Stream inStream, Stream outStream, int chunkSize)
    {
        var pump = new StreamPump(inStream, outStream, chunkSize);
        pump.Run();
    }

    /// <summary>
    /// Wraps a sparse stream in a read-only wrapper, preventing modification.
    /// </summary>
    /// <param name="toWrap">The stream to make read-only.</param>
    /// <param name="ownership">Whether to transfer responsibility for calling Dispose on <c>toWrap</c>.</param>
    /// <returns>The read-only stream.</returns>
    public static SparseStream ReadOnly(SparseStream toWrap, Ownership ownership)
    {
        if (ownership == Ownership.Dispose && toWrap is SparseReadOnlyWrapperStream)
        {
            return toWrap;
        }

        return new SparseReadOnlyWrapperStream(toWrap, ownership);
    }

    /// <summary>
    /// Wraps a sparse stream in a synchronized wrapper, ensuring that all operations are thread-safe.
    /// </summary>
    /// <param name="toWrap">The stream to make synchronized.</param>
    /// <param name="ownership">Whether to transfer responsibility for calling Dispose on <c>toWrap</c>.</param>
    /// <returns>The synchronized stream.</returns>
    public static SparseStream Synchronized(SparseStream toWrap, Ownership ownership)
    {
        if (ownership == Ownership.Dispose && toWrap is SynchronizedSparseStream)
        {
            return toWrap;
        }
        
        return new SynchronizedSparseStream(toWrap, ownership);
    }

    /// <summary>
    /// Clears bytes from the stream.
    /// </summary>
    /// <param name="count">The number of bytes (from the current position) to clear.</param>
    /// <remarks>
    /// <para>Logically equivalent to writing <c>count</c> null/zero bytes to the stream, some
    /// implementations determine that some (or all) of the range indicated is not actually
    /// stored.  There is no direct, automatic, correspondence to clearing bytes and them
    /// not being represented as an 'extent' - for example, the implementation of the underlying
    /// stream may not permit fine-grained extent storage.</para>
    /// <para>It is always safe to call this method to 'zero-out' a section of a stream, regardless of
    /// the underlying stream implementation.</para>
    /// </remarks>
    public virtual void Clear(int count)
    {
        byte[]? array = null;

        var buffer = count <= 512
            ? stackalloc byte[count]
            : (array = ArrayPool<byte>.Shared.Rent(count)).AsSpan(0, count);

        try
        {
            buffer.Clear();
            Write(buffer);
        }
        finally
        {
            if (array is not null)
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }

    /// <summary>
    /// Clears bytes from the stream.
    /// </summary>
    /// <param name="count">The number of bytes (from the current position) to clear.</param>
    /// <param name="cancellationToken"></param>
    /// <remarks>
    /// <para>Logically equivalent to writing <c>count</c> null/zero bytes to the stream, some
    /// implementations determine that some (or all) of the range indicated is not actually
    /// stored.  There is no direct, automatic, correspondence to clearing bytes and them
    /// not being represented as an 'extent' - for example, the implementation of the underlying
    /// stream may not permit fine-grained extent storage.</para>
    /// <para>It is always safe to call this method to 'zero-out' a section of a stream, regardless of
    /// the underlying stream implementation.</para>
    /// </remarks>
    public virtual async ValueTask ClearAsync(int count, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            Array.Clear(buffer, 0, count);
            await WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Gets the parts of a stream that are stored, within a specified range.
    /// </summary>
    /// <param name="start">The offset of the first byte of interest.</param>
    /// <param name="count">The number of bytes of interest.</param>
    /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
    public virtual IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
    {
        return StreamExtent.Intersect(Extents, new StreamExtent(start, count));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Read(buffer.Span));

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return default;
    }

    public abstract class ReadOnlySparseStream : SparseStream
    {
        public sealed override bool CanWrite => false;
        public sealed override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Attempt to write to read-only stream");
        public sealed override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Attempt to write to read-only stream");
        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new InvalidOperationException("Attempt to write to read-only stream");
        public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Attempt to write to read-only stream");
        public sealed override void WriteByte(byte value) => throw new InvalidOperationException("Attempt to write to read-only stream");
        public sealed override void Flush() { }
        public sealed override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public sealed override void SetLength(long value) => throw new InvalidOperationException("Attempt to change length of read-only stream");
    }

    private class SparseReadOnlyWrapperStream : ReadOnlySparseStream
    {
        private readonly Ownership _ownsWrapped;
        private SparseStream _wrapped;

        public SparseReadOnlyWrapperStream(SparseStream wrapped, Ownership ownsWrapped)
        {
            _wrapped = wrapped;
            _ownsWrapped = ownsWrapped;
        }

        public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
        {
            if (ReferenceEquals(baseStream, this))
            {
                return virtualPosition;
            }

            return _wrapped.GetPositionInBaseStream(baseStream, virtualPosition);
        }

        public override bool CanRead
        {
            get
            {
                CheckDisposed();

                return _wrapped.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                CheckDisposed();

                return _wrapped.CanSeek;
            }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckDisposed();

                return _wrapped.Extents;
            }
        }

        public override long Length
        {
            get
            {
                CheckDisposed();

                return _wrapped.Length;
            }
        }

        public override long Position
        {
            get
            {
                CheckDisposed();

                return _wrapped.Position;
            }

            set
            {
                CheckDisposed();

                _wrapped.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            return _wrapped.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDisposed();

            return _wrapped.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            CheckDisposed();

            return _wrapped.ReadAsync(buffer, cancellationToken);
        }

        public override int Read(Span<byte> buffer)
        {
            CheckDisposed();

            return _wrapped.Read(buffer);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            return _wrapped.Seek(offset, origin);
        }

        private void CheckDisposed()
        {
#if NET7_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_wrapped is null, this);
#else
            if (_wrapped is null)
            {
                throw new ObjectDisposedException(nameof(SparseWrapperStream));
            }
#endif
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Disposing?.Invoke(this, EventArgs.Empty);

                if (disposing && _wrapped != null)
                {
                    if (_ownsWrapped == Ownership.Dispose)
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
            finally
            {
                base.Dispose(disposing);

                if (disposing)
                {
                    Disposed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    private class SparseWrapperStream : SparseStream
    {
        private readonly List<StreamExtent>? _extents;
        private readonly Ownership _ownsWrapped;
        private Stream _wrapped;

        public SparseWrapperStream(Stream wrapped, Ownership ownsWrapped, IEnumerable<StreamExtent>? extents)
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(wrapped);
            _wrapped = wrapped;
#else
            _wrapped = wrapped
                ?? throw new ArgumentNullException(nameof(wrapped));
#endif

            _ownsWrapped = ownsWrapped;

            if (extents != null)
            {
                _extents = [.. extents];
            }
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

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckDisposed();

                if (_extents != null)
                {
                    return _extents;
                }

                if (_wrapped is SparseStream wrappedAsSparse)
                {
                    return wrappedAsSparse.Extents;
                }

                return SingleValueEnumerable.Get(new StreamExtent(0, _wrapped.Length));
            }
        }

        public override long Length
        {
            get
            {
                CheckDisposed();

                return _wrapped.Length;
            }
        }

        public override long Position
        {
            get
            {
                CheckDisposed();

                return _wrapped.Position;
            }

            set
            {
                CheckDisposed();

                _wrapped.Position = value;
            }
        }

        public override void Flush()
        {
            CheckDisposed();

            if (CanWrite)
            {
                _wrapped.Flush();
            }
        }

        public override void Flush(bool flushToDisk)
        {
            CheckDisposed();

            if (CanWrite)
            {
                FlushWrappedStream(_wrapped, flushToDisk);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            return _wrapped.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDisposed();

            return _wrapped.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            CheckDisposed();

            return _wrapped.ReadAsync(buffer, cancellationToken);
        }

        public override int Read(Span<byte> buffer)
        {
            CheckDisposed();

            return _wrapped.Read(buffer);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            return _wrapped.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            CheckDisposed();

            _wrapped.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (_extents != null)
            {
                throw new InvalidOperationException("Attempt to write to stream with explicit extents");
            }

            _wrapped.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (_extents != null)
            {
                throw new InvalidOperationException("Attempt to write to stream with explicit extents");
            }

            return _wrapped.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (_extents != null)
            {
                throw new InvalidOperationException("Attempt to write to stream with explicit extents");
            }

            return _wrapped.WriteAsync(buffer, cancellationToken);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            CheckDisposed();

            if (_extents != null)
            {
                throw new InvalidOperationException("Attempt to write to stream with explicit extents");
            }

            _wrapped.Write(buffer);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            CheckDisposed();

            return _wrapped.FlushAsync(cancellationToken);
        }

        private void CheckDisposed()
        {
#if NET7_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_wrapped is null, this);
#else
            if (_wrapped is null)
            {
                throw new ObjectDisposedException(nameof(SparseWrapperStream));
            }
#endif
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Disposing?.Invoke(this, EventArgs.Empty);

                if (disposing && _wrapped != null)
                {
                    if (_ownsWrapped == Ownership.Dispose)
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
            finally
            {
                base.Dispose(disposing);

                if (disposing)
                {
                    Disposed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    private sealed class SynchronizedSparseStream(SparseStream content, Ownership ownership) : SparseStream
    {
        public SparseStream WrappedStream => content;

        public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
        {
            if (ReferenceEquals(baseStream, this))
            {
                return virtualPosition;
            }

            return content.GetPositionInBaseStream(baseStream, virtualPosition);
        }

        private readonly SemaphoreSlim _sync = new(1);

        public override IEnumerable<StreamExtent> Extents => content.Extents;

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count) => content.GetExtentsInRange(start, count);

        public override bool CanRead => content.CanRead;

        public override bool CanSeek => content.CanSeek;

        public override bool CanWrite => content.CanWrite;

        public override long Length => content.Length;

        private long _position;

        public override long Position
        {
            get => _position;

            set
            {
                _sync.Wait();
                
                try
                {
                    content.Position = value;
                    _position = value;
                }
                finally
                {
                    _sync.Release();
                }
            }
        }

        public override void Flush()
        {
            _sync.Wait();

            try
            {
                content.Flush();
            }
            finally
            {
                _sync.Release();
            }
        }

        public override void Flush(bool flushToDisk)
        {
            _sync.Wait();

            try
            {
                content.Flush(flushToDisk);
            }
            finally
            {
                _sync.Release();
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await content.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sync.Release();
            }
        }

        public override int Read(Span<byte> buffer)
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                var r = content.Read(buffer);
                _position += r;
                return r;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                var r = content.Read(buffer, offset, count);
                _position += r;
                return r;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                content.Position = _position;                
                var r = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                _position += r;
                return r;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override int ReadByte()
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                var c = content.ReadByte();
                _position += c == -1 ? 0 : 1;
                return c;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override int ReadTimeout { get => content.ReadTimeout; set => content.ReadTimeout = value; }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;

                case SeekOrigin.Current:
                    Position += offset;
                    break;

                case SeekOrigin.End:
                    Position = Length + offset;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            _sync.Wait();

            try
            {
                content.SetLength(value);
            }
            finally
            {
                _sync.Release();
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                content.Write(buffer);
                _position += buffer.Length;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                content.Write(buffer, offset, count);
                _position += count;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                content.Position = _position;
                await content.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                _position += buffer.Length;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override void WriteByte(byte value)
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                content.WriteByte(value);
                _position++;
            }
            finally
            {
                _sync.Release();
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        public override void CopyTo(Stream destination, int bufferSize)
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                content.CopyTo(destination, bufferSize);
                _position = content.Position;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                content.Position = _position;
                await content.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
                _position = content.Position;
            }
            finally
            {
                _sync.Release();
            }
        }
#endif

        public override int WriteTimeout { get => content.WriteTimeout; set => content.WriteTimeout = value; }

        public override bool CanTimeout => content.CanTimeout;

        public override void Clear(int count)
        {
            _sync.Wait();

            try
            {
                content.Position = _position;
                content.Clear(count);
                _position += count;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override async ValueTask ClearAsync(int count, CancellationToken cancellationToken)
        {
            await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                content.Position = _position;
                await content.ClearAsync(count, cancellationToken).ConfigureAwait(false);
                _position += count;
            }
            finally
            {
                _sync.Release();
            }
        }

        public override void Close()
        {
            if (ownership == Ownership.Dispose)
            {
                _sync.Wait();

                try
                {
                    content.Close();
                }
                finally
                {
                    _sync.Release();
                }
            }
            else if (content.CanWrite)
            {
                _sync.Wait();

                try
                {
                    content.Flush();
                }
                finally
                {
                    _sync.Release();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ownership == Ownership.Dispose)
                {
                    _sync.Wait();

                    try
                    {
                        content.Dispose();
                    }
                    finally
                    {
                        _sync.Release();
                    }
                }
                else if (content.CanWrite)
                {
                    _sync.Wait();

                    try
                    {
                        content.Flush();
                    }
                    finally
                    {
                        _sync.Release();
                    }
                }
            }
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        public override async ValueTask DisposeAsync()
        {
            if (ownership == Ownership.Dispose)
            {
                await _sync.WaitAsync().ConfigureAwait(false);

                try
                {
                    await content.DisposeAsync().ConfigureAwait(false);
                    return;
                }
                finally
                {
                    _sync.Release();
                }
            }

            if (content.CanWrite)
            {
                await _sync.WaitAsync().ConfigureAwait(false);

                try
                {
                    await content.FlushAsync().ConfigureAwait(false);
                    return;
                }
                finally
                {
                    _sync.Release();
                }
            }
        }
#endif

        public override string ToString() => $"Syncrhonized[{content}]";
    }
}

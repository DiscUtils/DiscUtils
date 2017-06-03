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

namespace DiscUtils.Streams
{
    /// <summary>
    /// Represents a sparse stream.
    /// </summary>
    /// <remarks>A sparse stream is a logically contiguous stream where some parts of the stream
    /// aren't stored.  The unstored parts are implicitly zero-byte ranges.</remarks>
    public abstract class SparseStream : Stream
    {
        /// <summary>
        /// Gets the parts of the stream that are stored.
        /// </summary>
        /// <remarks>This may be an empty enumeration if all bytes are zero.</remarks>
        public abstract IEnumerable<StreamExtent> Extents { get; }

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
            return new SparseWrapperStream(stream, takeOwnership, null);
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
            StreamPump pump = new StreamPump(inStream, outStream, chunkSize);
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
            return new SparseReadOnlyWrapperStream(toWrap, ownership);
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
            Write(new byte[count], 0, count);
        }

        /// <summary>
        /// Gets the parts of a stream that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest.</param>
        /// <param name="count">The number of bytes of interest.</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
        public virtual IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return StreamExtent.Intersect(Extents, new[] { new StreamExtent(start, count) });
        }

        private class SparseReadOnlyWrapperStream : SparseStream
        {
            private readonly Ownership _ownsWrapped;
            private SparseStream _wrapped;

            public SparseReadOnlyWrapperStream(SparseStream wrapped, Ownership ownsWrapped)
            {
                _wrapped = wrapped;
                _ownsWrapped = ownsWrapped;
            }

            public override bool CanRead
            {
                get { return _wrapped.CanRead; }
            }

            public override bool CanSeek
            {
                get { return _wrapped.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get { return _wrapped.Extents; }
            }

            public override long Length
            {
                get { return _wrapped.Length; }
            }

            public override long Position
            {
                get { return _wrapped.Position; }

                set { _wrapped.Position = value; }
            }

            public override void Flush() {}

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _wrapped.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _wrapped.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                throw new InvalidOperationException("Attempt to change length of read-only stream");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new InvalidOperationException("Attempt to write to read-only stream");
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing && _ownsWrapped == Ownership.Dispose && _wrapped != null)
                    {
                        _wrapped.Dispose();
                        _wrapped = null;
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        private class SparseWrapperStream : SparseStream
        {
            private readonly List<StreamExtent> _extents;
            private readonly Ownership _ownsWrapped;
            private Stream _wrapped;

            public SparseWrapperStream(Stream wrapped, Ownership ownsWrapped, IEnumerable<StreamExtent> extents)
            {
                _wrapped = wrapped;
                _ownsWrapped = ownsWrapped;
                if (extents != null)
                {
                    _extents = new List<StreamExtent>(extents);
                }
            }

            public override bool CanRead
            {
                get { return _wrapped.CanRead; }
            }

            public override bool CanSeek
            {
                get { return _wrapped.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return _wrapped.CanWrite; }
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get
                {
                    if (_extents != null)
                    {
                        return _extents;
                    }
                    SparseStream wrappedAsSparse = _wrapped as SparseStream;
                    if (wrappedAsSparse != null)
                    {
                        return wrappedAsSparse.Extents;
                    }
                    return new[] { new StreamExtent(0, _wrapped.Length) };
                }
            }

            public override long Length
            {
                get { return _wrapped.Length; }
            }

            public override long Position
            {
                get { return _wrapped.Position; }

                set { _wrapped.Position = value; }
            }

            public override void Flush()
            {
                _wrapped.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _wrapped.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _wrapped.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _wrapped.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (_extents != null)
                {
                    throw new InvalidOperationException("Attempt to write to stream with explicit extents");
                }

                _wrapped.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing && _ownsWrapped == Ownership.Dispose && _wrapped != null)
                    {
                        _wrapped.Dispose();
                        _wrapped = null;
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }
    }
}
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
    /// Provides a thread-safe wrapping around a sparse stream.
    /// </summary>
    /// <remarks>
    /// <para>Streams are inherently not thread-safe (because read/write is not atomic w.r.t. Position).
    /// This method enables multiple 'views' of a stream to be created (each with their own Position), and ensures
    /// only a single operation is executing on the wrapped stream at any time.</para>
    /// <para>This example shows the pattern of use:</para>
    /// <example>
    /// <code>
    /// SparseStream baseStream = ...;
    /// ThreadSafeStream tss = new ThreadSafeStream(baseStream);
    /// for(int i = 0; i &lt; 10; ++i)
    /// {
    ///   SparseStream streamForThread = tss.OpenView();
    /// }
    /// </code>
    /// </example>
    /// <para>This results in 11 streams that can be used in different streams - <c>tss</c> and ten 'views' created from <c>tss</c>.</para>
    /// <para>Note, the stream length cannot be changed.</para>
    /// </remarks>
    public class ThreadSafeStream : SparseStream
    {
        private CommonState _common;
        private readonly bool _ownsCommon;
        private long _position;

        /// <summary>
        /// Initializes a new instance of the ThreadSafeStream class.
        /// </summary>
        /// <param name="toWrap">The stream to wrap.</param>
        /// <remarks>Do not directly modify <c>toWrap</c> after wrapping it, unless the thread-safe views
        /// will no longer be used.</remarks>
        public ThreadSafeStream(SparseStream toWrap)
            : this(toWrap, Ownership.None) {}

        /// <summary>
        /// Initializes a new instance of the ThreadSafeStream class.
        /// </summary>
        /// <param name="toWrap">The stream to wrap.</param>
        /// <param name="ownership">Whether to transfer ownership of <c>toWrap</c> to the new instance.</param>
        /// <remarks>Do not directly modify <c>toWrap</c> after wrapping it, unless the thread-safe views
        /// will no longer be used.</remarks>
        public ThreadSafeStream(SparseStream toWrap, Ownership ownership)
        {
            if (!toWrap.CanSeek)
            {
                throw new ArgumentException("Wrapped stream must support seeking", nameof(toWrap));
            }

            _common = new CommonState
            {
                WrappedStream = toWrap,
                WrappedStreamOwnership = ownership
            };
            _ownsCommon = true;
        }

        private ThreadSafeStream(ThreadSafeStream toClone)
        {
            _common = toClone._common;
            if (_common == null)
            {
                throw new ObjectDisposedException("toClone");
            }
        }

        /// <summary>
        /// Gets a value indicating if this stream supports reads.
        /// </summary>
        public override bool CanRead
        {
            get
            {
                lock (_common)
                {
                    return Wrapped.CanRead;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating if this stream supports seeking (always true).
        /// </summary>
        public override bool CanSeek
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating if this stream supports writes (currently, always false).
        /// </summary>
        public override bool CanWrite
        {
            get
            {
                lock (_common)
                {
                    return Wrapped.CanWrite;
                }
            }
        }

        /// <summary>
        /// Gets the parts of the stream that are stored.
        /// </summary>
        /// <remarks>This may be an empty enumeration if all bytes are zero.</remarks>
        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                lock (_common)
                {
                    return Wrapped.Extents;
                }
            }
        }

        /// <summary>
        /// Gets the length of the stream.
        /// </summary>
        public override long Length
        {
            get
            {
                lock (_common)
                {
                    return Wrapped.Length;
                }
            }
        }

        /// <summary>
        /// Gets the current stream position - each 'view' has it's own Position.
        /// </summary>
        public override long Position
        {
            get { return _position; }

            set { _position = value; }
        }

        private SparseStream Wrapped
        {
            get
            {
                SparseStream wrapped = _common.WrappedStream;
                if (wrapped == null)
                {
                    throw new ObjectDisposedException("ThreadSafeStream");
                }

                return wrapped;
            }
        }

        /// <summary>
        /// Opens a new thread-safe view on the stream.
        /// </summary>
        /// <returns>The new view.</returns>
        public SparseStream OpenView()
        {
            return new ThreadSafeStream(this);
        }

        /// <summary>
        /// Gets the parts of a stream that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest.</param>
        /// <param name="count">The number of bytes of interest.</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            lock (_common)
            {
                return Wrapped.GetExtentsInRange(start, count);
            }
        }

        /// <summary>
        /// Causes the stream to flush all changes.
        /// </summary>
        public override void Flush()
        {
            lock (_common)
            {
                Wrapped.Flush();
            }
        }

        /// <summary>
        /// Reads data from the stream.
        /// </summary>
        /// <param name="buffer">The buffer to fill.</param>
        /// <param name="offset">The first byte in buffer to fill.</param>
        /// <param name="count">The requested number of bytes to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_common)
            {
                SparseStream wrapped = Wrapped;
                wrapped.Position = _position;
                int numRead = wrapped.Read(buffer, offset, count);
                _position += numRead;
                return numRead;
            }
        }

        /// <summary>
        /// Changes the current stream position (each view has it's own Position).
        /// </summary>
        /// <param name="offset">The relative location to move to.</param>
        /// <param name="origin">The origin of the location.</param>
        /// <returns>The new location as an absolute position.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long effectiveOffset = offset;
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
        /// Sets the length of the stream (not supported).
        /// </summary>
        /// <param name="value">The new length.</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Writes data to the stream (not currently supported).
        /// </summary>
        /// <param name="buffer">The data to write.</param>
        /// <param name="offset">The first byte to write.</param>
        /// <param name="count">The number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_common)
            {
                SparseStream wrapped = Wrapped;

                if (_position + count > wrapped.Length)
                {
                    throw new IOException("Attempt to extend stream");
                }

                wrapped.Position = _position;
                wrapped.Write(buffer, offset, count);
                _position += count;
            }
        }

        /// <summary>
        /// Disposes of this instance, invalidating any remaining views.
        /// </summary>
        /// <param name="disposing"><c>true</c> if disposing, lese <c>false</c>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_ownsCommon && _common != null)
                {
                    lock (_common)
                    {
                        if (_common.WrappedStreamOwnership == Ownership.Dispose)
                        {
                            _common.WrappedStream.Dispose();
                        }

                        _common.Dispose();
                    }
                }
            }

            _common = null;
        }

        private sealed class CommonState : IDisposable
        {
            public SparseStream WrappedStream;
            public Ownership WrappedStreamOwnership;

            #region IDisposable Members

            public void Dispose()
            {
                WrappedStream = null;
            }

            #endregion
        }
    }
}
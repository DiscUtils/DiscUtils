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
    /// Converts a Stream into an IBuffer instance.
    /// </summary>
    public sealed class StreamBuffer : Buffer, IDisposable
    {
        private readonly Ownership _ownership;
        private SparseStream _stream;

        /// <summary>
        /// Initializes a new instance of the StreamBuffer class.
        /// </summary>
        /// <param name="stream">The stream to wrap.</param>
        /// <param name="ownership">Whether to dispose stream, when this object is disposed.</param>
        public StreamBuffer(Stream stream, Ownership ownership)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            _stream = stream as SparseStream;
            if (_stream == null)
            {
                _stream = SparseStream.FromStream(stream, ownership);
                _ownership = Ownership.Dispose;
            }
            else
            {
                _ownership = ownership;
            }
        }

        /// <summary>
        /// Can this buffer be read.
        /// </summary>
        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        /// <summary>
        /// Can this buffer be written.
        /// </summary>
        public override bool CanWrite
        {
            get { return _stream.CanWrite; }
        }

        /// <summary>
        /// Gets the current capacity of the buffer, in bytes.
        /// </summary>
        public override long Capacity
        {
            get { return _stream.Length; }
        }

        /// <summary>
        /// Gets the parts of the stream that are stored.
        /// </summary>
        /// <remarks>This may be an empty enumeration if all bytes are zero.</remarks>
        public override IEnumerable<StreamExtent> Extents
        {
            get { return _stream.Extents; }
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_ownership == Ownership.Dispose)
            {
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
            }
        }

        /// <summary>
        /// Reads from the buffer into a byte array.
        /// </summary>
        /// <param name="pos">The offset within the buffer to start reading.</param>
        /// <param name="buffer">The destination byte array.</param>
        /// <param name="offset">The start offset within the destination buffer.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            _stream.Position = pos;
            return _stream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Writes a byte array into the buffer.
        /// </summary>
        /// <param name="pos">The start offset within the buffer.</param>
        /// <param name="buffer">The source byte array.</param>
        /// <param name="offset">The start offset within the source byte array.</param>
        /// <param name="count">The number of bytes to write.</param>
        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            _stream.Position = pos;
            _stream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Flushes all data to the underlying storage.
        /// </summary>
        public override void Flush()
        {
            _stream.Flush();
        }

        /// <summary>
        /// Sets the capacity of the buffer, truncating if appropriate.
        /// </summary>
        /// <param name="value">The desired capacity of the buffer.</param>
        public override void SetCapacity(long value)
        {
            _stream.SetLength(value);
        }

        /// <summary>
        /// Gets the parts of a buffer that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest.</param>
        /// <param name="count">The number of bytes of interest.</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return _stream.GetExtentsInRange(start, count);
        }
    }
}
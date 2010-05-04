//
// Copyright (c) 2008-2010, Kenneth Bell
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

using System.Collections.Generic;
using System.IO;

namespace DiscUtils
{
    /// <summary>
    /// Converts a Buffer into a Stream.
    /// </summary>
    public class BufferStream : SparseStream
    {
        private IBuffer _buffer;
        private FileAccess _access;

        private long _position;

        /// <summary>
        /// Creates a new instance using a pre-existing buffer.
        /// </summary>
        /// <param name="buffer">The buffer to use</param>
        /// <param name="access">The access permitted to clients</param>
        public BufferStream(IBuffer buffer, FileAccess access)
        {
            _buffer = buffer;
            _access = access;
        }

        /// <summary>
        /// Gets an indication of whether read access is permitted.
        /// </summary>
        public override bool CanRead
        {
            get { return _access != FileAccess.Write; }
        }

        /// <summary>
        /// Gets an indication of whether seeking is permitted.
        /// </summary>
        public override bool CanSeek
        {
            get { return true; }
        }

        /// <summary>
        /// Gets an indication of whether write access is permitted.
        /// </summary>
        public override bool CanWrite
        {
            get { return _access != FileAccess.Read; }
        }

        /// <summary>
        /// Flushes all data to the underlying storage.
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// Gets the length of the stream (the capacity of the underlying buffer).
        /// </summary>
        public override long Length
        {
            get { return _buffer.Capacity; }
        }

        /// <summary>
        /// Gets and sets the current position within the stream.
        /// </summary>
        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        /// <summary>
        /// Reads a number of bytes from the stream.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="offset">The start offset within the destination buffer.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The number of bytes read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new IOException("Attempt to read from write-only stream");
            }

            int numRead = _buffer.Read(_position, buffer, offset, count);
            _position += numRead;
            return numRead;
        }

        /// <summary>
        /// Changes the current stream position.
        /// </summary>
        /// <param name="offset">The origin-relative stream position.</param>
        /// <param name="origin">The origin for the stream position.</param>
        /// <returns>The new stream position</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long effectiveOffset = offset;
            if (origin == SeekOrigin.Current)
            {
                effectiveOffset += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                effectiveOffset += _buffer.Capacity;
            }

            if (effectiveOffset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            else
            {
                _position = effectiveOffset;
                return _position;
            }
        }

        /// <summary>
        /// Sets the length of the stream (the underlying buffer's capacity).
        /// </summary>
        /// <param name="value">The new length of the stream.</param>
        public override void SetLength(long value)
        {
            _buffer.SetCapacity(value);
        }

        /// <summary>
        /// Writes a buffer to the stream.
        /// </summary>
        /// <param name="buffer">The buffer to write.</param>
        /// <param name="offset">The starting offset within buffer.</param>
        /// <param name="count">The number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to write to read-only stream");
            }

            _buffer.Write(_position, buffer, offset, count);
            _position += count;
        }

        /// <summary>
        /// Gets the stored extents within the sparse stream.
        /// </summary>
        public override IEnumerable<StreamExtent> Extents
        {
            get { return _buffer.Extents; }
        }
    }
}

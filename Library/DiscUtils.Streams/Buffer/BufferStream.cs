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

using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Streams
{
    /// <summary>
    /// Converts a Buffer into a Stream.
    /// </summary>
    public class BufferStream : SparseStream
    {
        private readonly FileAccess _access;
        private readonly IBuffer _buffer;

        private long _position;

        /// <summary>
        /// Initializes a new instance of the BufferStream class.
        /// </summary>
        /// <param name="buffer">The buffer to use.</param>
        /// <param name="access">The access permitted to clients.</param>
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
        /// Gets the stored extents within the sparse stream.
        /// </summary>
        public override IEnumerable<StreamExtent> Extents
        {
            get { return _buffer.Extents; }
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
        /// Flushes all data to the underlying storage.
        /// </summary>
        public override void Flush() {}

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

            StreamUtilities.AssertBufferParameters(buffer, offset, count);

            int numRead = _buffer.Read(_position, buffer, offset, count);
            _position += numRead;
            return numRead;
        }

        /// <summary>
        /// Changes the current stream position.
        /// </summary>
        /// <param name="offset">The origin-relative stream position.</param>
        /// <param name="origin">The origin for the stream position.</param>
        /// <returns>The new stream position.</returns>
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
            _position = effectiveOffset;
            return _position;
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

            StreamUtilities.AssertBufferParameters(buffer, offset, count);

            _buffer.Write(_position, buffer, offset, count);
            _position += count;
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
        public override void Clear(int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to erase bytes in a read-only stream");
            }

            _buffer.Clear(_position, count);
            _position += count;
        }

        /// <summary>
        /// Gets the parts of a stream that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest.</param>
        /// <param name="count">The number of bytes of interest.</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return _buffer.GetExtentsInRange(start, count);
        }
    }
}
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

#if !NETCORE
    using System;
#endif

namespace DiscUtils.Streams
{
    /// <summary>
    /// Abstract base class for implementations of IBuffer.
    /// </summary>
    public abstract class Buffer :
#if !NETCORE
        MarshalByRefObject, 
#endif
        IBuffer
    {
        /// <summary>
        /// Gets a value indicating whether this buffer can be read.
        /// </summary>
        public abstract bool CanRead { get; }

        /// <summary>
        /// Gets a value indicating whether this buffer can be modified.
        /// </summary>
        public abstract bool CanWrite { get; }

        /// <summary>
        /// Gets the current capacity of the buffer, in bytes.
        /// </summary>
        public abstract long Capacity { get; }

        /// <summary>
        /// Gets the parts of the stream that are stored.
        /// </summary>
        /// <remarks>This may be an empty enumeration if all bytes are zero.</remarks>
        public virtual IEnumerable<StreamExtent> Extents
        {
            get { return GetExtentsInRange(0, Capacity); }
        }

        /// <summary>
        /// Reads from the buffer into a byte array.
        /// </summary>
        /// <param name="pos">The offset within the buffer to start reading.</param>
        /// <param name="buffer">The destination byte array.</param>
        /// <param name="offset">The start offset within the destination buffer.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public abstract int Read(long pos, byte[] buffer, int offset, int count);

        /// <summary>
        /// Writes a byte array into the buffer.
        /// </summary>
        /// <param name="pos">The start offset within the buffer.</param>
        /// <param name="buffer">The source byte array.</param>
        /// <param name="offset">The start offset within the source byte array.</param>
        /// <param name="count">The number of bytes to write.</param>
        public abstract void Write(long pos, byte[] buffer, int offset, int count);

        /// <summary>
        /// Clears bytes from the buffer.
        /// </summary>
        /// <param name="pos">The start offset within the buffer.</param>
        /// <param name="count">The number of bytes to clear.</param>
        /// <remarks>
        /// <para>Logically equivalent to writing <c>count</c> null/zero bytes to the buffer, some
        /// implementations determine that some (or all) of the range indicated is not actually
        /// stored.  There is no direct, automatic, correspondence to clearing bytes and them
        /// not being represented as an 'extent' - for example, the implementation of the underlying
        /// stream may not permit fine-grained extent storage.</para>
        /// <para>It is always safe to call this method to 'zero-out' a section of a buffer, regardless of
        /// the underlying buffer implementation.</para>
        /// </remarks>
        public virtual void Clear(long pos, int count)
        {
            Write(pos, new byte[count], 0, count);
        }

        /// <summary>
        /// Flushes all data to the underlying storage.
        /// </summary>
        /// <remarks>The default behaviour, implemented by this class, is to take no action.</remarks>
        public virtual void Flush() {}

        /// <summary>
        /// Sets the capacity of the buffer, truncating if appropriate.
        /// </summary>
        /// <param name="value">The desired capacity of the buffer.</param>
        public abstract void SetCapacity(long value);

        /// <summary>
        /// Gets the parts of a buffer that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest.</param>
        /// <param name="count">The number of bytes of interest.</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes.</returns>
        public abstract IEnumerable<StreamExtent> GetExtentsInRange(long start, long count);
    }
}
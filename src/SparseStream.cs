//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils
{
    internal delegate SparseStream SparseStreamOpenDelegate();

    /// <summary>
    /// Represents a sparse stream.
    /// </summary>
    /// <remarks>A sparse stream is a logically contiguous stream where some parts of the stream
    /// aren't stored.  The unstored parts are implicitly zero-byte ranges.</remarks>
    public abstract class SparseStream : Stream
    {
        /// <summary>
        /// Gets the parts of a stream that are stored, within a specified range.
        /// </summary>
        /// <param name="start">The offset of the first byte of interest</param>
        /// <param name="count">The number of bytes of interest</param>
        /// <returns>An enumeration of stream extents, indicating stored bytes</returns>
        public virtual IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return StreamExtent.Intersect(Extents, new StreamExtent[] { new StreamExtent(start, count) });
        }

        /// <summary>
        /// Gets the parts of the stream that are stored.
        /// </summary>
        /// <remarks>This may be an empty enumeration if all bytes are zero.</remarks>
        public abstract IEnumerable<StreamExtent> Extents
        {
            get;
        }

        /// <summary>
        /// Converts any stream into a sparse stream.
        /// </summary>
        /// <param name="stream">The stream to convert.</param>
        /// <param name="takeOwnership"><c>true</c> to have the new stream dispose the wrapped
        /// stream when it is disposed.</param>
        /// <returns>A sparse stream</returns>
        /// <remarks>The returned stream has the entire wrapped stream as a
        /// single extent.</remarks>
        public static SparseStream FromStream(Stream stream, Ownership takeOwnership)
        {
            return new SparseWrapperStream(stream, takeOwnership);
        }

        /// <summary>
        /// Efficiently pumps data from a sparse stream to another stream.
        /// </summary>
        /// <param name="inStream">The sparse stream to pump from.</param>
        /// <param name="outStream">The stream to pump to.</param>
        /// <remarks><paramref name="outStream"/> must support seeking.</remarks>
        public static void Pump(SparseStream inStream, Stream outStream)
        {
            Pump(inStream, outStream, Sizes.Sector);
        }

        /// <summary>
        /// Efficiently pumps data from a sparse stream to another stream.
        /// </summary>
        /// <param name="inStream">The sparse stream to pump from.</param>
        /// <param name="outStream">The stream to pump to.</param>
        /// <param name="chunkSize">The smallest sequence of zero bytes that will be skipped when writing to <paramref name="outStream"/></param>
        /// <remarks><paramref name="outStream"/> must support seeking.</remarks>
        public static void Pump(SparseStream inStream, Stream outStream, int chunkSize)
        {
            if (!outStream.CanSeek)
            {
                throw new ArgumentException("Stream does not support seek operations", "outStream");
            }

            byte[] copyBuffer = new byte[Math.Max(512 * Sizes.OneKiB, chunkSize)];

            foreach (var extent in inStream.Extents)
            {
                inStream.Position = extent.Start;

                long extentOffset = 0;
                while (extentOffset < extent.Length)
                {
                    int toRead = (int)Math.Min(copyBuffer.Length, extent.Length - extentOffset);
                    int numRead = Utilities.ReadFully(inStream, copyBuffer, 0, toRead);

                    int copyBufferOffset = 0;
                    for (int i = 0; i < numRead; i += chunkSize)
                    {
                        if (IsAllZeros(copyBuffer, i, Math.Min(chunkSize, numRead - i)))
                        {
                            if (copyBufferOffset < i)
                            {
                                outStream.Position = extent.Start + extentOffset + copyBufferOffset;
                                outStream.Write(copyBuffer, copyBufferOffset, i - copyBufferOffset);
                            }
                            copyBufferOffset = i + chunkSize;
                        }
                    }

                    if (copyBufferOffset < numRead)
                    {
                        outStream.Position = extent.Start + extentOffset + copyBufferOffset;
                        outStream.Write(copyBuffer, copyBufferOffset, numRead - copyBufferOffset);
                    }

                    extentOffset += numRead;
                }
            }

            // Ensure the output stream is at least as long as the input stream.  This uses
            // read/write, rather than SetLength, to avoid failing on streams that can't be
            // explicitly resized.  Side-effect of this, is that if outStream is an NTFS
            // file stream, then actual clusters will be allocated out to at least the
            // length of the input stream.
            if (outStream.Length < inStream.Length)
            {
                inStream.Position = inStream.Length - 1;
                int b = inStream.ReadByte();
                if (b >= 0)
                {
                    outStream.Position = inStream.Length - 1;
                    outStream.WriteByte((byte)b);
                }
            }
        }

        /// <summary>
        /// Wraps a sparse stream in a read-only wrapper, preventing modification.
        /// </summary>
        /// <param name="toWrap">The stream to make read-only</param>
        /// <param name="ownership">Whether to transfer responsibility for calling Dispose on <c>toWrap</c></param>
        /// <returns>The read-only stream.</returns>
        public static SparseStream ReadOnly(SparseStream toWrap, Ownership ownership)
        {
            return new SparseReadOnlyWrapperStream(toWrap, ownership);
        }

        private static bool IsAllZeros(byte[] buffer, int offset, int count)
        {
            for (int j = 0; j < count; j++)
            {
                if (buffer[offset + j] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private class SparseReadOnlyWrapperStream : SparseStream
        {
            private SparseStream _wrapped;
            private Ownership _ownsWrapped;

            public SparseReadOnlyWrapperStream(SparseStream wrapped, Ownership ownsWrapped)
            {
                _wrapped = wrapped;
                _ownsWrapped = ownsWrapped;
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

            public override void Flush()
            {
            }

            public override long Length
            {
                get { return _wrapped.Length; }
            }

            public override long Position
            {
                get
                {
                    return _wrapped.Position;
                }
                set
                {
                    _wrapped.Position = value;
                }
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
                throw new InvalidOperationException("Attempt to change length of read-only stream");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new InvalidOperationException("Attempt to write to read-only stream");
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get { return _wrapped.Extents; }
            }
        }

        private class SparseWrapperStream : SparseStream
        {
            private Stream _wrapped;
            private Ownership _ownsWrapped;

            public SparseWrapperStream(Stream wrapped, Ownership ownsWrapped)
            {
                _wrapped = wrapped;
                _ownsWrapped = ownsWrapped;
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

            public override void Flush()
            {
                _wrapped.Flush();
            }

            public override long Length
            {
                get { return _wrapped.Length; }
            }

            public override long Position
            {
                get
                {
                    return _wrapped.Position;
                }
                set
                {
                    _wrapped.Position = value;
                }
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
                _wrapped.Write(buffer, offset, count);
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get
                {
                    yield return new StreamExtent(0, _wrapped.Length);
                }
            }
        }
    }
}

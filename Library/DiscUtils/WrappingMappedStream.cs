//
// Copyright (c) 2008-2012, Kenneth Bell
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

namespace DiscUtils
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Base class for streams that wrap another stream.
    /// </summary>
    /// <typeparam name="T">The type of stream to wrap.</typeparam>
    /// <remarks>
    /// Provides the default implementation of methods &amp; properties, so
    /// wrapping streams need only override the methods they need to intercept.
    /// </remarks>
    internal class WrappingMappedStream<T> : MappedStream
        where T : Stream
    {
        private T _wrapped;
        private Ownership _ownership;
        private List<StreamExtent> _extents;

        public WrappingMappedStream(T toWrap, Ownership ownership, IEnumerable<StreamExtent> extents)
        {
            _wrapped = toWrap;
            _ownership = ownership;
            if (extents != null)
            {
                _extents = new List<StreamExtent>(extents);
            }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                if (_extents != null)
                {
                    return _extents;
                }
                else
                {
                    SparseStream sparse = _wrapped as SparseStream;
                    if (sparse != null)
                    {
                        return sparse.Extents;
                    }
                    else
                    {
                        return new StreamExtent[] { new StreamExtent(0, _wrapped.Length) };
                    }
                }
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

        public override long Length
        {
            get { return _wrapped.Length; }
        }

        public override long Position
        {
            get { return _wrapped.Position; }
            set { _wrapped.Position = value; }
        }

        protected T WrappedStream
        {
            get { return _wrapped; }
        }

        public override IEnumerable<StreamExtent> MapContent(long start, long length)
        {
            MappedStream mapped = _wrapped as MappedStream;
            if (mapped != null)
            {
                return mapped.MapContent(start, length);
            }
            else
            {
                return new StreamExtent[] { new StreamExtent(start, length) };
            }
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

        public override void Clear(int count)
        {
            SparseStream sparse = _wrapped as SparseStream;
            if (sparse != null)
            {
                sparse.Clear(count);
            }
            else
            {
                base.Clear(count);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _wrapped.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_wrapped != null && _ownership == Ownership.Dispose)
                    {
                        _wrapped.Dispose();
                    }

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

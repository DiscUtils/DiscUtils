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

using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Streams
{
    /// <summary>
    /// Base class for streams that wrap another stream.
    /// </summary>
    /// <remarks>
    /// Provides the default implementation of methods &amp; properties, so
    /// wrapping streams need only override the methods they need to intercept.
    /// </remarks>
    public class WrappingStream : SparseStream
    {
        private readonly Ownership _ownership;
        private SparseStream _wrapped;

        public WrappingStream(SparseStream toWrap, Ownership ownership)
        {
            _wrapped = toWrap;
            _ownership = ownership;
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
            _wrapped.Clear(count);
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
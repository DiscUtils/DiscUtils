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
using System.IO;
using System.Security.Cryptography;
using DiscUtils.Streams;

namespace DiscUtils.Xva
{
#if NETCORE
    internal class HashStreamCore : Stream
    {
        private readonly IncrementalHash _hashAlg;
        private readonly Ownership _ownWrapped;

        private long _hashPos;
        private Stream _wrapped;

        public HashStreamCore(Stream wrapped, Ownership ownsWrapped, IncrementalHash hashAlg)
        {
            _wrapped = wrapped;
            _ownWrapped = ownsWrapped;
            _hashAlg = hashAlg;
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

        public override void Flush()
        {
            _wrapped.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Position != _hashPos)
            {
                throw new InvalidOperationException("Reads must be contiguous");
            }

            int numRead = _wrapped.Read(buffer, offset, count);

            _hashAlg.AppendData(buffer, offset, numRead);
            _hashPos += numRead;

            return numRead;
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

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _ownWrapped == Ownership.Dispose && _wrapped != null)
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
#else
    internal class HashStreamDotnet : Stream
    {
        private Stream _wrapped;
        private Ownership _ownWrapped;

        private HashAlgorithm _hashAlg;

        private long _hashPos;

        public HashStreamDotnet(Stream wrapped, Ownership ownsWrapped, HashAlgorithm hashAlg)
        {
            _wrapped = wrapped;
            _ownWrapped = ownsWrapped;
            _hashAlg = hashAlg;
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
            get
            {
                return _wrapped.Position;
            }

            set
            {
                _wrapped.Position = value;
            }
        }

        public override void Flush()
        {
            _wrapped.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Position != _hashPos)
            {
                throw new InvalidOperationException("Reads must be contiguous");
            }

            int numRead = _wrapped.Read(buffer, offset, count);

            _hashAlg.TransformBlock(buffer, offset, numRead, buffer, offset);
            _hashPos += numRead;

            return numRead;
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

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _ownWrapped == Ownership.Dispose && _wrapped != null)
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
#endif
}
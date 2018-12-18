//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Streams
{
    /// <summary>
    /// Stream wrapper to allow forward only seeking on not seekable streams
    /// </summary>
    public class PositionWrappingStream : WrappingStream
    {
        public PositionWrappingStream(SparseStream toWrap, long currentPosition, Ownership ownership)
            : base(toWrap, ownership)
        {
            _position = currentPosition;
        }

        private long _position;
        public override long Position
        {
            get { return _position; }
            set
            {
                if (_position == value)
                    return;
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (base.CanSeek)
            {
                return base.Seek(offset, SeekOrigin.Current);
            }
            switch (origin)
            {
                case SeekOrigin.Begin:
                    offset = offset - _position;
                    break;
                case SeekOrigin.Current:
                    offset = offset + _position;
                    break;
                case SeekOrigin.End:
                    offset = Length - offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
            }
            if (offset == 0)
                return _position;
            if (offset < 0)
                throw new NotSupportedException("backward seeking is not supported");
            var buffer = new byte[Sizes.OneKiB];
            while (offset > 0)
            {
                var read = base.Read(buffer, 0, (int)Math.Min(buffer.Length, offset));
                offset -= read;
            }
            return _position;
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            _position += count;
        }
    }
}
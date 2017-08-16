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
    public class SubStream : MappedStream
    {
        private readonly long _first;
        private readonly long _length;
        private readonly Ownership _ownsParent;

        private readonly Stream _parent;
        private long _position;

        public SubStream(Stream parent, long first, long length)
        {
            _parent = parent;
            _first = first;
            _length = length;
            _ownsParent = Ownership.None;

            if (_first + _length > _parent.Length)
            {
                throw new ArgumentException("Substream extends beyond end of parent stream");
            }
        }

        public SubStream(Stream parent, Ownership ownsParent, long first, long length)
        {
            _parent = parent;
            _ownsParent = ownsParent;
            _first = first;
            _length = length;

            if (_first + _length > _parent.Length)
            {
                throw new ArgumentException("Substream extends beyond end of parent stream");
            }
        }

        public override bool CanRead
        {
            get { return _parent.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _parent.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _parent.CanWrite; }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                SparseStream parentAsSparse = _parent as SparseStream;
                if (parentAsSparse != null)
                {
                    return OffsetExtents(parentAsSparse.GetExtentsInRange(_first, _length));
                }
                return new[] { new StreamExtent(0, _length) };
            }
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get { return _position; }

            set
            {
                if (value <= _length)
                {
                    _position = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Attempt to move beyond end of stream");
                }
            }
        }

        public override IEnumerable<StreamExtent> MapContent(long start, long length)
        {
            return new[] { new StreamExtent(start + _first, length) };
        }

        public override void Flush()
        {
            _parent.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
            }

            if (_position > _length)
            {
                return 0;
            }

            _parent.Position = _first + _position;
            int numRead = _parent.Read(buffer, offset,
                (int)Math.Min(count, Math.Min(_length - _position, int.MaxValue)));
            _position += numRead;
            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long absNewPos = offset;
            if (origin == SeekOrigin.Current)
            {
                absNewPos += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                absNewPos += _length;
            }

            if (absNewPos < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Attempt to move before start of stream");
            }

            _position = absNewPos;
            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Attempt to change length of a substream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
            }

            if (_position + count > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of substream");
            }

            _parent.Position = _first + _position;
            _parent.Write(buffer, offset, count);
            _position += count;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_ownsParent == Ownership.Dispose)
                    {
                        _parent.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private IEnumerable<StreamExtent> OffsetExtents(IEnumerable<StreamExtent> src)
        {
            foreach (StreamExtent e in src)
            {
                yield return new StreamExtent(e.Start - _first, e.Length);
            }
        }
    }
}
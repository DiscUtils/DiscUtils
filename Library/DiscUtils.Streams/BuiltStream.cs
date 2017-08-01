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
    public class BuiltStream : SparseStream
    {
        private Stream _baseStream;

        private BuilderExtent _currentExtent;
        private readonly List<BuilderExtent> _extents;
        private readonly long _length;
        private long _position;

        public BuiltStream(long length, List<BuilderExtent> extents)
        {
            _baseStream = new ZeroStream(length);
            _length = length;
            _extents = extents;

            // Make sure the extents are sorted, so binary searches will work.
            _extents.Sort(new ExtentStartComparer());
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                foreach (BuilderExtent extent in _extents)
                {
                    foreach (StreamExtent streamExtent in extent.StreamExtents)
                    {
                        yield return streamExtent;
                    }
                }
            }
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public override void Flush() {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length)
            {
                return 0;
            }
            if (_position + count > _length)
            {
                count = (int)(_length - _position);
            }

            int totalRead = 0;
            while (totalRead < count && _position < _length)
            {
                // If current region is outside the area of interest, clean it up
                if (_currentExtent != null &&
                    (_position < _currentExtent.Start || _position >= _currentExtent.Start + _currentExtent.Length))
                {
                    _currentExtent.DisposeReadState();
                    _currentExtent = null;
                }

                // If we need to find a new region, look for it
                if (_currentExtent == null)
                {
                    using (SearchExtent searchExtent = new SearchExtent(_position))
                    {
                        int idx = _extents.BinarySearch(searchExtent, new ExtentRangeComparer());
                        if (idx >= 0)
                        {
                            BuilderExtent extent = _extents[idx];
                            extent.PrepareForRead();
                            _currentExtent = extent;
                        }
                    }
                }

                int numRead = 0;

                // If the block is outside any known extent, defer to base stream.
                if (_currentExtent == null)
                {
                    _baseStream.Position = _position;
                    BuilderExtent nextExtent = FindNext(_position);
                    if (nextExtent != null)
                    {
                        numRead = _baseStream.Read(buffer, offset + totalRead,
                            (int)Math.Min(count - totalRead, nextExtent.Start - _position));
                    }
                    else
                    {
                        numRead = _baseStream.Read(buffer, offset + totalRead, count - totalRead);
                    }
                }
                else
                {
                    numRead = _currentExtent.Read(_position, buffer, offset + totalRead, count - totalRead);
                }

                _position += numRead;
                totalRead += numRead;
                if (numRead == 0)
                    break;
            }

            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = offset;
            if (origin == SeekOrigin.Current)
            {
                newPos += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                newPos += _length;
            }

            _position = newPos;
            return newPos;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_currentExtent != null)
                    {
                        _currentExtent.DisposeReadState();
                        _currentExtent = null;
                    }

                    if (_baseStream != null)
                    {
                        _baseStream.Dispose();
                        _baseStream = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private BuilderExtent FindNext(long pos)
        {
            int min = 0;
            int max = _extents.Count - 1;

            if (_extents.Count == 0 || _extents[_extents.Count - 1].Start + _extents[_extents.Count - 1].Length <= pos)
            {
                return null;
            }

            while (true)
            {
                if (min >= max)
                {
                    return _extents[min];
                }

                int mid = (max + min) / 2;
                if (_extents[mid].Start < pos)
                {
                    min = mid + 1;
                }
                else if (_extents[mid].Start > pos)
                {
                    max = mid;
                }
                else
                {
                    return _extents[mid];
                }
            }
        }

        private class SearchExtent : BuilderExtent
        {
            public SearchExtent(long pos)
                : base(pos, 1) {}

            public override void Dispose() {}

            public override void PrepareForRead()
            {
                // Not valid to use this 'dummy' extent for actual construction
                throw new NotSupportedException();
            }

            public override int Read(long diskOffset, byte[] block, int offset, int count)
            {
                // Not valid to use this 'dummy' extent for actual construction
                throw new NotSupportedException();
            }

            public override void DisposeReadState()
            {
                // Not valid to use this 'dummy' extent for actual construction
                throw new NotSupportedException();
            }
        }

        private class ExtentRangeComparer : IComparer<BuilderExtent>
        {
            public int Compare(BuilderExtent x, BuilderExtent y)
            {
                if (x == null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                if (y == null)
                {
                    throw new ArgumentNullException(nameof(y));
                }

                if (x.Start + x.Length <= y.Start)
                {
                    // x < y, with no intersection
                    return -1;
                }
                if (x.Start >= y.Start + y.Length)
                {
                    // x > y, with no intersection
                    return 1;
                }

                // x intersects y
                return 0;
            }
        }

        private class ExtentStartComparer : IComparer<BuilderExtent>
        {
            public int Compare(BuilderExtent x, BuilderExtent y)
            {
                if (x == null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                if (y == null)
                {
                    throw new ArgumentNullException(nameof(y));
                }

                long val = x.Start - y.Start;
                if (val < 0)
                {
                    return -1;
                }
                if (val > 0)
                {
                    return 1;
                }
                return 0;
            }
        }
    }
}
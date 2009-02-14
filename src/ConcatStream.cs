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
    /// <summary>
    /// The concatenation of multiple streams (read-only, for now).
    /// </summary>
    internal class ConcatStream : SparseStream
    {
        private SparseStream[] _streams;

        private long _position;

        public ConcatStream(params SparseStream[] streams)
        {
            _streams = streams;
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

        public override void Flush()
        {
            for (int i = 0; i < _streams.Length; ++i)
            {
                _streams[i].Flush();
            }
        }

        public override long Length
        {
            get
            {
                long length = 0;
                for (int i = 0; i < _streams.Length; ++i)
                {
                    length += _streams[i].Length;
                }
                return length;
            }
        }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Find the stream that _position is within
            long pos = 0;
            int activeStream = 0;
            while (activeStream < _streams.Length - 1 && pos + _streams[activeStream].Length <= _position)
            {
                pos += _streams[activeStream].Length;
                activeStream++;
            }

            _streams[activeStream].Position = _position - pos;
            int numRead = _streams[activeStream].Read(buffer, offset, count);
            _position += numRead;
            return numRead;
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            long effectiveOffset = offset;
            if (origin == SeekOrigin.Current)
            {
                effectiveOffset += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                effectiveOffset += Length;
            }

            if (offset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            else
            {
                Position = effectiveOffset;
                return Position;
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                List<StreamExtent> extents = new List<StreamExtent>();

                long pos = 0;
                for (int i = 0; i < _streams.Length; ++i)
                {
                    foreach (StreamExtent extent in _streams[i].Extents)
                    {
                        extents.Add(new StreamExtent(extent.Start + pos, extent.Length));
                    }

                    pos += _streams[i].Length;
                }

                return extents;
            }
        }
    }
}

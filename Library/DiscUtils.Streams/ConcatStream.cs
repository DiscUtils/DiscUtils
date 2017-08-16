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
using System.Globalization;
using System.IO;

namespace DiscUtils.Streams
{
    /// <summary>
    /// The concatenation of multiple streams (read-only, for now).
    /// </summary>
    public class ConcatStream : SparseStream
    {
        private readonly bool _canWrite;
        private readonly Ownership _ownsStreams;

        private long _position;
        private SparseStream[] _streams;

        public ConcatStream(Ownership ownsStreams, params SparseStream[] streams)
        {
            _ownsStreams = ownsStreams;
            _streams = streams;

            // Only allow writes if all streams can be written
            _canWrite = true;
            foreach (SparseStream stream in streams)
            {
                if (!stream.CanWrite)
                {
                    _canWrite = false;
                }
            }
        }

        public override bool CanRead
        {
            get
            {
                CheckDisposed();
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                CheckDisposed();
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                CheckDisposed();
                return _canWrite;
            }
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                CheckDisposed();
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

        public override long Length
        {
            get
            {
                CheckDisposed();
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
                CheckDisposed();
                return _position;
            }

            set
            {
                CheckDisposed();
                _position = value;
            }
        }

        public override void Flush()
        {
            CheckDisposed();
            for (int i = 0; i < _streams.Length; ++i)
            {
                _streams[i].Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            int totalRead = 0;
            int numRead = 0;

            do
            {
                long activeStreamStartPos;
                int activeStream = GetActiveStream(out activeStreamStartPos);

                _streams[activeStream].Position = _position - activeStreamStartPos;

                numRead = _streams[activeStream].Read(buffer, offset + totalRead, count - totalRead);

                totalRead += numRead;
                _position += numRead;
            } while (numRead != 0);

            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();

            long effectiveOffset = offset;
            if (origin == SeekOrigin.Current)
            {
                effectiveOffset += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                effectiveOffset += Length;
            }

            if (effectiveOffset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            Position = effectiveOffset;
            return Position;
        }

        public override void SetLength(long value)
        {
            CheckDisposed();

            long lastStreamOffset;
            int lastStream = GetStream(Length, out lastStreamOffset);
            if (value < lastStreamOffset)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "Unable to reduce stream length to less than {0}", lastStreamOffset));
            }

            _streams[lastStream].SetLength(value - lastStreamOffset);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            int totalWritten = 0;
            while (totalWritten != count)
            {
                // Offset of the stream = streamOffset
                long streamOffset;
                int streamIdx = GetActiveStream(out streamOffset);

                // Offset within the stream = streamPos
                long streamPos = _position - streamOffset;
                _streams[streamIdx].Position = streamPos;

                // Write (limited to the stream's length), except for final stream - that may be
                // extendable
                int numToWrite;
                if (streamIdx == _streams.Length - 1)
                {
                    numToWrite = count - totalWritten;
                }
                else
                {
                    numToWrite = (int)Math.Min(count - totalWritten, _streams[streamIdx].Length - streamPos);
                }

                _streams[streamIdx].Write(buffer, offset + totalWritten, numToWrite);

                totalWritten += numToWrite;
                _position += numToWrite;
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _ownsStreams == Ownership.Dispose && _streams != null)
                {
                    foreach (SparseStream stream in _streams)
                    {
                        stream.Dispose();
                    }

                    _streams = null;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private int GetActiveStream(out long startPos)
        {
            return GetStream(_position, out startPos);
        }

        private int GetStream(long targetPos, out long streamStartPos)
        {
            // Find the stream that _position is within
            streamStartPos = 0;
            int focusStream = 0;
            while (focusStream < _streams.Length - 1 && streamStartPos + _streams[focusStream].Length <= targetPos)
            {
                streamStartPos += _streams[focusStream].Length;
                focusStream++;
            }

            return focusStream;
        }

        private void CheckDisposed()
        {
            if (_streams == null)
            {
                throw new ObjectDisposedException("ConcatStream");
            }
        }
    }
}
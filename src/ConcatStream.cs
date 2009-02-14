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
using System.Globalization;
using System.IO;

namespace DiscUtils
{
    /// <summary>
    /// The concatenation of multiple streams (read-only, for now).
    /// </summary>
    internal class ConcatStream : SparseStream
    {
        private SparseStream[] _streams;
        private bool _canWrite;

        private long _position;

        public ConcatStream(params SparseStream[] streams)
        {
            _streams = streams;

            // Only allow writes if all streams can be written
            _canWrite = true;
            foreach (var stream in streams)
            {
                if (!stream.CanWrite)
                {
                    _canWrite = false;
                }
            }
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
            get { return _canWrite; }
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
            long activeStreamStartPos;
            int activeStream = GetActiveStream(out activeStreamStartPos);

            _streams[activeStream].Position = _position - activeStreamStartPos;
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
            long lastStreamOffset;
            int lastStream = GetStream(Length, out lastStreamOffset);
            if (value < lastStreamOffset)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "Unable to reduce stream length to less than {0}", lastStreamOffset));
            }
            _streams[lastStream].SetLength(value - lastStreamOffset);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {

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
                if(streamIdx == _streams.Length - 1)
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

    }
}

//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils
{
    internal class CompoundSparseStream : SparseStream
    {
        private SparseStream[] _streams;

        public CompoundSparseStream(SparseStream[] streams)
        {
            if (streams == null)
            {
                throw new ArgumentNullException("streams");
            }
            if (streams.Length < 1)
            {
                throw new ArgumentException("Must have at least one stream");
            }

            long length = streams[0].Length;
            for (int i = 1; i < streams.Length; ++i)
            {
                if (streams[i].Length != length)
                {
                    throw new ArgumentException("Streams must have identical length");
                }
            }

            _streams = streams;
        }

        public override int BlockBoundary
        {
            get { return _streams[0].BlockBoundary; }
        }

        public override bool CanWipe()
        {
            return false;
        }

        public override void Wipe(long length)
        {
            throw new NotSupportedException("Compound sparse streams cannot be wiped");
        }

        public override long NextBlock
        {
            get
            {
                long value = _streams[0].NextBlock;
                for (int i = 1; i < _streams.Length; ++i)
                {
                    long newValue = _streams[i].NextBlock;
                    if (value == -1 && newValue != -1)
                    {
                        value = newValue;
                    }
                    else if (newValue != -1 && newValue < value)
                    {
                        value = newValue;
                    }
                }
                return value;
            }
        }

        public override long NextBlockPosition
        {
            get
            {
                long nextBlock = NextBlock;
                if (nextBlock == -1)
                {
                    return -1;
                }
                return nextBlock + Position;
            }
        }

        public override long NextBlockLength
        {
            get {
                // Remember where we are - we have to keep skipping forward to look for the end of
                // the block.
                long savedPosition = Position;

                // If there is no next block, abort
                long nextBlock = NextBlockPosition;
                if (nextBlock == -1)
                {
                    return -1;
                }

                try
                {
                    long focusPos = nextBlock;

                    // Iterate through the streams, moving the focus position on as far as possible
                    // each time.  Only give up when a pass through all streams fails to improve the
                    // cursor.
                    long lastFocusPos = -1;
                    while (focusPos > lastFocusPos)
                    {
                        lastFocusPos = focusPos;
                        foreach (SparseStream s in _streams)
                        {
                            s.Position = focusPos;

                            // If we at the start of / in the middle of a block in this stream, move
                            // the cursor on...
                            if (s.NextBlock == 0)
                            {
                                focusPos += s.NextBlockLength;
                            }
                        }
                    }

                    return focusPos;
                }
                finally
                {
                    Position = savedPosition;
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
            get { return _streams[0].CanWrite; }
        }

        public override void Flush()
        {
            _streams[0].Flush();
        }

        public override long Length
        {
            get { return _streams[0].Length; }
        }

        public override long Position
        {
            get
            {
                return _streams[0].Position;
            }
            set
            {
                foreach (Stream s in _streams)
                {
                    s.Position = value;
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to read negative number of bytes");
            }
            if (Position + count > Length)
            {
                throw new ArgumentException("Attempt to read beyond end of stream");
            }

            long pos = Position;
            int numRead = ReadFromStreams(0, buffer, offset, count);

            // Make sure all position markers are in the right place
            Position = pos + numRead;

            return numRead;
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            long pos = _streams[0].Seek(offset, origin);
            return SyncPositions();
        }

        public override void SetLength(long value)
        {
            if (Length != value)
            {
                throw new NotSupportedException("The length of compound sparse streams cannot be changed");
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to write negative number of bytes");
            }
            if (Position + count > Length)
            {
                throw new ArgumentException("Attempt to write beyond end of stream");
            }

            if (Position % _streams[0].BlockBoundary == 0 && count % _streams[0].BlockBoundary == 0)
            {
                _streams[0].Write(buffer, offset, count);
            }
            else
            {
                // Need to 'read' the unspecified bytes contained in other disks at begining and end of this buffer
                throw new NotImplementedException("Unaligned writes not supported");
            }

            SyncPositions();
        }

        /// <summary>
        /// Updates all streams to the position of the 'first' stream.
        /// </summary>
        private long SyncPositions()
        {
            long pos = _streams[0].Position;
            for (int i = 1; i < _streams.Length; ++i)
            {
                _streams[i].Position = pos;
            }
            return pos;
        }

        private int ReadFromStreams(int stream, byte[] buffer, int offset, int count)
        {
            if (stream >= _streams.Length)
            {
                // No data in any stream - fill
                for (int i = 0; i < count; ++i)
                {
                    buffer[offset + i] = 0;
                }
                return count;
            }

            long nextBlock = _streams[stream].NextBlock;
            if (nextBlock == -1)
            {
                // Nothing left in this stream - let the next one have a shot.
                return ReadFromStreams(stream + 1, buffer, offset, count);
            }
            else if (nextBlock == 0)
            {
                // Read some bytes from this stream, and update global position
                return _streams[stream].Read(buffer, offset, (int)Math.Min(_streams[stream].NextBlockLength, count));
            }
            else
            {
                // We're in a gap of this stream - limit the extent of the read in lower-priority streams
                return ReadFromStreams(stream + 1, buffer, offset, (int)Math.Min(nextBlock, count));
            }
        }

    }
}

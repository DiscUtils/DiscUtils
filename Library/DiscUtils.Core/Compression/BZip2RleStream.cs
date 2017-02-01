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

//
// Based on "libbzip2", Copyright (C) 1996-2007 Julian R Seward.
//

using System;
using System.IO;

namespace DiscUtils.Compression
{
    internal class BZip2RleStream : Stream
    {
        private byte[] _blockBuffer;
        private int _blockOffset;
        private int _blockRemaining;
        private byte _lastByte;

        private int _numSame;
        private long _position;
        private int _runBytesOutstanding;

        public bool AtEof
        {
            get { return _runBytesOutstanding == 0 && _blockRemaining == 0; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { return _position; }
            set { throw new NotSupportedException(); }
        }

        public void Reset(byte[] buffer, int offset, int count)
        {
            _position = 0;
            _blockBuffer = buffer;
            _blockOffset = offset;
            _blockRemaining = count;
            _numSame = -1;
            _lastByte = 0;
            _runBytesOutstanding = 0;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int numRead = 0;

            while (numRead < count && _runBytesOutstanding > 0)
            {
                int runCount = Math.Min(_runBytesOutstanding, count);
                for (int i = 0; i < runCount; ++i)
                {
                    buffer[offset + numRead] = _lastByte;
                }

                _runBytesOutstanding -= runCount;
                numRead += runCount;
            }

            while (numRead < count && _blockRemaining > 0)
            {
                byte b = _blockBuffer[_blockOffset];
                ++_blockOffset;
                --_blockRemaining;

                if (_numSame == 4)
                {
                    int runCount = Math.Min(b, count - numRead);
                    for (int i = 0; i < runCount; ++i)
                    {
                        buffer[offset + numRead] = _lastByte;
                        numRead++;
                    }

                    _runBytesOutstanding = b - runCount;
                    _numSame = 0;
                }
                else
                {
                    if (b != _lastByte || _numSame <= 0)
                    {
                        _lastByte = b;
                        _numSame = 0;
                    }

                    buffer[offset + numRead] = b;
                    numRead++;
                    _numSame++;
                }
            }

            _position += numRead;
            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
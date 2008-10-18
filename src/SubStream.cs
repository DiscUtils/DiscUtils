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
    internal class SubStream : Stream
    {
        private long position;
        private long first;
        private long length;

        private Stream parent;

        public SubStream(Stream parent, long first, long length)
        {
            this.parent = parent;
            this.first = first;
            this.length = length;
            this.position = 0;
        }


        public override bool CanRead
        {
            get { return parent.CanRead; }
        }

        public override bool CanSeek
        {
            get { return parent.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return parent.CanWrite; }
        }

        public override void Flush()
        {
            parent.Flush();
        }

        public override long Length
        {
            get { return length; }
        }

        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                if (value <= length)
                {
                    position = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("value", "Attempt to move beyond end of stream");
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to read negative bytes");
            }

            parent.Position = first + position;
            int numRead = parent.Read(buffer, offset, (int)Math.Min(count, Math.Min(length - position, int.MaxValue)));
            position += numRead;
            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long absNewPos = offset;
            if (origin == SeekOrigin.Current)
            {
                absNewPos += position;
            }
            else if (origin == SeekOrigin.End)
            {
                absNewPos += length;
            }

            if (absNewPos < 0)
            {
                throw new ArgumentOutOfRangeException("offset", "Attempt to move before start of stream");
            }
            position = absNewPos;
            return position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Attempt to change length of a substream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to write negative bytes");
            }
            if (position + count > length)
            {
                throw new ArgumentOutOfRangeException("count", "Attempt to write beyond end of substream");
            }

            parent.Position = first + position;
            parent.Write(buffer, offset, count);
            position += count;
        }
    }
}

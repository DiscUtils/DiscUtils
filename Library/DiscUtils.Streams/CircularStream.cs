//
// Copyright (c) 2008-2013, Kenneth Bell
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

namespace DiscUtils.Streams
{
    /// <summary>
    /// Represents a stream that is circular, so reads and writes off the end of the stream wrap.
    /// </summary>
    public sealed class CircularStream : WrappingStream
    {
        public CircularStream(SparseStream toWrap, Ownership ownership)
            : base(toWrap, ownership) {}

        public override int Read(byte[] buffer, int offset, int count)
        {
            WrapPosition();

            int read = base.Read(buffer, offset, (int)Math.Min(Length - Position, count));

            WrapPosition();

            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WrapPosition();

            int totalWritten = 0;
            while (totalWritten < count)
            {
                int toWrite = (int)Math.Min(count - totalWritten, Length - Position);

                base.Write(buffer, offset + totalWritten, toWrite);

                WrapPosition();

                totalWritten += toWrite;
            }
        }

        private void WrapPosition()
        {
            long pos = Position;
            long length = Length;

            if (pos >= length)
            {
                Position = pos % length;
            }
        }
    }
}
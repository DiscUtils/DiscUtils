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

namespace DiscUtils.Streams
{
    public class BuilderBufferExtent : BuilderExtent
    {
        private byte[] _buffer;
        private readonly bool _fixedBuffer;

        public BuilderBufferExtent(long start, long length)
            : base(start, length) {}

        public BuilderBufferExtent(long start, byte[] buffer)
            : base(start, buffer.Length)
        {
            _fixedBuffer = true;
            _buffer = buffer;
        }

        public override void Dispose() {}

        public override void PrepareForRead()
        {
            if (!_fixedBuffer)
            {
                _buffer = GetBuffer();
            }
        }

        public override int Read(long diskOffset, byte[] block, int offset, int count)
        {
            int startOffset = (int)(diskOffset - Start);
            int numBytes = (int)Math.Min(Length - startOffset, count);
            Array.Copy(_buffer, startOffset, block, offset, numBytes);
            return numBytes;
        }

        public override void DisposeReadState()
        {
            if (!_fixedBuffer)
            {
                _buffer = null;
            }
        }

        protected virtual byte[] GetBuffer()
        {
            throw new NotSupportedException("Derived class should implement");
        }
    }
}
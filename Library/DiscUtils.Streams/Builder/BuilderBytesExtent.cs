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
    public class BuilderBytesExtent : BuilderExtent
    {
        protected byte[] _data;

        public BuilderBytesExtent(long start, byte[] data)
            : base(start, data.Length)
        {
            _data = data;
        }

        protected BuilderBytesExtent(long start, long length)
            : base(start, length) {}

        public override void Dispose() {}

        public override void PrepareForRead() {}

        public override int Read(long diskOffset, byte[] block, int offset, int count)
        {
            int start = (int)Math.Min(diskOffset - Start, _data.Length);
            int numRead = Math.Min(count, _data.Length - start);

            Array.Copy(_data, start, block, offset, numRead);

            return numRead;
        }

        public override void DisposeReadState() {}
    }
}
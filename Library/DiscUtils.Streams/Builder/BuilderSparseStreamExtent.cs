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

using System.Collections.Generic;

namespace DiscUtils.Streams
{
    public class BuilderSparseStreamExtent : BuilderExtent
    {
        private readonly Ownership _ownership;
        private SparseStream _stream;

        public BuilderSparseStreamExtent(long start, SparseStream stream)
            : this(start, stream, Ownership.None) {}

        public BuilderSparseStreamExtent(long start, SparseStream stream, Ownership ownership)
            : base(start, stream.Length)
        {
            _stream = stream;
            _ownership = ownership;
        }

        public override IEnumerable<StreamExtent> StreamExtents
        {
            get { return StreamExtent.Offset(_stream.Extents, Start); }
        }

        public override void Dispose()
        {
            if (_stream != null && _ownership == Ownership.Dispose)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        public override void PrepareForRead() {}

        public override int Read(long diskOffset, byte[] block, int offset, int count)
        {
            _stream.Position = diskOffset - Start;
            return _stream.Read(block, offset, count);
        }

        public override void DisposeReadState() {}
    }
}
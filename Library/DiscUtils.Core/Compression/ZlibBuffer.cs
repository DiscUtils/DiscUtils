//
// Copyright (c) 2014, Quamotion
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
using DiscUtils.Streams;
using Buffer=DiscUtils.Streams.Buffer;

namespace DiscUtils.Compression
{
    internal class ZlibBuffer : Buffer
    {
        private Ownership _ownership;
        private readonly Stream _stream;
        private int position;

        public ZlibBuffer(Stream stream, Ownership ownership)
        {
            _stream = stream;
            _ownership = ownership;
            position = 0;
        }

        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        public override bool CanWrite
        {
            get { return _stream.CanWrite; }
        }

        public override long Capacity
        {
            get { return _stream.Length; }
        }

        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            if (pos != position)
            {
                throw new NotSupportedException();
            }

            int read = _stream.Read(buffer, offset, count);
            position += read;
            return read;
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void SetCapacity(long value)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            yield return new StreamExtent(0, _stream.Length);
        }
    }
}
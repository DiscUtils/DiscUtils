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
using System.IO;

namespace DiscUtils.Ntfs
{
    internal class ResidentAttributeStream : SparseStream
    {
        private File _file;
        private SparseStream _wrapped;

        public ResidentAttributeStream(File file, SparseStream wrapped)
        {
            _file = file;
            _wrapped = wrapped;
        }

        public override void Close()
        {
            base.Close();
        }

        public override bool CanRead
        {
            get { return _wrapped.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _wrapped.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _wrapped.CanWrite; }
        }

        public override void Flush()
        {
            _wrapped.Flush();
        }

        public override long Length
        {
            get { return _wrapped.Length; }
        }

        public override long Position
        {
            get
            {
                return _wrapped.Position;
            }
            set
            {
                _wrapped.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _wrapped.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _wrapped.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (value != _wrapped.Length)
            {
                _file.MarkMftRecordDirty();
                _wrapped.SetLength(value);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (CanWrite && count != 0)
            {
                _file.MarkMftRecordDirty();
            }
            _wrapped.Write(buffer, offset, count);
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get { return _wrapped.Extents; }
        }
    }
}

//
// Copyright (c) 2009, Kenneth Bell
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

namespace DiscUtils.Iscsi
{
    internal class DiskStream : SparseStream
    {
        private Session _session;
        private long _lun;

        private long _length;
        private long _position;

        private ushort _blockSize = 512; // TODO - use real value...
        private bool _canWrite = false; // TODO - use real value...

        public DiskStream(Session session, long lun)
        {
            _session = session;
            _lun = lun;

            LunCapacity capacity = session.GetCapacity(lun);
            _length = capacity.LogicalBlockCount * capacity.BlockSize;
        }
        
        public override bool CanRead
        {
            get { throw new NotImplementedException(); }
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
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { return _length; }
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
            long firstBlock = _position / _blockSize;
            long lastBlock = Utilities.Ceil(_position + count, _blockSize);

            byte[] tempBuffer = new byte[(lastBlock - firstBlock) * _blockSize];
            int numRead = _session.Read(_lun, firstBlock, (short)(lastBlock - firstBlock), tempBuffer, 0);

            Array.Copy(tempBuffer, _position - (firstBlock * _blockSize), buffer, offset, Math.Min(count, numRead));

            _position += numRead;

            return numRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<StreamExtent> Extents
        {
            get
            {
                yield return new StreamExtent(0, _length);
            }
        }
    }
}

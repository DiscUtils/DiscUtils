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

        private int _blockSize;
        private bool _canWrite = true; // TODO - use real value...

        public DiskStream(Session session, long lun)
        {
            _session = session;
            _lun = lun;

            LunCapacity capacity = session.GetCapacity(lun);
            _blockSize = capacity.BlockSize;
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
            long effectiveOffset = offset;
            if (origin == SeekOrigin.Current)
            {
                effectiveOffset += _position;
            }
            else if (origin == SeekOrigin.End)
            {
                effectiveOffset += _length;
            }

            if (offset < 0)
            {
                throw new IOException("Attempt to move before beginning of disk");
            }
            else
            {
                _position = effectiveOffset;
                return _position;
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new IOException("Attempt to write to read-only stream");
            }

            int numWritten = 0;

            while (numWritten < count)
            {
                long block = _position / _blockSize;
                uint offsetInBlock = (uint)(_position % _blockSize);

                int toWrite = (int)Math.Min(count - numWritten, _blockSize - offsetInBlock);

                // Need to read - we're not handling a full block
                if (offsetInBlock != 0 || toWrite < _blockSize)
                {
                    byte[] blockBuffer = new byte[_blockSize];
                    int numRead = _session.Read(_lun, block, 1, blockBuffer, 0);

                    if (numRead != _blockSize)
                    {
                        throw new IOException("Incomplete read, received " + numRead + " bytes from 1 block");
                    }

                    // Overlay as much data as we have for this block
                    Array.Copy(buffer, offset + numWritten, blockBuffer, offsetInBlock, toWrite);

                    // Write the block back
                    _session.Write(_lun, block, 1, _blockSize, blockBuffer, 0);
                }
                else
                {
                    // Processing at least one whole sector, just write (after making sure to trim any partial sectors from the end)...
                    short numBlocks = (short)(toWrite / _blockSize);
                    toWrite = numBlocks * _blockSize;

                    _session.Write(_lun, block, numBlocks, _blockSize, buffer, offset + numWritten);
                }

                numWritten += toWrite;
                _position += toWrite;
            }
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

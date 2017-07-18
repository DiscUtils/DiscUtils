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
using System.Collections.Generic;
using DiscUtils.Streams;
using Buffer=DiscUtils.Streams.Buffer;

namespace DiscUtils.Ext
{
    internal class FileBuffer : Buffer
    {
        private readonly Context _context;
        private readonly Inode _inode;

        public FileBuffer(Context context, Inode inode)
        {
            _context = context;
            _inode = inode;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Capacity
        {
            get { return _inode.FileSize; }
        }

        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            if (pos > _inode.FileSize)
            {
                return 0;
            }

            uint blockSize = _context.SuperBlock.BlockSize;

            int totalRead = 0;
            int totalBytesRemaining = (int)Math.Min(count, _inode.FileSize - pos);

            while (totalBytesRemaining > 0)
            {
                uint logicalBlock = (uint)((pos + totalRead) / blockSize);
                int blockOffset = (int)(pos + totalRead - logicalBlock * (long)blockSize);

                uint physicalBlock = 0;
                if (logicalBlock < 12)
                {
                    physicalBlock = _inode.DirectBlocks[logicalBlock];
                }
                else
                {
                    logicalBlock -= 12;
                    if (logicalBlock < blockSize / 4)
                    {
                        if (_inode.IndirectBlock != 0)
                        {
                            _context.RawStream.Position = _inode.IndirectBlock * (long)blockSize + logicalBlock * 4;
                            byte[] indirectData = StreamUtilities.ReadExact(_context.RawStream, 4);
                            physicalBlock = EndianUtilities.ToUInt32LittleEndian(indirectData, 0);
                        }
                    }
                    else
                    {
                        logicalBlock -= blockSize / 4;
                        if (logicalBlock < blockSize / 4 * (blockSize / 4))
                        {
                            if (_inode.DoubleIndirectBlock != 0)
                            {
                                _context.RawStream.Position = _inode.DoubleIndirectBlock * (long)blockSize +
                                                              logicalBlock / (blockSize / 4) * 4;
                                byte[] indirectData = StreamUtilities.ReadExact(_context.RawStream, 4);
                                uint indirectBlock = EndianUtilities.ToUInt32LittleEndian(indirectData, 0);

                                if (indirectBlock != 0)
                                {
                                    _context.RawStream.Position = indirectBlock * (long)blockSize +
                                                                  logicalBlock % (blockSize / 4) * 4;
                                    StreamUtilities.ReadExact(_context.RawStream, indirectData, 0, 4);
                                    physicalBlock = EndianUtilities.ToUInt32LittleEndian(indirectData, 0);
                                }
                            }
                        }
                        else
                        {
                            throw new NotSupportedException("Triple indirection");
                        }
                    }
                }

                int toRead = (int)Math.Min(totalBytesRemaining, blockSize - blockOffset);
                int numRead;
                if (physicalBlock == 0)
                {
                    Array.Clear(buffer, offset + totalRead, toRead);
                    numRead = toRead;
                }
                else
                {
                    _context.RawStream.Position = physicalBlock * (long)blockSize + blockOffset;
                    numRead = _context.RawStream.Read(buffer, offset + totalRead, toRead);
                }

                totalBytesRemaining -= numRead;
                totalRead += numRead;
            }

            return totalRead;
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
            return StreamExtent.Intersect(
                new[] { new StreamExtent(0, Capacity) },
                new StreamExtent(start, count));
        }
    }
}
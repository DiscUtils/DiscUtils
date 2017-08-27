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
using System.IO;
using DiscUtils.Streams;
using Buffer=DiscUtils.Streams.Buffer;

namespace DiscUtils.Ext
{
    internal class ExtentsFileBuffer : Buffer
    {
        private readonly Context _context;
        private readonly Inode _inode;

        public ExtentsFileBuffer(Context context, Inode inode)
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

            long blockSize = _context.SuperBlock.BlockSize;

            int totalRead = 0;
            int totalBytesRemaining = (int)Math.Min(count, _inode.FileSize - pos);

            ExtentBlock extents = _inode.Extents;

            while (totalBytesRemaining > 0)
            {
                uint logicalBlock = (uint)((pos + totalRead) / blockSize);
                int blockOffset = (int)(pos + totalRead - logicalBlock * blockSize);

                int numRead = 0;

                Extent extent = FindExtent(extents, logicalBlock);
                if (extent == null)
                {
                    throw new IOException("Unable to find extent for block " + logicalBlock);
                }
                if (extent.FirstLogicalBlock > logicalBlock)
                {
                    numRead =
                        (int)
                        Math.Min(totalBytesRemaining,
                            (extent.FirstLogicalBlock - logicalBlock) * blockSize - blockOffset);
                    Array.Clear(buffer, offset + totalRead, numRead);
                }
                else
                {
                    long physicalBlock = logicalBlock - extent.FirstLogicalBlock + (long)extent.FirstPhysicalBlock;
                    int toRead =
                        (int)
                        Math.Min(totalBytesRemaining,
                            (extent.NumBlocks - (logicalBlock - extent.FirstLogicalBlock)) * blockSize - blockOffset);

                    _context.RawStream.Position = physicalBlock * blockSize + blockOffset;
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

        private Extent FindExtent(ExtentBlock node, uint logicalBlock)
        {
            if (node.Index != null)
            {
                ExtentIndex idxEntry = null;

                if (node.Index.Length == 0)
                {
                    return null;
                }
                if (node.Index[0].FirstLogicalBlock >= logicalBlock)
                {
                    idxEntry = node.Index[0];
                }
                else
                {
                    for (int i = 0; i < node.Index.Length; ++i)
                    {
                        if (node.Index[i].FirstLogicalBlock > logicalBlock)
                        {
                            idxEntry = node.Index[i - 1];
                            break;
                        }
                    }
                }

                if (idxEntry == null)
                {
                    idxEntry = node.Index[node.Index.Length - 1];
                }

                ExtentBlock subBlock = LoadExtentBlock(idxEntry);
                return FindExtent(subBlock, logicalBlock);
            }
            if (node.Extents != null)
            {
                Extent entry = null;

                if (node.Extents.Length == 0)
                {
                    return null;
                }
                if (node.Extents[0].FirstLogicalBlock >= logicalBlock)
                {
                    return node.Extents[0];
                }
                for (int i = 0; i < node.Extents.Length; ++i)
                {
                    if (node.Extents[i].FirstLogicalBlock > logicalBlock)
                    {
                        entry = node.Extents[i - 1];
                        break;
                    }
                }

                if (entry == null)
                {
                    entry = node.Extents[node.Extents.Length - 1];
                }

                return entry;
            }
            return null;
        }

        private ExtentBlock LoadExtentBlock(ExtentIndex idxEntry)
        {
            uint blockSize = _context.SuperBlock.BlockSize;
            _context.RawStream.Position = idxEntry.LeafPhysicalBlock * blockSize;
            byte[] buffer = StreamUtilities.ReadExact(_context.RawStream, (int)blockSize);
            ExtentBlock subBlock = EndianUtilities.ToStruct<ExtentBlock>(buffer, 0);
            return subBlock;
        }
    }
}
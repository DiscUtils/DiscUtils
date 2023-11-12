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

        /// <summary>
        /// Reads from the buffer into a byte array.
        /// </summary>
        /// <param name="pos">The offset within the buffer to start reading.</param>
        /// <param name="buffer">The destination byte array.</param>
        /// <param name="offset">The start offset within the destination buffer.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            if (pos >= _inode.FileSize)
            {
                return 0;
            }

            long blockSize = _context.SuperBlock.BlockSize;

            int totalRead = 0;  // number of bytes of the file read so far during this invocation of Read
            int totalBytesRemaining = (int)Math.Min(count, _inode.FileSize - pos);  // number of bytes remaining to be read from the file during this invocation

            ExtentBlock extents = _inode.Extents;

            while (totalBytesRemaining > 0)
            {
                uint logicalBlock = (uint)((pos + totalRead) / blockSize);  // logical block containing the next byte to read
                int blockOffset = (int)(pos + totalRead - logicalBlock * blockSize);  // offset within 'logicalBlock' of the next byte to read

                int numRead = 0;  // number of bytes read from the file during this iteration of the 'while' loop

                // Find the extent containing 'logicalBlock' or, if none, the first extent beyond it.
                Extent extent = FindExtent(extents, logicalBlock);

                if (extent == null)
                {
                    // The remainder of the sparse file is one big hole; all bytes remaining to be read in the file are zeroes.
                    numRead = totalBytesRemaining;
                    Array.Clear(buffer, offset + totalRead, numRead);
                }
                else if (extent.FirstLogicalBlock > logicalBlock)
                {
                    // We're reading from a hole in the sparse file that ends at 'extent'.  Implicitly read zeroes
                    // for the remainder of 'logicalBlock', and for all subsequent blocks before the beginning of 'extent'.
                    numRead = (int)
                        Math.Min(totalBytesRemaining,
                                 (extent.FirstLogicalBlock - logicalBlock) * blockSize - blockOffset);
                    Array.Clear(buffer, offset + totalRead, numRead);
                }
                else
                {
                    // We found the extent containing 'logicalblock'.  Read all bytes from 'logicalBlock' until the end of this extent.
                    int toRead = (int)
                        Math.Min(totalBytesRemaining,
                                 (extent.FirstLogicalBlock + extent.NumBlocks - logicalBlock) * blockSize - blockOffset);

                    long physicalBlock = logicalBlock - extent.FirstLogicalBlock + (long)extent.FirstPhysicalBlock;
                    _context.RawStream.Position = physicalBlock * blockSize + blockOffset;
                    numRead = _context.RawStream.Read(buffer, offset + totalRead, toRead);
                }

                // Assert that we've made some progress during this iteration of the loop.
                if (numRead <= 0)
                {
                    throw new IOException($"Unable to read logical block {logicalBlock};  extent?.FirstLogicalBlock: {extent?.FirstLogicalBlock}");
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

        /// <summary>
        /// Returns the extent containing a given logical block or, if none, the first extent beyond it.
        /// In other words, returns the first extent containing a logical block greater than or equal to the given one.
        /// Returns null if no such extent exists.  Ignores empty or uninitialized extents.
        /// </summary>
        private Extent FindExtent(ExtentBlock node, uint logicalBlock)
        {
            if (node.Extents != null)
            {
                return FindExtent(node.Extents, logicalBlock);
            }
            else if (node.Index != null)
            {
                return FindExtent(node.Index, logicalBlock);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the extent containing a given logical block or, if none, the first extent beyond it.
        /// In other words, returns the first extent containing a logical block greater than or equal to the given one.
        /// Returns null if no such extent exists.  Ignores empty or uninitialized extents.
        /// </summary>
        private Extent FindExtent(Extent[] extents, uint logicalBlock)
        {
            for (int i = 0; i < extents.Length; ++i)
            {
                Extent extent = extents[i];
                if (extent.IsInitialized &&
                    extent.NumBlocks > 0 &&
                    extent.FirstLogicalBlock + extent.NumBlocks > logicalBlock)
                {
                    return extent;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the extent containing a given logical block or, if none, the first extent beyond it.
        /// In other words, returns the first extent containing a logical block greater than or equal to the given one.
        /// Returns null if no such extent exists.  Ignores empty or uninitialized extents.
        /// </summary>
        private Extent FindExtent(ExtentIndex[] indexes, uint logicalBlock)
        {
            // Find indexBefore -- the last ExtentIndex (if any) that starts before logicalBlock.
            // Find indexAfter -- the first ExtentIndex (if any) that starts at or after logicalBlock.
            // The extent we want will be in one of those.
            ExtentIndex indexBefore = null;
            ExtentIndex indexAfter = null;
            for (int i = 0; i < indexes.Length; ++i)
            {
                if (indexes[i].FirstLogicalBlock < logicalBlock)
                {
                    indexBefore = indexes[i];
                }
                else
                {
                    indexAfter = indexes[i];
                    // As an optimization, we can ignore indexBefore if indexAfter starts exactly at logicalBlock.
                    if (indexAfter.FirstLogicalBlock == logicalBlock)
                    {
                        indexBefore = null;
                    }
                    break;
                }
            }

            Extent extent = null;

            // Look for the desired extent in the ExtentIndex before logicalBlock.
            if (indexBefore != null)
            {
                ExtentBlock subBlock = LoadExtentBlock(indexBefore);
                extent = FindExtent(subBlock, logicalBlock);
            }

            // If the desired extent wasn't found above, look for it in the next ExtentIndex.
            if (extent == null && indexAfter != null)
            {
                ExtentBlock subBlock = LoadExtentBlock(indexAfter);
                extent = FindExtent(subBlock, logicalBlock);
            }

            return extent;
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
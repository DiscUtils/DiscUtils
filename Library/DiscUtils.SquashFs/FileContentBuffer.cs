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

namespace DiscUtils.SquashFs
{
    internal class FileContentBuffer : IBuffer
    {
        private const uint InvalidFragmentKey = 0xFFFFFFFF;

        private readonly int[] _blockLengths;
        private readonly Context _context;
        private readonly RegularInode _inode;

        public FileContentBuffer(Context context, RegularInode inode, MetadataRef inodeRef)
        {
            _context = context;
            _inode = inode;

            context.InodeReader.SetPosition(inodeRef);
            context.InodeReader.Skip(_inode.Size);

            int numBlocks = (int)(_inode.FileSize / _context.SuperBlock.BlockSize);
            if (_inode.FileSize % _context.SuperBlock.BlockSize != 0 && _inode.FragmentKey == InvalidFragmentKey)
            {
                ++numBlocks;
            }

            byte[] lengthData = new byte[numBlocks * 4];
            context.InodeReader.Read(lengthData, 0, lengthData.Length);

            _blockLengths = new int[numBlocks];
            for (int i = 0; i < numBlocks; ++i)
            {
                _blockLengths[i] = EndianUtilities.ToInt32LittleEndian(lengthData, i * 4);
            }
        }

        public bool CanRead
        {
            get { return true; }
        }

        public bool CanWrite
        {
            get { return false; }
        }

        public long Capacity
        {
            get { return _inode.FileSize; }
        }

        public IEnumerable<StreamExtent> Extents
        {
            get { return new[] { new StreamExtent(0, Capacity) }; }
        }

        public int Read(long pos, byte[] buffer, int offset, int count)
        {
            if (pos > _inode.FileSize)
            {
                return 0;
            }

            long startOfFragment = _blockLengths.Length * _context.SuperBlock.BlockSize;
            long currentPos = pos;
            int totalRead = 0;
            int totalToRead = (int)Math.Min(_inode.FileSize - pos, count);
            int currentBlock = 0;
            long currentBlockDiskStart = _inode.StartBlock;
            while (totalRead < totalToRead)
            {
                if (currentPos >= startOfFragment)
                {
                    int read = ReadFrag((int)(currentPos - startOfFragment), buffer, offset + totalRead,
                        totalToRead - totalRead);
                    return totalRead + read;
                }

                int targetBlock = (int)(currentPos / _context.SuperBlock.BlockSize);
                while (currentBlock < targetBlock)
                {
                    currentBlockDiskStart += _blockLengths[currentBlock] & 0x7FFFFF;
                    ++currentBlock;
                }

                int blockOffset = (int)(pos % _context.SuperBlock.BlockSize);

                Block block = _context.ReadBlock(currentBlockDiskStart, _blockLengths[currentBlock]);

                int toCopy = Math.Min(block.Available - blockOffset, totalToRead - totalRead);
                Array.Copy(block.Data, blockOffset, buffer, offset + totalRead, toCopy);
                totalRead += toCopy;
                currentPos += toCopy;
            }

            return totalRead;
        }

        public void Write(long pos, byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public void Clear(long pos, int count)
        {
            throw new NotSupportedException();
        }

        public void Flush() {}

        public void SetCapacity(long value)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return StreamExtent.Intersect(Extents, new StreamExtent(start, count));
        }

        private int ReadFrag(int pos, byte[] buffer, int offset, int count)
        {
            int fragRecordsPerBlock = 8192 / FragmentRecord.RecordSize;
            int fragTable = (int)_inode.FragmentKey / fragRecordsPerBlock;
            int recordOffset = (int)(_inode.FragmentKey % fragRecordsPerBlock) * FragmentRecord.RecordSize;

            byte[] fragRecordData = new byte[FragmentRecord.RecordSize];

            _context.FragmentTableReaders[fragTable].SetPosition(0, recordOffset);
            _context.FragmentTableReaders[fragTable].Read(fragRecordData, 0, fragRecordData.Length);

            FragmentRecord fragRecord = new FragmentRecord();
            fragRecord.ReadFrom(fragRecordData, 0);

            Block frag = _context.ReadBlock(fragRecord.StartBlock, fragRecord.CompressedSize);

            // Attempt to read data beyond end of fragment
            if (pos > frag.Available)
            {
                return 0;
            }

            int toCopy = (int)Math.Min(frag.Available - (_inode.FragmentOffset + pos), count);
            Array.Copy(frag.Data, (int)(_inode.FragmentOffset + pos), buffer, offset, toCopy);
            return toCopy;
        }
    }
}
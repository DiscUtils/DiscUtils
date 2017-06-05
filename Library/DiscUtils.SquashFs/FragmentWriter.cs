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
    internal sealed class FragmentWriter
    {
        private readonly BuilderContext _context;

        private readonly byte[] _currentBlock;
        private int _currentOffset;

        private readonly List<FragmentRecord> _fragmentBlocks;

        public FragmentWriter(BuilderContext context)
        {
            _context = context;
            _currentBlock = new byte[context.DataBlockSize];
            _currentOffset = 0;

            _fragmentBlocks = new List<FragmentRecord>();
        }

        public int FragmentCount { get; private set; }

        public void Flush()
        {
            if (_currentOffset != 0)
            {
                NextBlock();
            }
        }

        public uint WriteFragment(int length, out uint offset)
        {
            if (_currentBlock.Length - _currentOffset < length)
            {
                NextBlock();
                _currentOffset = 0;
            }

            offset = (uint)_currentOffset;
            Array.Copy(_context.IoBuffer, 0, _currentBlock, _currentOffset, length);
            _currentOffset += length;

            ++FragmentCount;

            return (uint)_fragmentBlocks.Count;
        }

        internal long Persist()
        {
            if (_fragmentBlocks.Count <= 0)
            {
                return -1;
            }

            if (_fragmentBlocks.Count * FragmentRecord.RecordSize > _context.DataBlockSize)
            {
                throw new NotImplementedException("Large numbers of fragments");
            }

            // Persist the table that references the block containing the fragment records
            long blockPos = _context.RawStream.Position;
            int recordSize = FragmentRecord.RecordSize;
            byte[] buffer = new byte[_fragmentBlocks.Count * recordSize];
            for (int i = 0; i < _fragmentBlocks.Count; ++i)
            {
                _fragmentBlocks[i].WriteTo(buffer, i * recordSize);
            }

            MetablockWriter writer = new MetablockWriter();
            writer.Write(buffer, 0, buffer.Length);
            writer.Persist(_context.RawStream);

            long tablePos = _context.RawStream.Position;
            byte[] tableBuffer = new byte[8];
            EndianUtilities.WriteBytesLittleEndian(blockPos, tableBuffer, 0);
            _context.RawStream.Write(tableBuffer, 0, 8);

            return tablePos;
        }

        private void NextBlock()
        {
            long position = _context.RawStream.Position;

            uint writeLen = _context.WriteDataBlock(_currentBlock, 0, _currentOffset);
            FragmentRecord blockRecord = new FragmentRecord
            {
                StartBlock = position,
                CompressedSize = (int)writeLen
            };

            _fragmentBlocks.Add(blockRecord);
        }
    }
}
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

namespace DiscUtils
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class BlockCache<T>
        where T : Block, new()
    {
        private int _blockSize;
        private Dictionary<long, T> _blocks;
        private LinkedList<T> _lru;
        private List<T> _freeBlocks;
        private int _blocksCreated;
        private int _totalBlocks;

        private int _freeBlockCount;

        public BlockCache(int blockSize, int blockCount)
        {
            _blockSize = blockSize;
            _totalBlocks = blockCount;

            _blocks = new Dictionary<long, T>();
            _lru = new LinkedList<T>();
            _freeBlocks = new List<T>(_totalBlocks);

            _freeBlockCount = _totalBlocks;
        }

        public int FreeBlockCount
        {
            get { return _freeBlockCount; }
        }

        public bool ContainsBlock(long position)
        {
            return _blocks.ContainsKey(position);
        }

        public bool TryGetBlock(long position, out T block)
        {
            if (_blocks.TryGetValue(position, out block))
            {
                _lru.Remove(block);
                _lru.AddFirst(block);
                return true;
            }

            return false;
        }

        public T GetBlock(long position)
        {
            T result;

            if (TryGetBlock(position, out result))
            {
                return result;
            }

            result = GetFreeBlock();
            result.Position = position;
            result.Available = -1;
            StoreBlock(result);

            return result;
        }

        public void ReleaseBlock(long position)
        {
            T block;
            if (_blocks.TryGetValue(position, out block))
            {
                _blocks.Remove(position);
                _lru.Remove(block);
                _freeBlocks.Add(block);
                _freeBlockCount++;
            }
        }

        private void StoreBlock(T block)
        {
            _blocks[block.Position] = block;
            _lru.AddFirst(block);
        }

        private T GetFreeBlock()
        {
            T block;

            if (_freeBlocks.Count > 0)
            {
                int idx = _freeBlocks.Count - 1;
                block = _freeBlocks[idx];
                _freeBlocks.RemoveAt(idx);
                _freeBlockCount--;
            }
            else if (_blocksCreated < _totalBlocks)
            {
                block = new T();
                block.Data = new byte[_blockSize];
                _blocksCreated++;
                _freeBlockCount--;
            }
            else
            {
                block = _lru.Last.Value;
                _lru.RemoveLast();
                _blocks.Remove(block.Position);
            }

            return block;
        }
    }
}

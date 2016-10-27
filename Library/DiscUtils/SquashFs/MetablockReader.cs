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

namespace DiscUtils.SquashFs
{
    using System;

    internal sealed class MetablockReader
    {
        private Context _context;
        private long _start;

        private long _currentBlockStart;
        private int _currentOffset;

        public MetablockReader(Context context, long start)
        {
            _context = context;
            _start = start;
        }

        public void SetPosition(MetadataRef position)
        {
            SetPosition(position.Block, position.Offset);
        }

        public void SetPosition(long blockStart, int blockOffset)
        {
            if (blockOffset < 0 || blockOffset >= VfsSquashFileSystemReader.MetadataBufferSize)
            {
                throw new ArgumentOutOfRangeException("blockOffset", blockOffset, "Offset must be positive and less than block size");
            }

            _currentBlockStart = blockStart;
            _currentOffset = blockOffset;
        }

        public long DistanceFrom(long blockStart, int blockOffset)
        {
            return ((_currentBlockStart - blockStart) * VfsSquashFileSystemReader.MetadataBufferSize)
                + (_currentOffset - blockOffset);
        }

        public void Skip(int count)
        {
            Metablock block = _context.ReadMetaBlock(_start + _currentBlockStart);

            int totalSkipped = 0;
            while (totalSkipped < count)
            {
                if (_currentOffset >= block.Available)
                {
                    int oldAvailable = block.Available;
                    block = _context.ReadMetaBlock(block.NextBlockStart);
                    _currentBlockStart = block.Position - _start;
                    _currentOffset -= oldAvailable;
                }

                int toSkip = Math.Min(count - totalSkipped, block.Available - _currentOffset);
                totalSkipped += toSkip;
                _currentOffset += toSkip;
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            Metablock block = _context.ReadMetaBlock(_start + _currentBlockStart);

            int totalRead = 0;
            while (totalRead < count)
            {
                if (_currentOffset >= block.Available)
                {
                    int oldAvailable = block.Available;
                    block = _context.ReadMetaBlock(block.NextBlockStart);
                    _currentBlockStart = block.Position - _start;
                    _currentOffset -= oldAvailable;
                }

                int toRead = Math.Min(count - totalRead, block.Available - _currentOffset);
                Array.Copy(block.Data, _currentOffset, buffer, offset + totalRead, toRead);
                totalRead += toRead;
                _currentOffset += toRead;
            }

            return totalRead;
        }

        public uint ReadUInt()
        {
            Metablock block = _context.ReadMetaBlock(_start + _currentBlockStart);

            if (block.Available - _currentOffset < 4)
            {
                byte[] buffer = new byte[4];
                Read(buffer, 0, 4);
                return Utilities.ToUInt32LittleEndian(buffer, 0);
            }
            else
            {
                uint result = Utilities.ToUInt32LittleEndian(block.Data, _currentOffset);
                _currentOffset += 4;
                return result;
            }
        }

        public int ReadInt()
        {
            Metablock block = _context.ReadMetaBlock(_start + _currentBlockStart);

            if (block.Available - _currentOffset < 4)
            {
                byte[] buffer = new byte[4];
                Read(buffer, 0, 4);
                return Utilities.ToInt32LittleEndian(buffer, 0);
            }
            else
            {
                int result = Utilities.ToInt32LittleEndian(block.Data, _currentOffset);
                _currentOffset += 4;
                return result;
            }
        }

        public ushort ReadUShort()
        {
            Metablock block = _context.ReadMetaBlock(_start + _currentBlockStart);

            if (block.Available - _currentOffset < 2)
            {
                byte[] buffer = new byte[2];
                Read(buffer, 0, 2);
                return Utilities.ToUInt16LittleEndian(buffer, 0);
            }
            else
            {
                ushort result = Utilities.ToUInt16LittleEndian(block.Data, _currentOffset);
                _currentOffset += 2;
                return result;
            }
        }

        public short ReadShort()
        {
            Metablock block = _context.ReadMetaBlock(_start + _currentBlockStart);

            if (block.Available - _currentOffset < 2)
            {
                byte[] buffer = new byte[2];
                Read(buffer, 0, 2);
                return Utilities.ToInt16LittleEndian(buffer, 0);
            }
            else
            {
                short result = Utilities.ToInt16LittleEndian(block.Data, _currentOffset);
                _currentOffset += 2;
                return result;
            }
        }

        public string ReadString(int len)
        {
            Metablock block = _context.ReadMetaBlock(_start + _currentBlockStart);

            if (block.Available - _currentOffset < len)
            {
                byte[] buffer = new byte[len];
                Read(buffer, 0, len);
                return Utilities.BytesToString(buffer, 0, len);
            }
            else
            {
                string result = Utilities.BytesToString(block.Data, _currentOffset, len);
                _currentOffset += len;
                return result;
            }
        }
    }
}

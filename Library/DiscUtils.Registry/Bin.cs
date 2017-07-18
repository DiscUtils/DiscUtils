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

namespace DiscUtils.Registry
{
    /// <summary>
    /// An internal structure within registry files, bins are the major unit of allocation in a registry hive.
    /// </summary>
    /// <remarks>Bins are divided into multiple cells, that contain actual registry data.</remarks>
    internal sealed class Bin
    {
        private readonly byte[] _buffer;
        private readonly Stream _fileStream;

        private readonly List<Range<int, int>> _freeCells;

        private readonly BinHeader _header;
        private readonly RegistryHive _hive;
        private readonly long _streamPos;

        public Bin(RegistryHive hive, Stream stream)
        {
            _hive = hive;
            _fileStream = stream;
            _streamPos = stream.Position;

            stream.Position = _streamPos;
            byte[] buffer = StreamUtilities.ReadExact(stream, 0x20);
            _header = new BinHeader();
            _header.ReadFrom(buffer, 0);

            _fileStream.Position = _streamPos;
            _buffer = StreamUtilities.ReadExact(_fileStream, _header.BinSize);

            // Gather list of all free cells.
            _freeCells = new List<Range<int, int>>();
            int pos = 0x20;
            while (pos < _buffer.Length)
            {
                int size = EndianUtilities.ToInt32LittleEndian(_buffer, pos);
                if (size > 0)
                {
                    _freeCells.Add(new Range<int, int>(pos, size));
                }

                pos += Math.Abs(size);
            }
        }

        public Cell TryGetCell(int index)
        {
            int size = EndianUtilities.ToInt32LittleEndian(_buffer, index - _header.FileOffset);
            if (size >= 0)
            {
                return null;
            }

            return Cell.Parse(_hive, index, _buffer, index + 4 - _header.FileOffset);
        }

        public void FreeCell(int index)
        {
            int freeIndex = index - _header.FileOffset;

            int len = EndianUtilities.ToInt32LittleEndian(_buffer, freeIndex);
            if (len >= 0)
            {
                throw new ArgumentException("Attempt to free non-allocated cell");
            }

            len = Math.Abs(len);

            // If there's a free cell before this one, combine
            int i = 0;
            while (i < _freeCells.Count && _freeCells[i].Offset < freeIndex)
            {
                if (_freeCells[i].Offset + _freeCells[i].Count == freeIndex)
                {
                    freeIndex = _freeCells[i].Offset;
                    len += _freeCells[i].Count;
                    _freeCells.RemoveAt(i);
                }
                else
                {
                    ++i;
                }
            }

            // If there's a free cell after this one, combine
            if (i < _freeCells.Count && _freeCells[i].Offset == freeIndex + len)
            {
                len += _freeCells[i].Count;
                _freeCells.RemoveAt(i);
            }

            // Record the new free cell
            _freeCells.Insert(i, new Range<int, int>(freeIndex, len));

            // Free cells are indicated by length > 0
            EndianUtilities.WriteBytesLittleEndian(len, _buffer, freeIndex);

            _fileStream.Position = _streamPos + freeIndex;
            _fileStream.Write(_buffer, freeIndex, 4);
        }

        public bool UpdateCell(Cell cell)
        {
            int index = cell.Index - _header.FileOffset;
            int allocSize = Math.Abs(EndianUtilities.ToInt32LittleEndian(_buffer, index));

            int newSize = cell.Size + 4;
            if (newSize > allocSize)
            {
                return false;
            }

            cell.WriteTo(_buffer, index + 4);

            _fileStream.Position = _streamPos + index;
            _fileStream.Write(_buffer, index, newSize);

            return true;
        }

        public byte[] ReadRawCellData(int cellIndex, int maxBytes)
        {
            int index = cellIndex - _header.FileOffset;
            int len = Math.Abs(EndianUtilities.ToInt32LittleEndian(_buffer, index));
            byte[] result = new byte[Math.Min(len - 4, maxBytes)];
            Array.Copy(_buffer, index + 4, result, 0, result.Length);
            return result;
        }

        internal bool WriteRawCellData(int cellIndex, byte[] data, int offset, int count)
        {
            int index = cellIndex - _header.FileOffset;
            int allocSize = Math.Abs(EndianUtilities.ToInt32LittleEndian(_buffer, index));

            int newSize = count + 4;
            if (newSize > allocSize)
            {
                return false;
            }

            Array.Copy(data, offset, _buffer, index + 4, count);

            _fileStream.Position = _streamPos + index;
            _fileStream.Write(_buffer, index, newSize);

            return true;
        }

        internal int AllocateCell(int size)
        {
            if (size < 8 || size % 8 != 0)
            {
                throw new ArgumentException("Invalid cell size");
            }

            // Very inefficient algorithm - will lead to fragmentation
            for (int i = 0; i < _freeCells.Count; ++i)
            {
                int result = _freeCells[i].Offset + _header.FileOffset;
                if (_freeCells[i].Count > size)
                {
                    // Record the newly allocated cell
                    EndianUtilities.WriteBytesLittleEndian(-size, _buffer, _freeCells[i].Offset);
                    _fileStream.Position = _streamPos + _freeCells[i].Offset;
                    _fileStream.Write(_buffer, _freeCells[i].Offset, 4);

                    // Keep the remainder of the free buffer as unallocated
                    _freeCells[i] = new Range<int, int>(_freeCells[i].Offset + size, _freeCells[i].Count - size);
                    EndianUtilities.WriteBytesLittleEndian(_freeCells[i].Count, _buffer, _freeCells[i].Offset);
                    _fileStream.Position = _streamPos + _freeCells[i].Offset;
                    _fileStream.Write(_buffer, _freeCells[i].Offset, 4);

                    return result;
                }
                if (_freeCells[i].Count == size)
                {
                    // Record the whole of the free buffer as a newly allocated cell
                    EndianUtilities.WriteBytesLittleEndian(-size, _buffer, _freeCells[i].Offset);
                    _fileStream.Position = _streamPos + _freeCells[i].Offset;
                    _fileStream.Write(_buffer, _freeCells[i].Offset, 4);

                    _freeCells.RemoveAt(i);
                    return result;
                }
            }

            return -1;
        }
    }
}
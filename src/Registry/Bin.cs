//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Registry
{
    /// <summary>
    /// An internal structure within registry files, bins are the major unit of allocation in a registry hive.
    /// </summary>
    /// <remarks>Bins are divided into multiple cells, that contain actual registry data.</remarks>
    internal class Bin
    {
        internal const int Size = 4096;

        private Stream _fileStream;
        private long _streamPos;

        private BinHeader _header;
        private byte[] _buffer;

        private List<Range<int, int>> _freeCells;

        public Bin(Stream stream)
        {
            _fileStream = stream;
            _streamPos = stream.Position;

            stream.Position = _streamPos;
            byte[] buffer = Utilities.ReadFully(stream, 0x20);
            _header = new BinHeader();
            _header.ReadFrom(buffer, 0);

            _fileStream.Position = _streamPos;
            _buffer = Utilities.ReadFully(_fileStream, _header.BinSize);

            // Gather list of all free cells.
            _freeCells = new List<Range<int, int>>();
            int pos = 0x20;
            while (pos < _buffer.Length)
            {
                int size = Utilities.ToInt32LittleEndian(_buffer, pos);
                if (size > 0)
                {
                    _freeCells.Add(new Range<int, int>(pos, size));
                }
                pos += Math.Abs(size);
            }
        }

        public BinHeader Header
        {
            get { return _header; }
        }

        public Cell this[int index]
        {
            get
            {
                int size = Utilities.ToInt32LittleEndian(_buffer, index);
                if (size >= 0)
                {
                    throw new ArgumentException("index is not allocated", "index");
                }
                return Cell.Parse(_buffer, index + 4);
            }
        }

        public void FreeCell(int index)
        {
            int freeIndex = index;

            int len = Utilities.ToInt32LittleEndian(_buffer, freeIndex);
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
                ++i;
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
            Utilities.WriteBytesLittleEndian(len, _buffer, freeIndex);

            _fileStream.Position = _streamPos + freeIndex;
            _fileStream.Write(_buffer, freeIndex, 4);
        }

        public bool UpdateCell(int index, Cell cell)
        {
            int allocSize = Math.Abs(Utilities.ToInt32LittleEndian(_buffer, index));

            int newSize = cell.Size + 4;
            if (newSize> allocSize)
            {
                return false;
            }

            cell.WriteTo(_buffer, index + 4);

            _fileStream.Position = _streamPos + index;
            _fileStream.Write(_buffer, index, newSize);

            return true;
        }

        public byte[] RawCellData(int index, int maxBytes)
        {
            int len = Math.Abs(Utilities.ToInt32LittleEndian(_buffer, index));
            byte[] result = new byte[Math.Min(len - 4, maxBytes)];
            Array.Copy(_buffer, index + 4, result, 0, result.Length);
            return result;
        }


        internal object WriteRawCellData(int index, byte[] data, int offset, int count)
        {
            int allocSize = Math.Abs(Utilities.ToInt32LittleEndian(_buffer, index));

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
    }
}

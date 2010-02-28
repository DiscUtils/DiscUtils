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
using System.IO;

namespace DiscUtils.Ntfs
{
    internal sealed class Bitmap
    {
        private Stream _stream;
        private long _maxIndex;

        private SparseMemoryBuffer _bitmap;

        private long _nextAvailable;

        public Bitmap(Stream stream, long maxIndex)
        {
            _stream = stream;
            _maxIndex = maxIndex;
            _bitmap = new SparseMemoryBuffer(128);

            if (stream.Length > 10 * Sizes.OneMiB)
            {
                throw new NotImplementedException("Large Bitmap");
            }

            _stream.Position = 0;
            byte[] buffer = Utilities.ReadFully(stream, (int)stream.Length);
            _bitmap.Write(0, buffer, 0, buffer.Length);
        }

        public bool IsPresent(long index)
        {
            long byteIdx = index / 8;
            int mask = 1 << (int)(index % 8);

            return (_bitmap[byteIdx] & mask) != 0;
        }

        public void MarkPresent(long index)
        {
            long byteIdx = index / 8;
            byte mask = (byte)(1 << (byte)(index % 8));

            _bitmap[byteIdx] |= mask;

            if (byteIdx >= _stream.Length)
            {
                _stream.Position = Utilities.RoundUp(byteIdx + 1, 8) - 1;
                _stream.WriteByte(0);
            }

            _stream.Position = byteIdx;
            _stream.WriteByte(_bitmap[byteIdx]);
            _stream.Flush();
        }

        public void MarkPresentRange(long index, long count)
        {
            if (count <= 0)
            {
                return;
            }

            for (long i = index; i < index + count; ++i)
            {
                long byteIdx = i / 8;
                byte mask = (byte)(1 << (byte)(i % 8));
                _bitmap[byteIdx] |= mask;
            }

            long firstByte = index / 8;
            long lastByte = (index + count - 1) / 8;

            if (lastByte >= _stream.Length)
            {
                _stream.Position = Utilities.RoundUp(lastByte + 1, 8) - 1;
                _stream.WriteByte(0);
            }

            byte[] buffer = new byte[lastByte - firstByte + 1];
            _bitmap.Read(firstByte, buffer, 0, buffer.Length);

            _stream.Position = firstByte;
            _stream.Write(buffer, 0, buffer.Length);
            _stream.Flush();
        }

        public void MarkAbsent(long index)
        {
            long byteIdx = index / 8;
            byte mask = (byte)(1 << (byte)(index % 8));

            if (byteIdx < _stream.Length)
            {
                _bitmap[byteIdx] &= (byte)~mask;

                _stream.Position = byteIdx;
                _stream.WriteByte(_bitmap[byteIdx]);
                _stream.Flush();
            }

            if (index < _nextAvailable)
            {
                _nextAvailable = index;
            }
        }

        internal void MarkAbsentRange(long index, long count)
        {
            for (long i = index; i < index + count; ++i)
            {
                long byteIdx = i / 8;
                byte mask = (byte)~(1 << (byte)(i % 8));

                _bitmap[byteIdx] &= mask;
            }

            long firstByte = index / 8;
            long lastByte = (index + count) / 8;

            if (lastByte >= _stream.Length)
            {
                _stream.Position = Utilities.RoundUp(lastByte + 1, 8) - 1;
                _stream.WriteByte(0);
            }

            byte[] buffer = new byte[lastByte - firstByte + 1];
            _bitmap.Read(firstByte, buffer, 0, buffer.Length);

            _stream.Position = firstByte;
            _stream.Write(buffer, 0, buffer.Length);
            _stream.Flush();

            if (index < _nextAvailable)
            {
                _nextAvailable = index;
            }
        }

        internal long AllocateFirstAvailable(long minValue)
        {
            long i = Math.Max(minValue, _nextAvailable);
            while (IsPresent(i) && i < _maxIndex)
            {
                ++i;
            }

            if (i < _maxIndex)
            {
                MarkPresent(i);
                _nextAvailable = i + 1;
                return i;
            }
            else
            {
                return -1;
            }
        }

        internal long SetTotalEntries(long numEntries)
        {
            long length = Utilities.RoundUp(numEntries / 8, 8);
            _stream.SetLength(length);
            return length * 8;
        }
    }
}

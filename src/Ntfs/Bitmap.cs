//
// Copyright (c) 2008-2010, Kenneth Bell
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

        private BlockCacheStream _bitmap;

        private long _nextAvailable;

        public Bitmap(Stream stream, long maxIndex)
        {
            _stream = stream;
            _maxIndex = maxIndex;
            _bitmap = new BlockCacheStream(SparseStream.FromStream(stream, Ownership.None), Ownership.None);
        }

        public bool IsPresent(long index)
        {
            long byteIdx = index / 8;
            int mask = 1 << (int)(index % 8);
            return (GetByte(byteIdx) & mask) != 0;
        }

        public void MarkPresent(long index)
        {
            long byteIdx = index / 8;
            byte mask = (byte)(1 << (byte)(index % 8));

            if (byteIdx >= _bitmap.Length)
            {
                _bitmap.Position = Utilities.RoundUp(byteIdx + 1, 8) - 1;
                _bitmap.WriteByte(0);
            }

            SetByte(byteIdx, (byte)(GetByte(byteIdx) | mask));
        }

        public void MarkPresentRange(long index, long count)
        {
            if (count <= 0)
            {
                return;
            }

            long firstByte = index / 8;
            long lastByte = (index + count - 1) / 8;

            if (lastByte >= _bitmap.Length)
            {
                _bitmap.Position = Utilities.RoundUp(lastByte + 1, 8) - 1;
                _bitmap.WriteByte(0);
            }

            byte[] buffer = new byte[lastByte - firstByte + 1];
            buffer[0] = GetByte(firstByte);
            if (buffer.Length != 1)
            {
                buffer[buffer.Length - 1] = GetByte(lastByte);
            }

            for (long i = index; i < index + count; ++i)
            {
                long byteIdx = (i / 8) - firstByte;
                byte mask = (byte)(1 << (byte)(i % 8));

                buffer[byteIdx] |= mask;
            }

            SetBytes(firstByte, buffer);
        }

        public void MarkAbsent(long index)
        {
            long byteIdx = index / 8;
            byte mask = (byte)(1 << (byte)(index % 8));

            if (byteIdx < _stream.Length)
            {
                SetByte(byteIdx, (byte)(GetByte(byteIdx) & ~mask));
            }

            if (index < _nextAvailable)
            {
                _nextAvailable = index;
            }
        }

        internal void MarkAbsentRange(long index, long count)
        {
            if (count <= 0)
            {
                return;
            }

            long firstByte = index / 8;
            long lastByte = (index + count - 1) / 8;
            if (lastByte >= _bitmap.Length)
            {
                _bitmap.Position = Utilities.RoundUp(lastByte + 1, 8) - 1;
                _bitmap.WriteByte(0);
            }

            byte[] buffer = new byte[lastByte - firstByte + 1];
            buffer[0] = GetByte(firstByte);
            if (buffer.Length != 1)
            {
                buffer[buffer.Length - 1] = GetByte(lastByte);
            }


            for (long i = index; i < index + count; ++i)
            {
                long byteIdx = i / 8 - firstByte;
                byte mask = (byte)(1 << (byte)(i % 8));

                buffer[byteIdx] &= (byte)~mask;
            }

            SetBytes(firstByte, buffer);

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
            long length = Utilities.RoundUp(Utilities.Ceil(numEntries, 8), 8);
            _stream.SetLength(length);
            return length * 8;
        }

        private byte GetByte(long index)
        {
            if (index >= _bitmap.Length)
            {
                return 0;
            }

            byte[] buffer = new byte[1];
            _bitmap.Position = index;
            if (_bitmap.Read(buffer, 0, 1) != 0)
            {
                return buffer[0];
            }
            else
            {
                return 0;
            }
        }

        private void SetByte(long index, byte value)
        {
            byte[] buffer = new byte[] { value };
            _bitmap.Position = index;
            _bitmap.Write(buffer, 0, 1);
            _bitmap.Flush();
        }

        private void SetBytes(long index, byte[] buffer)
        {
            _bitmap.Position = index;
            _bitmap.Write(buffer, 0, buffer.Length);
            _bitmap.Flush();
        }

    }
}

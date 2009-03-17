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
using DiscUtils.Ntfs.Attributes;

namespace DiscUtils.Ntfs
{
    internal class Bitmap
    {
        private byte[] _bitmap;
        private BitmapAttribute _fileAttr;

        public Bitmap(BitmapAttribute fileAttr)
        {
            if (fileAttr.Length > 100 * 1024)
            {
                throw new NotImplementedException("Large Bitmap");
            }

            _fileAttr = fileAttr;
            using (Stream stream = _fileAttr.Open(FileAccess.Read))
            {
                _bitmap = Utilities.ReadFully(stream, (int)_fileAttr.Length);
            }
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

            using (Stream stream = _fileAttr.Open(FileAccess.ReadWrite))
            {
                stream.Position = byteIdx;
                stream.WriteByte(_bitmap[byteIdx]);
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

            using (Stream stream = _fileAttr.Open(FileAccess.ReadWrite))
            {
                long firstByte = index / 8;
                long lastByte = (index + count) / 8;

                stream.Position = firstByte;
                stream.Write(_bitmap, (int)firstByte, (int)(lastByte - firstByte + 1));
            }
        }
    }
}

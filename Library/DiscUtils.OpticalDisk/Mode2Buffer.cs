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

namespace DiscUtils.OpticalDisk
{
    /// <summary>
    /// Interprets a Mode 2 image.
    /// </summary>
    /// <remarks>
    /// Effectively just strips the additional header / footer from the Mode 2 sector
    /// data - does not attempt to validate the information.
    /// </remarks>
    internal class Mode2Buffer : IBuffer
    {
        private readonly byte[] _iobuffer;
        private readonly IBuffer _wrapped;

        public Mode2Buffer(IBuffer toWrap)
        {
            _wrapped = toWrap;
            _iobuffer = new byte[DiscImageFile.Mode2SectorSize];
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
            get { return _wrapped.Capacity / DiscImageFile.Mode2SectorSize * DiscImageFile.Mode1SectorSize; }
        }

        public IEnumerable<StreamExtent> Extents
        {
            get { yield return new StreamExtent(0, Capacity); }
        }

        public int Read(long pos, byte[] buffer, int offset, int count)
        {
            int totalToRead = (int)Math.Min(Capacity - pos, count);
            int totalRead = 0;

            while (totalRead < totalToRead)
            {
                long thisPos = pos + totalRead;
                long sector = thisPos / DiscImageFile.Mode1SectorSize;
                int sectorOffset = (int)(thisPos - sector * DiscImageFile.Mode1SectorSize);

                StreamUtilities.ReadExact(_wrapped, sector * DiscImageFile.Mode2SectorSize, _iobuffer, 0, DiscImageFile.Mode2SectorSize);

                int bytesToCopy = Math.Min(DiscImageFile.Mode1SectorSize - sectorOffset, totalToRead - totalRead);
                Array.Copy(_iobuffer, 24 + sectorOffset, buffer, offset + totalRead, bytesToCopy);
                totalRead += bytesToCopy;
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

        public void Flush()
        {
            throw new NotSupportedException();
        }

        public void SetCapacity(long value)
        {
            throw new NotSupportedException();
        }

        public IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            long capacity = Capacity;
            if (start < capacity)
            {
                long end = Math.Min(start + count, capacity);
                yield return new StreamExtent(start, end - start);
            }
        }
    }
}
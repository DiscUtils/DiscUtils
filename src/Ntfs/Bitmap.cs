//
// Copyright (c) 2008, Kenneth Bell
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
    internal class Bitmap
    {
        private byte[] _bitmap;

        public Bitmap(BitmapFileAttribute fileAttr)
        {
            if (fileAttr.Length > 100 * 1024)
            {
                throw new NotImplementedException("Large Bitmap");
            }

            using (Stream stream = fileAttr.Open())
            {
                _bitmap = Utilities.ReadFully(stream, (int)fileAttr.Length);
            }
        }

        public bool IsPresent(long index)
        {
            long byteIdx = index / 8;
            int mask = 1 << (int)(index % 8);

            return (_bitmap[byteIdx] & mask) != 0;
        }
    }
}

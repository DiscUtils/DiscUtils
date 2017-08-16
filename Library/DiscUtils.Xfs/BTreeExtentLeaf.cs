//
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Xfs
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class BTreeExtentLeaf : BTreeExtentHeader
    {
        public Extent[] Extents { get; private set; }

        public override int Size
        {
            get { return base.Size + (NumberOfRecords * 0x10); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            offset += base.ReadFrom(buffer, offset);
            if (Level != 0)
                throw new IOException("invalid B+tree level - expected 0");
            Extents = new Extent[NumberOfRecords];
            for (int i = 0; i < NumberOfRecords; i++)
            {
                var rec = new Extent();
                offset += rec.ReadFrom(buffer, offset);
                Extents[i] = rec;
            }
            return Size;
        }

        /// <inheritdoc />
        public override void LoadBtree(Context context)
        {
        }

        /// <inheritdoc />
        public override IList<Extent> GetExtents()
        {
            return Extents;
        }
    }
}

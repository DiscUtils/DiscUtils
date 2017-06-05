//
// Copyright (c) 2016, Bianco Veigel
// Copyright (c) 2017, Timo Walter
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

using System.IO;

namespace DiscUtils.Xfs
{
    using System;

    internal class BTreeInodeLeaf : BtreeHeader
    {
        public BTreeInodeRecord[] Records { get; private set; }
        public override int Size
        {
            get { return base.Size + (NumberOfRecords * 0x10); }
        }

        public BTreeInodeLeaf(uint superBlockVersion) : base(superBlockVersion){}

        public override int ReadFrom(byte[] buffer, int offset)
        {
            base.ReadFrom(buffer, offset);
            offset += base.Size;
            if (Level != 0)
                throw new IOException("invalid B+tree level - expected 1");
            Records = new BTreeInodeRecord[NumberOfRecords];
            for (int i = 0; i < NumberOfRecords; i++)
            {
                var rec = new BTreeInodeRecord();
                offset += rec.ReadFrom(buffer, offset);
                Records[i] = rec;
            }
            return Size;
        }

        public override void LoadBtree(AllocationGroup ag)
        {
            
        }
    }
}

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

using DiscUtils.Streams;

namespace DiscUtils.Vhd
{
    internal sealed class DiskExtent : VirtualDiskExtent
    {
        private readonly DiskImageFile _file;

        public DiskExtent(DiskImageFile file)
        {
            _file = file;
        }

        public override long Capacity
        {
            get { return _file.Capacity; }
        }

        public override bool IsSparse
        {
            get { return _file.IsSparse; }
        }

        public override long StoredSize
        {
            get { return _file.StoredSize; }
        }

        public override MappedStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            return _file.DoOpenContent(parent, ownsParent);
        }
    }
}
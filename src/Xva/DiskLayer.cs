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


namespace DiscUtils.Xva
{

    /// <summary>
    /// Class representing a single layer of an XVA disk.
    /// </summary>
    /// <remarks>XVA only supports a single layer.</remarks>
    public sealed class DiskLayer : VirtualDiskLayer
    {
        private long _capacity;

        internal DiskLayer(long capacity)
        {
            _capacity = capacity;
        }

        /// <summary>
        /// Gets a indication of whether the disk is 'sparse'.
        /// </summary>
        /// <remarks>Always true for XVA disks</remarks>
        public override bool IsSparse
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the capacity of the layer (in bytes).
        /// </summary>
        internal override long Capacity
        {
            get { return _capacity; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return null; }
        }
    }
}

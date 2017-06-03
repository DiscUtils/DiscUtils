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

namespace DiscUtils.Xva
{
    /// <summary>
    /// Class representing a single layer of an XVA disk.
    /// </summary>
    /// <remarks>XVA only supports a single layer.</remarks>
    public sealed class DiskLayer : VirtualDiskLayer
    {
        private readonly long _capacity;
        private readonly string _location;
        private readonly VirtualMachine _vm;

        internal DiskLayer(VirtualMachine vm, long capacity, string location)
        {
            _vm = vm;
            _capacity = capacity;
            _location = location;
        }

        /// <summary>
        /// Gets the capacity of the layer (in bytes).
        /// </summary>
        internal override long Capacity
        {
            get { return _capacity; }
        }

        /// <summary>
        /// Gets the disk's geometry.
        /// </summary>
        /// <remarks>The geometry is not stored with the disk, so this is at best
        /// a guess of the actual geometry.</remarks>
        public override Geometry Geometry
        {
            get { return Geometry.FromCapacity(_capacity); }
        }

        /// <summary>
        /// Gets a indication of whether the disk is 'sparse'.
        /// </summary>
        /// <remarks>Always true for XVA disks.</remarks>
        public override bool IsSparse
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the file is a differencing disk.
        /// </summary>
        public override bool NeedsParent
        {
            get { return false; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return null; }
        }

        /// <summary>
        /// Opens the content of the disk layer as a stream.
        /// </summary>
        /// <param name="parent">The parent file's content (if any).</param>
        /// <param name="ownsParent">Whether the created stream assumes ownership of parent stream.</param>
        /// <returns>The new content stream.</returns>
        public override SparseStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            if (ownsParent == Ownership.Dispose && parent != null)
            {
                parent.Dispose();
            }

            return new DiskStream(_vm.Archive, _capacity, _location);
        }

        /// <summary>
        /// Gets the possible locations of the parent file (if any).
        /// </summary>
        /// <returns>Array of strings, empty if no parent.</returns>
        public override string[] GetParentLocations()
        {
            return new string[0];
        }
    }
}
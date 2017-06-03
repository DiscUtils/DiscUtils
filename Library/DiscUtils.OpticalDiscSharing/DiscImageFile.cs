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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.OpticalDiscSharing
{
    internal sealed class DiscImageFile : VirtualDiskLayer
    {
        internal const int Mode1SectorSize = 2048;

        internal DiscImageFile(Uri uri, string userName, string password)
        {
            Content = new BufferStream(new DiscContentBuffer(uri, userName, password), FileAccess.Read);

            BlockCacheSettings cacheSettings = new BlockCacheSettings
            {
                BlockSize = (int)(32 * Sizes.OneKiB),
                OptimumReadSize = (int)(128 * Sizes.OneKiB)
            };

            Content = new BlockCacheStream(Content, Ownership.Dispose);
        }

        internal override long Capacity
        {
            get { return Content.Length; }
        }

        public SparseStream Content { get; }

        public override Geometry Geometry
        {
            // Note external sector size is always 2048
            get { return new Geometry(1, 1, 1, Mode1SectorSize); }
        }

        public override bool IsSparse
        {
            get { return false; }
        }

        public override bool NeedsParent
        {
            get { return false; }
        }

        internal override FileLocator RelativeFileLocator
        {
            get { return null; }
        }

        public override SparseStream OpenContent(SparseStream parent, Ownership ownsParent)
        {
            if (ownsParent == Ownership.Dispose && parent != null)
            {
                parent.Dispose();
            }

            return SparseStream.FromStream(Content, Ownership.None);
        }

        public override string[] GetParentLocations()
        {
            return new string[0];
        }
    }
}
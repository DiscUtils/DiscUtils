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
using DiscUtils.Streams;

namespace DiscUtils.Iso9660
{
    internal abstract class VolumeDescriptorDiskRegion : BuilderExtent
    {
        private byte[] _readCache;

        public VolumeDescriptorDiskRegion(long start)
            : base(start, IsoUtilities.SectorSize) {}

        public override void Dispose() {}

        public override void PrepareForRead()
        {
            _readCache = GetBlockData();
        }

        public override int Read(long diskOffset, byte[] buffer, int offset, int count)
        {
            long relPos = diskOffset - Start;

            int numRead = (int)Math.Min(count, _readCache.Length - relPos);

            Array.Copy(_readCache, (int)relPos, buffer, offset, numRead);

            return numRead;
        }

        public override void DisposeReadState()
        {
            _readCache = null;
        }

        protected abstract byte[] GetBlockData();
    }
}
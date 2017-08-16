//
// Copyright (c) 2008-2012, Kenneth Bell
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
using System.Globalization;
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class FreeSpaceTable
    {
        private long _fileSize;
        private List<StreamExtent> _freeExtents;

        public FreeSpaceTable(long fileSize)
        {
            // There used to be a check here that the file size is a multiple of 1 MB.
            // However, the
            // VHDX Format Specification 1.00 25-August-2012 only states this:
            //     "the only restriction being that all objects have 1 MB alignment within the file"
            // Which does not mean the file size has to be multiple of 1MiB.
            // (The last extent can be less than 1MiB and still all extent have 1 MiB alignment.)

            _freeExtents = new List<StreamExtent>();
            _freeExtents.Add(new StreamExtent(0, fileSize));
            _fileSize = fileSize;
        }

        public void ExtendTo(long fileSize, bool isFree)
        {
            if (fileSize % Sizes.OneMiB != 0)
            {
                throw new ArgumentException("VHDX space must be allocated on 1MB boundaries", nameof(fileSize));
            }

            if (fileSize < _fileSize)
            {
                throw new ArgumentOutOfRangeException(nameof(fileSize), "Attempt to extend file to smaller size",
                    fileSize.ToString(CultureInfo.InvariantCulture));
            }

            _fileSize = fileSize;

            if (isFree)
            {
                _freeExtents =
                    new List<StreamExtent>(StreamExtent.Union(_freeExtents,
                        new StreamExtent(_fileSize, fileSize - _fileSize)));
            }
        }

        public void Release(long start, long length)
        {
            ValidateRange(start, length, "release");
            _freeExtents = new List<StreamExtent>(StreamExtent.Union(_freeExtents, new StreamExtent(start, length)));
        }

        public void Reserve(long start, long length)
        {
            ValidateRange(start, length, "reserve");
            _freeExtents = new List<StreamExtent>(StreamExtent.Subtract(_freeExtents, new StreamExtent(start, length)));
        }

        public void Reserve(IEnumerable<StreamExtent> extents)
        {
            _freeExtents = new List<StreamExtent>(StreamExtent.Subtract(_freeExtents, extents));
        }

        public bool TryAllocate(long length, out long start)
        {
            if (length % Sizes.OneMiB != 0)
            {
                throw new ArgumentException("VHDX free space must be managed on 1MB boundaries", nameof(length));
            }

            for (int i = 0; i < _freeExtents.Count; ++i)
            {
                StreamExtent extent = _freeExtents[i];
                if (extent.Length == length)
                {
                    _freeExtents.RemoveAt(i);
                    start = extent.Start;
                    return true;
                }
                if (extent.Length > length)
                {
                    _freeExtents[i] = new StreamExtent(extent.Start + length, extent.Length - length);
                    start = extent.Start;
                    return true;
                }
            }

            start = 0;
            return false;
        }

        private void ValidateRange(long start, long length, string method)
        {
            if (start % Sizes.OneMiB != 0)
            {
                throw new ArgumentException("VHDX free space must be managed on 1MB boundaries", nameof(start));
            }

            if (length % Sizes.OneMiB != 0)
            {
                throw new ArgumentException("VHDX free space must be managed on 1MB boundaries", nameof(length));
            }

            if (start < 0 || start > _fileSize || length > _fileSize - start)
            {
                throw new ArgumentOutOfRangeException("Attempt to " + method + " space outside of file range");
            }
        }
    }
}
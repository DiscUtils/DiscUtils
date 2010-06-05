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

using System;
using System.IO;
using DiscUtils.Vfs;

namespace DiscUtils.Iso9660
{
    internal class File : IVfsFile
    {
        protected IsoContext _context;
        protected DirectoryRecord _dirEntry;

        public File(IsoContext context, DirectoryRecord dirEntry)
        {
            _context = context;
            _dirEntry = dirEntry;
        }

        public DateTime LastAccessTimeUtc
        {
            get
            {
                return _dirEntry.LastAccessTimeUtc;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public DateTime LastWriteTimeUtc
        {
            get
            {
                return _dirEntry.LastWriteTimeUtc;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public DateTime CreationTimeUtc
        {
            get
            {
                return _dirEntry.CreationTimeUtc;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public FileAttributes FileAttributes
        {
            get
            {
                return _dirEntry.FileAttributes;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public long FileLength
        {
            get { return _dirEntry.DataLength; }
        }

        public IBuffer FileContent
        {
            get
            {
                ExtentStream es = new ExtentStream(_context.DataStream, _dirEntry.LocationOfExtent, _dirEntry.DataLength, _dirEntry.FileUnitSize, _dirEntry.InterleaveGapSize);
                return new StreamBuffer(es, Ownership.Dispose);
            }
        }
    }
}

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

namespace DiscUtils.Ext
{
    using System;
    using System.IO;
    using DiscUtils.Vfs;

    internal class File : IVfsFile
    {
        private Context _context;
        private uint _inodeNum;
        private Inode _inode;
        private IBuffer _content;

        public File(Context context, uint inodeNum, Inode inode)
        {
            _context = context;
            _inodeNum = inodeNum;
            _inode = inode;
        }

        public DateTime LastAccessTimeUtc
        {
            get
            {
                return Utilities.DateTimeFromUnix(_inode.AccessTime);
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public DateTime LastWriteTimeUtc
        {
            get
            {
                return Utilities.DateTimeFromUnix(_inode.ModificationTime);
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public DateTime CreationTimeUtc
        {
            get
            {
                return Utilities.DateTimeFromUnix(_inode.CreationTime);
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public FileAttributes FileAttributes
        {
            get
            {
                return FromMode(_inode.Mode);
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public long FileLength
        {
            get { return _inode.FileSize; }
        }

        public IBuffer FileContent
        {
            get
            {
                if (_content == null)
                {
                    _content = _inode.GetContentBuffer(_context);
                }

                return _content;
            }
        }

        internal uint InodeNumber
        {
            get { return _inodeNum; }
        }

        internal Inode Inode
        {
            get { return _inode; }
        }

        protected Context Context
        {
            get { return _context; }
        }

        private static FileAttributes FromMode(uint mode)
        {
            return Utilities.FileAttributesFromUnixFileType((UnixFileType)((mode >> 12) & 0xF));
        }
    }
}

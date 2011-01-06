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

namespace DiscUtils.SquashFs
{
    using System;
    using System.IO;
    using DiscUtils.Vfs;

    internal class File : IVfsFile
    {
        private Context _context;
        private Inode _inode;
        private MetadataRef _inodeRef;

        private FileContentBuffer _content;

        public File(Context context, Inode inode, MetadataRef inodeRef)
        {
            _context = context;
            _inode = inode;
            _inodeRef = inodeRef;
        }

        public DateTime LastAccessTimeUtc
        {
            get
            {
                return _inode.ModificationTime;
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
                return _inode.ModificationTime;
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
                return _inode.ModificationTime;
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
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public long FileLength
        {
            get
            {
                DirectoryInode dirInode = _inode as DirectoryInode;
                if (dirInode != null)
                {
                    return dirInode.FileSize;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        public IBuffer FileContent
        {
            get
            {
                if (_content == null)
                {
                    _content = new FileContentBuffer(_context, (RegularInode)_inode, _inodeRef);
                }

                return _content;
            }
        }

        protected Context Context
        {
            get { return _context; }
        }
    }
}

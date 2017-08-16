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
using DiscUtils.Internal;
using DiscUtils.Streams;
using DiscUtils.Vfs;

namespace DiscUtils.SquashFs
{
    internal class File : IVfsFile
    {
        private FileContentBuffer _content;
        private readonly MetadataRef _inodeRef;

        public File(Context context, Inode inode, MetadataRef inodeRef)
        {
            Context = context;
            Inode = inode;
            _inodeRef = inodeRef;
        }

        protected Context Context { get; }

        internal Inode Inode { get; }

        public DateTime LastAccessTimeUtc
        {
            get { return Inode.ModificationTime; }

            set { throw new NotSupportedException(); }
        }

        public DateTime LastWriteTimeUtc
        {
            get { return Inode.ModificationTime; }

            set { throw new NotSupportedException(); }
        }

        public DateTime CreationTimeUtc
        {
            get { return Inode.ModificationTime; }

            set { throw new NotSupportedException(); }
        }

        public FileAttributes FileAttributes
        {
            get
            {
                UnixFileType fileType = VfsSquashFileSystemReader.FileTypeFromInodeType(Inode.Type);
                return Utilities.FileAttributesFromUnixFileType(fileType);
            }

            set { throw new NotSupportedException(); }
        }

        public long FileLength
        {
            get { return Inode.FileSize; }
        }

        public IBuffer FileContent
        {
            get
            {
                if (_content == null)
                {
                    _content = new FileContentBuffer(Context, (RegularInode)Inode, _inodeRef);
                }

                return _content;
            }
        }
    }
}
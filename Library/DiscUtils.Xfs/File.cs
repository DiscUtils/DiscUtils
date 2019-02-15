//
// Copyright (c) 2008-2011, Kenneth Bell
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Xfs
{
    using System;
    using System.IO;
    using DiscUtils.Vfs;
    using DiscUtils.Internal;
    using DiscUtils.Streams;

    internal class File : IVfsFile
    {
        protected readonly Context Context;
        protected readonly Inode Inode;
        private IBuffer _content;

        public File(Context context, Inode inode)
        {
            Context = context;
            Inode = inode;
        }

        public DateTime LastAccessTimeUtc
        {
            get { return Inode.AccessTime; }
            set { throw new NotImplementedException(); }
        }

        public DateTime LastWriteTimeUtc
        {
            get { return Inode.ModificationTime; }
            set { throw new NotImplementedException(); }
        }

        public DateTime CreationTimeUtc
        {
            get { return Inode.CreationTime; }
            set { throw new NotImplementedException(); }
        }

        public FileAttributes FileAttributes
        {
            get { return Utilities.FileAttributesFromUnixFileType(Inode.FileType); }
            set { throw new NotImplementedException(); }
        }

        public long FileLength
        {
            get { return (long) Inode.Length; }
        }

        public IBuffer FileContent
        {
            get
            {
                if (_content == null)
                {
                    _content = Inode.GetContentBuffer(Context);
                }

                return _content;
            }
        }
        
    }
}

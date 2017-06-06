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

namespace DiscUtils.Ext
{
    internal class File : IVfsFile
    {
        private IBuffer _content;

        public File(Context context, uint inodeNum, Inode inode)
        {
            Context = context;
            InodeNumber = inodeNum;
            Inode = inode;
        }

        protected Context Context { get; }

        internal Inode Inode { get; }

        internal uint InodeNumber { get; }

        public DateTime LastAccessTimeUtc
        {
            get { return ((long)Inode.AccessTime).FromUnixTimeSeconds().DateTime; }

            set { throw new NotImplementedException(); }
        }

        public DateTime LastWriteTimeUtc
        {
            get { return ((long) Inode.ModificationTime).FromUnixTimeSeconds().DateTime; }

            set { throw new NotImplementedException(); }
        }

        public DateTime CreationTimeUtc
        {
            get { return ((long) Inode.CreationTime).FromUnixTimeSeconds().DateTime; }

            set { throw new NotImplementedException(); }
        }

        public FileAttributes FileAttributes
        {
            get { return FromMode(Inode.Mode); }

            set { throw new NotImplementedException(); }
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
                    _content = Inode.GetContentBuffer(Context);
                }

                return _content;
            }
        }

        private static FileAttributes FromMode(uint mode)
        {
            return Utilities.FileAttributesFromUnixFileType((UnixFileType)((mode >> 12) & 0xF));
        }
    }
}
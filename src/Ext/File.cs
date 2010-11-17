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

namespace DiscUtils.Ext
{
    using System;
    using System.IO;
    using DiscUtils.Vfs;

    internal class File : IVfsFile
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1);

        private Context _context;
        private uint _inodeNumber;
        private Inode _inode;
        private FileBuffer _content;

        public File(Context context, uint inode)
        {
            _context = context;
            _inodeNumber = inode;
            _inode = _context.GetInode(_inodeNumber);
        }

        public DateTime LastAccessTimeUtc
        {
            get
            {
                return FromFileTime(_inode.AccessTime);
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
                return FromFileTime(_inode.ModificationTime);
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
                return FromFileTime(_inode.CreationTime);
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
                    _content = new FileBuffer(_context, _inode);
                }

                return _content;
            }
        }

        internal Inode Inode
        {
            get { return _inode; }
        }

        protected Context Context
        {
            get { return _context; }
        }

        private static DateTime FromFileTime(uint fileTime)
        {
            long ticks = fileTime * (long)10 * 1000 * 1000;
            return new DateTime(ticks + Epoch.Ticks);
        }

        private static FileAttributes FromMode(uint mode)
        {
            UnixFileType fileType = (UnixFileType)((mode >> 12) & 0xF);
            switch (fileType)
            {
                case UnixFileType.Fifo:
                    return FileAttributes.Device | FileAttributes.System;
                case UnixFileType.Character:
                    return FileAttributes.Device | FileAttributes.System;
                case UnixFileType.Directory:
                    return FileAttributes.Directory;
                case UnixFileType.Block:
                    return FileAttributes.Device | FileAttributes.System;
                case UnixFileType.Regular:
                    return FileAttributes.Normal;
                case UnixFileType.Link:
                    return FileAttributes.ReparsePoint;
                case UnixFileType.Socket:
                    return FileAttributes.Device | FileAttributes.System;
                default:
                    return (FileAttributes)0;
            }
        }
    }
}

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

    internal class DirEntry : VfsDirEntry
    {
        private DirectoryRecord _record;

        public DirEntry(DirectoryRecord record)
        {
            _record = record;
        }

        public override bool IsDirectory
        {
            get { return _record.FileType == DirectoryRecord.FileTypeDirectory; }
        }

        public override bool IsSymlink
        {
            get { return _record.FileType == DirectoryRecord.FileTypeSymlink; }
        }

        public override string FileName
        {
            get { return _record.Name; }
        }

        public override bool HasVfsTimeInfo
        {
            get { return false; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override DateTime CreationTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override bool HasVfsFileAttributes
        {
            get { return false; }
        }

        public override FileAttributes FileAttributes
        {
            get { throw new NotSupportedException(); }
        }

        public override long UniqueCacheId
        {
            get { return _record.Inode; }
        }

        public DirectoryRecord Record
        {
            get { return _record; }
        }

        public override string ToString()
        {
            return _record.Name ?? "(no name)";
        }
    }
}

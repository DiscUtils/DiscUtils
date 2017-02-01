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
using DiscUtils.Vfs;

namespace DiscUtils.SquashFs
{
    internal class DirectoryEntry : VfsDirEntry
    {
        private readonly DirectoryHeader _header;
        private readonly DirectoryRecord _record;

        public DirectoryEntry(DirectoryHeader header, DirectoryRecord record)
        {
            _header = header;
            _record = record;
        }

        public override DateTime CreationTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override FileAttributes FileAttributes
        {
            get
            {
                UnixFileType fileType = VfsSquashFileSystemReader.FileTypeFromInodeType(_record.Type);
                return Utilities.FileAttributesFromUnixFileType(fileType);
            }
        }

        public override string FileName
        {
            get { return _record.Name; }
        }

        public override bool HasVfsFileAttributes
        {
            get { return true; }
        }

        public override bool HasVfsTimeInfo
        {
            get { return false; }
        }

        public MetadataRef InodeReference
        {
            get { return new MetadataRef(_header.StartBlock, _record.Offset); }
        }

        public override bool IsDirectory
        {
            get { return _record.Type == InodeType.Directory || _record.Type == InodeType.ExtendedDirectory; }
        }

        public override bool IsSymlink
        {
            get { return _record.Type == InodeType.Symlink || _record.Type == InodeType.ExtendedSymlink; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { throw new NotSupportedException(); }
        }

        public override long UniqueCacheId
        {
            get { return _header.InodeNumber + _record.InodeNumber; }
        }
    }
}
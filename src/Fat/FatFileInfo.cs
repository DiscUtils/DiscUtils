//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils.Fat
{
    internal class FatFileInfo : DiscFileInfo
    {
        private FatDirectoryInfo _parent;
        private DirectoryEntry _dirEntry;
        private FatFileSystem _fileSystem;

        internal FatFileInfo(FatFileSystem fileSystem, FatDirectoryInfo parent, DirectoryEntry dirEntry)
        {
            _fileSystem = fileSystem;
            _parent = parent;
            _dirEntry = dirEntry;
        }

        public override long Length
        {
            get { return _dirEntry.FileSize; }
        }

        public override Stream Open(FileMode mode)
        {
            return _fileSystem.OpenExistingStream(mode, _dirEntry.FirstCluster, (uint)_dirEntry.FileSize);
        }

        public override Stream Open(FileMode mode, FileAccess access)
        {
            throw new NotImplementedException();
        }

        public override string Name
        {
            get { return _dirEntry.Name; }
        }

        public override string FullName
        {
            get { return Parent.FullName + "\\" + Name; }
        }

        public override FileAttributes Attributes
        {
            get {
                // Conveniently, .NET filesystem attributes have identical values to
                // FAT filesystem attributes...
                return (FileAttributes)_dirEntry.Attributes;
            }
        }

        public override DiscDirectoryInfo Parent
        {
            get { return _parent; }
        }

        public override DateTime CreationTime
        {
            get { return _dirEntry.CreationTime; }
        }

        public override DateTime CreationTimeUtc
        {
            get { return _dirEntry.CreationTime.ToUniversalTime(); }
        }

        public override DateTime LastAccessTime
        {
            get { return _dirEntry.LastAccessTime; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return _dirEntry.LastAccessTime.ToUniversalTime(); }
        }

        public override DateTime LastWriteTime
        {
            get { return _dirEntry.LastWriteTime; }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return _dirEntry.LastWriteTime.ToUniversalTime(); }
        }
    }
}

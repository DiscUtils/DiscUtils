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
        private FatFileSystem _fileSystem;
        private string _path;

        public FatFileInfo(FatFileSystem fileSystem, string path)
        {
            if (string.IsNullOrEmpty(path) || path.EndsWith("\\"))
            {
                throw new ArgumentException("Invalid file path", "path");
            }

            _fileSystem = fileSystem;
            _path = path.TrimStart('\\');
        }

        public override long Length
        {
            get { return GetDirEntry().FileSize; }
        }

        public override Stream Open(FileMode mode)
        {
            return _fileSystem.Open(_path, mode);
        }

        public override Stream Open(FileMode mode, FileAccess access)
        {
            return _fileSystem.Open(_path, mode, access);
        }

        public override string Name
        {
            get
            {
                int sepIdx = _path.LastIndexOf('\\');
                if (sepIdx < 0)
                {
                    return _path;
                }
                else
                {
                    return _path.Substring(sepIdx);
                }
            }
        }

        public override string FullName
        {
            get { return _path; }
        }

        public override FileAttributes Attributes
        {
            get {
                // Conveniently, .NET filesystem attributes have identical values to
                // FAT filesystem attributes...
                return (FileAttributes)GetDirEntry().Attributes;
            }
        }

        public override DiscDirectoryInfo Parent
        {
            get
            {
                int sepIdx = _path.LastIndexOf('\\');
                if (sepIdx < 0)
                {
                    return new FatDirectoryInfo(_fileSystem, "");
                }
                else
                {
                    return new FatDirectoryInfo(_fileSystem, _path.Substring(0, sepIdx));
                }
            }
        }

        public override bool Exists
        {
            get
            {
                DirectoryEntry dirEntry = _fileSystem.GetDirectoryEntry(_path);
                return (dirEntry != null && (dirEntry.Attributes & FatAttributes.Directory) == 0);
            }
        }

        public override DateTime CreationTime
        {
            get { return CreationTimeUtc.ToLocalTime(); }
        }

        public override DateTime CreationTimeUtc
        {
            get { return _fileSystem.ConvertToUtc(GetDirEntry().CreationTime); }
        }

        public override DateTime LastAccessTime
        {
            get { return LastAccessTimeUtc.ToLocalTime(); }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return _fileSystem.ConvertToUtc(GetDirEntry().LastAccessTime); }
        }

        public override DateTime LastWriteTime
        {
            get { return LastWriteTimeUtc.ToLocalTime(); }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return _fileSystem.ConvertToUtc(GetDirEntry().LastWriteTime); }
        }

        private DirectoryEntry GetDirEntry()
        {
            DirectoryEntry dirEntry = _fileSystem.GetDirectoryEntry(_path);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", _path);
            }
            return dirEntry;
        }
    }
}

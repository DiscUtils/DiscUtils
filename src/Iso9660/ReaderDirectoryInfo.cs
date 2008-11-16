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

namespace DiscUtils.Iso9660
{
    internal class ReaderDirectoryInfo : DiscDirectoryInfo
    {
        private CDReader _reader;
        private string _path;

        public ReaderDirectoryInfo(CDReader reader, string path)
        {
            _reader = reader;
            _path = path.Trim('\\');
        }

        public override string Name
        {
            get { return Utilities.GetFileFromPath(_path); }
        }

        public override string FullName
        {
            get { return _path + '\\'; }
        }

        public override FileAttributes Attributes
        {
            get { return _reader.GetAttributes(_path); }
            set { throw new NotSupportedException(); }
        }

        public override DiscDirectoryInfo Parent
        {
            get
            {
                if (string.IsNullOrEmpty(_path))
                {
                    return null;
                }
                else
                {
                    return new ReaderDirectoryInfo(_reader, Utilities.GetDirectoryFromPath(_path));
                }
            }
        }

        public override bool Exists
        {
            get { return _reader.DirectoryExists(_path); }
        }

        public override DateTime CreationTime
        {
            get { return _reader.GetCreationTime(_path); }
            set { throw new NotSupportedException(); }
        }

        public override DateTime CreationTimeUtc
        {
            get { return _reader.GetCreationTimeUtc(_path); }
            set { throw new NotSupportedException(); }
        }

        public override DateTime LastAccessTime
        {
            get { return _reader.GetLastAccessTime(_path); }
            set { throw new NotSupportedException(); }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return _reader.GetLastAccessTimeUtc(_path); }
            set { throw new NotSupportedException(); }
        }

        public override DateTime LastWriteTime
        {
            get { return _reader.GetLastWriteTime(_path); }
            set { throw new NotSupportedException(); }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return _reader.GetLastWriteTimeUtc(_path); }
            set { throw new NotSupportedException(); }
        }

        public override void Create()
        {
            throw new NotSupportedException();
        }

        public override void MoveTo(string destDirName)
        {
            throw new NotSupportedException();
        }

        public override DiscDirectoryInfo[] GetDirectories()
        {
            return Utilities.Map<string, DiscDirectoryInfo>(_reader.GetDirectories(_path), ConvertPathToDiscDirectoryInfo);
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern, SearchOption option)
        {
            return Utilities.Map<string, DiscDirectoryInfo>(_reader.GetDirectories(_path, pattern, option), ConvertPathToDiscDirectoryInfo);
        }

        public override DiscFileInfo[] GetFiles()
        {
            return Utilities.Map<string, DiscFileInfo>(_reader.GetFiles(_path), ConvertPathToDiscFileInfo);
        }

        public override DiscFileInfo[] GetFiles(string pattern, SearchOption option)
        {
            ReaderDirectory dir = _reader.GetDirectory(_path);
            return Utilities.Map<string, DiscFileInfo>(dir.SearchFiles(pattern, option == SearchOption.AllDirectories), ConvertPathToDiscFileInfo);
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos()
        {
            ReaderDirectory dir = _reader.GetDirectory(_path);
            return Utilities.Map<string, DiscFileSystemInfo>(_reader.GetFileSystemEntries(_path), ConvertPathToDiscFileSystemInfo);
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos(string pattern)
        {
            ReaderDirectory dir = _reader.GetDirectory(_path);
            return Utilities.Map<string, DiscFileSystemInfo>(dir.SearchFileInfos(pattern, false), ConvertPathToDiscFileSystemInfo);
        }

        public override void Delete()
        {
            throw new NotSupportedException();
        }

        public override void Delete(bool recursive)
        {
            throw new NotSupportedException();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            ReaderDirectoryInfo other = (ReaderDirectoryInfo)obj;

            return _reader == other._reader && _path == other._path;
        }

        public override int GetHashCode()
        {
            return _reader.GetHashCode() ^ _path.GetHashCode();
        }

        private DiscDirectoryInfo ConvertPathToDiscDirectoryInfo(string path)
        {
            return new ReaderDirectoryInfo(_reader, path);
        }

        private DiscFileInfo ConvertPathToDiscFileInfo(string path)
        {
            return new ReaderFileInfo(_reader, path);
        }

        private DiscFileSystemInfo ConvertPathToDiscFileSystemInfo(string path)
        {
            if (path.EndsWith("\\", StringComparison.Ordinal))
            {
                return new ReaderDirectoryInfo(_reader, path);
            }
            else
            {
                return new ReaderFileInfo(_reader, path);
            }
        }
    }
}

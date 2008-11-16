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
using System.Globalization;
using System.IO;

namespace DiscUtils.Fat
{
    internal class FatDirectoryInfo : DiscDirectoryInfo
    {
        private FatFileSystem _fileSystem;
        private string _path;

        public FatDirectoryInfo(FatFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            _path = path.Trim('\\');
        }

        public override void Create()
        {
            _fileSystem.CreateDirectory(_path);
        }

        public override void Delete()
        {
            _fileSystem.DeleteDirectory(_path);
        }

        public override void Delete(bool recursive)
        {
            _fileSystem.DeleteDirectory(_path, recursive);
        }

        public override void MoveTo(string destinationDirName)
        {
            _fileSystem.MoveDirectory(_path, destinationDirName);
        }

        public override DiscDirectoryInfo[] GetDirectories()
        {
            return Utilities.Map<string,DiscDirectoryInfo>(_fileSystem.GetDirectories(_path), ConvertPathToDiscDirectoryInfo);
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern, SearchOption option)
        {
            return Utilities.Map<string, DiscDirectoryInfo>(_fileSystem.GetDirectories(_path, pattern, option), ConvertPathToDiscDirectoryInfo);
        }

        public override DiscFileInfo[] GetFiles()
        {
            return Utilities.Map<string, DiscFileInfo>(_fileSystem.GetFiles(_path), ConvertPathToDiscFileInfo);
        }

        public override DiscFileInfo[] GetFiles(string pattern, SearchOption option)
        {
            return Utilities.Map<string, DiscFileInfo>(_fileSystem.GetFiles(_path, pattern, option), ConvertPathToDiscFileInfo);
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos()
        {
            return Utilities.Map<string, DiscFileSystemInfo>(_fileSystem.GetFileSystemEntries(_path), ConvertPathToDiscFileSystemInfo);
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos(string pattern)
        {
            return Utilities.Map<string, DiscFileSystemInfo>(_fileSystem.GetFileSystemEntries(_path, pattern), ConvertPathToDiscFileSystemInfo);
        }

        public override FileAttributes Attributes
        {
            get { return _fileSystem.GetAttributes(_path); }
            set { _fileSystem.SetAttributes(_path, value); }
        }

        public override bool Exists
        {
            get { return _fileSystem.DirectoryExists(_path); }
        }

        public override DateTime CreationTime
        {
            get { return _fileSystem.GetCreationTime(_path); }
            set { _fileSystem.SetCreationTime(_path, value); }
        }

        public override DateTime CreationTimeUtc
        {
            get { return _fileSystem.GetCreationTimeUtc(_path); }
            set { _fileSystem.SetCreationTimeUtc(_path, value); }
        }

        public override DateTime LastAccessTime
        {
            get { return _fileSystem.GetLastAccessTime(_path); }
            set { _fileSystem.SetLastAccessTime(_path, value); }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return _fileSystem.GetLastAccessTimeUtc(_path); }
            set { _fileSystem.SetLastAccessTimeUtc(_path, value); }
        }

        public override DateTime LastWriteTime
        {
            get { return _fileSystem.GetLastWriteTime(_path); }
            set { _fileSystem.SetLastWriteTime(_path, value); }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return _fileSystem.GetLastWriteTimeUtc(_path); }
            set { _fileSystem.SetLastWriteTimeUtc(_path, value); }
        }

        public override string Name
        {
            get { return Utilities.GetFileFromPath(_path); }
        }

        public override string FullName
        {
            get { return _path + "\\"; }
        }

        public override DiscDirectoryInfo Parent
        {
            get
            {
                if (String.IsNullOrEmpty(_path))
                {
                    return null;
                }
                else
                {
                    return new FatDirectoryInfo(_fileSystem, Utilities.GetDirectoryFromPath(_path));
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            FatDirectoryInfo other = (FatDirectoryInfo)obj;

            return _fileSystem == other._fileSystem && _path == other._path;
        }

        public override int GetHashCode()
        {
            return _fileSystem.GetHashCode() ^ _path.GetHashCode();
        }

        #region Support Functions
        private DiscDirectoryInfo ConvertPathToDiscDirectoryInfo(string path)
        {
            return new FatDirectoryInfo(_fileSystem, path);
        }

        private DiscFileInfo ConvertPathToDiscFileInfo(string path)
        {
            return new FatFileInfo(_fileSystem, path);
        }

        private DiscFileSystemInfo ConvertPathToDiscFileSystemInfo(string path)
        {
            return new FatFileSystemInfo(_fileSystem, path);
        }
        #endregion
    }
}

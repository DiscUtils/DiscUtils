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
    internal class FatFileSystemInfo : DiscFileSystemInfo
    {
        private FatFileSystem _fileSystem;
        private string _path;

        public FatFileSystemInfo(FatFileSystem fileSystem, string path)
        {
            if (string.IsNullOrEmpty(path) || path.EndsWith("\\", StringComparison.Ordinal))
            {
                throw new ArgumentException("Invalid file path", "path");
            }

            _fileSystem = fileSystem;
            _path = path.TrimStart('\\');
        }

        public override string Name
        {
            get { return Utilities.GetFileFromPath(_path); }
        }

        public override string FullName
        {
            get { return _path; }
        }

        public override FileAttributes Attributes
        {
            get
            {
                // Conveniently, .NET filesystem attributes have identical values to
                // FAT filesystem attributes...
                return (FileAttributes)GetDirEntry().Attributes;
            }

            set
            {
                UpdateDirEntry((e) => { e.Attributes = (FatAttributes)value; });
            }
        }

        public override DiscDirectoryInfo Parent
        {
            get { return new FatDirectoryInfo(_fileSystem, Utilities.GetDirectoryFromPath(_path)); }
        }

        public override bool Exists
        {
            get
            {
                return _fileSystem.GetDirectoryEntry(_path) != null;
            }
        }

        public override DateTime CreationTime
        {
            get { return CreationTimeUtc.ToLocalTime(); }
            set { CreationTimeUtc = value.ToUniversalTime(); }
        }

        public override DateTime CreationTimeUtc
        {
            get { return _fileSystem.ConvertToUtc(GetDirEntry().CreationTime); }
            set { UpdateDirEntry((e) => { e.CreationTime = _fileSystem.ConvertFromUtc(value); }); }
        }

        public override DateTime LastAccessTime
        {
            get { return LastAccessTimeUtc.ToLocalTime(); }
            set { LastAccessTimeUtc = value.ToUniversalTime(); }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return _fileSystem.ConvertToUtc(GetDirEntry().LastAccessTime); }
            set { UpdateDirEntry((e) => { e.LastAccessTime = _fileSystem.ConvertFromUtc(value); }); }
        }

        public override DateTime LastWriteTime
        {
            get { return LastWriteTimeUtc.ToLocalTime(); }
            set { LastWriteTimeUtc = value.ToUniversalTime(); }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return _fileSystem.ConvertToUtc(GetDirEntry().LastWriteTime); }
            set { UpdateDirEntry((e) => { e.LastWriteTime = _fileSystem.ConvertFromUtc(value); }); }
        }

        public override void Delete()
        {
            if ((Attributes & FileAttributes.Directory) != 0)
            {
                _fileSystem.DeleteDirectory(_path);
            }
            else
            {
                _fileSystem.DeleteFile(_path);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            FatFileSystemInfo other = (FatFileSystemInfo)obj;

            return _fileSystem == other._fileSystem && _path == other._path;
        }

        public override int GetHashCode()
        {
            return _fileSystem.GetHashCode() ^ _path.GetHashCode();
        }

        private DirectoryEntry GetDirEntry()
        {
            Directory parent;
            return GetDirEntry(out parent);
        }

        private DirectoryEntry GetDirEntry(out Directory parent)
        {
            long id = _fileSystem.GetDirectoryEntry(_path, out parent);
            if (id < 0)
            {
                throw new FileNotFoundException("File not found", _path);
            }
            return parent.GetEntry(id);
        }

        private delegate void EntryUpdateAction(DirectoryEntry entry);

        private void UpdateDirEntry(EntryUpdateAction action)
        {
            Directory parent;
            long id = _fileSystem.GetDirectoryEntry(_path, out parent);
            if (parent != null && id >= 0)
            {
                DirectoryEntry entry = parent.GetEntry(id);
                action(entry);
                parent.UpdateEntry(id, entry);
            }
        }
    }
}

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
using System.Collections.Generic;
using System.IO;

namespace DiscUtils.Fat
{
    internal class FatDirectoryInfo : DiscDirectoryInfo
    {
        private FatFileSystem _fileSystem;
        private FatDirectoryInfo _parent;
        private DirectoryEntry _dirEntry;
        private List<DirectoryEntry> _entries;

        internal FatDirectoryInfo(FatFileSystem fileSystem, FatDirectoryInfo parent, DirectoryEntry dirEntry, Stream dirStream)
        {
            _fileSystem = fileSystem;
            _parent = parent;
            _dirEntry = dirEntry;

            _entries = new List<DirectoryEntry>();
            while (dirStream.Position < dirStream.Length)
            {
                DirectoryEntry entry = new DirectoryEntry(dirStream);

                // Long File Name entry
                if (entry.Attributes == (FatAttributes.ReadOnly | FatAttributes.Hidden | FatAttributes.System | FatAttributes.VolumeId))
                {
                    continue;
                }

                // E5 = Free Entry
                if (entry.Name[0] == 0xE5)
                {
                    continue;
                }

                // 00 = Free Entry, no more entries available
                if (entry.Name[0] == 0x00)
                {
                    break;
                }

                _entries.Add(entry);
            }
        }

        public override DiscDirectoryInfo[] GetDirectories()
        {
            throw new NotImplementedException();
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern)
        {
            throw new NotImplementedException();
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern, SearchOption option)
        {
            throw new NotImplementedException();
        }

        public override DiscFileInfo[] GetFiles()
        {
            List<FatFileInfo> files = new List<FatFileInfo>(_entries.Count);
            foreach (DirectoryEntry dirEntry in _entries)
            {
                if ((dirEntry.Attributes & FatAttributes.Directory) == 0)
                {
                    files.Add(new FatFileInfo(_fileSystem, this, dirEntry));
                }
            }
            return files.ToArray();
        }

        public override DiscFileInfo[] GetFiles(string pattern)
        {
            throw new NotImplementedException();
        }

        public override DiscFileInfo[] GetFiles(string pattern, SearchOption option)
        {
            throw new NotImplementedException();
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos()
        {
            throw new NotImplementedException();
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos(string pattern)
        {
            throw new NotImplementedException();
        }

        public override string Name
        {
            get { return (_dirEntry == null) ? "" : _dirEntry.Name; }
        }

        public override string FullName
        {
            get { return ((_parent == null) ? "" : _parent.FullName) + "\\" + Name; }
        }

        public override FileAttributes Attributes
        {
            get
            {
                // Conveniently, .NET filesystem attributes have identical values to
                // FAT filesystem attributes...
                if (_dirEntry == null)
                {
                    return FileAttributes.Directory;
                }
                else
                {
                    return (FileAttributes)_dirEntry.Attributes;
                }
            }
        }

        public override DiscDirectoryInfo Parent
        {
            get { return _parent; }
        }

        public override DateTime CreationTime
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _dirEntry.CreationTime; }
        }

        public override DateTime CreationTimeUtc
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _dirEntry.CreationTime.ToUniversalTime(); }
        }

        public override DateTime LastAccessTime
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _dirEntry.LastAccessTime; }
        }

        public override DateTime LastAccessTimeUtc
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _dirEntry.LastAccessTime.ToUniversalTime(); }
        }

        public override DateTime LastWriteTime
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _dirEntry.LastWriteTime; }
        }

        public override DateTime LastWriteTimeUtc
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _dirEntry.LastWriteTime.ToUniversalTime(); }
        }

        internal bool TryGetDirectoryEntry(string name, out DirectoryEntry entry)
        {
            foreach (DirectoryEntry de in _entries)
            {
                if (de.NormalizedName == name)
                {
                    entry = de;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}

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
using System.Text.RegularExpressions;

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
            Directory self = GetDirectory();
            if (!self.IsEmpty)
            {
                throw new IOException("Unable to delete non-empty directory");
            }

            Directory parent;
            DirectoryEntry selfEntry = GetDirEntry(out parent);
            if (parent == null)
            {
                throw new IOException("Unable to delete root directory");
            }
            else
            {
                parent.DeleteEntry(selfEntry);
            }
        }

        public override void Delete(bool recursive)
        {
            if (recursive)
            {
                foreach (DiscDirectoryInfo di in GetDirectories())
                {
                    di.Delete(true);
                }

                foreach (DiscFileInfo fi in GetFiles())
                {
                    fi.Delete();
                }
            }

            Delete();
        }

        public override DiscDirectoryInfo[] GetDirectories()
        {
            Directory dir = _fileSystem.GetDirectory(_path);
            DirectoryEntry[] entries = dir.GetDirectories();
            List<FatDirectoryInfo> dirs = new List<FatDirectoryInfo>(entries.Length);
            foreach (DirectoryEntry dirEntry in entries)
            {
                dirs.Add(new FatDirectoryInfo(_fileSystem, FullName + dirEntry.Name));
            }
            return dirs.ToArray();
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern)
        {
            return GetDirectories(pattern, SearchOption.TopDirectoryOnly);
        }

        public override DiscDirectoryInfo[] GetDirectories(string pattern, SearchOption option)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(pattern);

            List<DiscDirectoryInfo> dirs = new List<DiscDirectoryInfo>();
            DoSearch(dirs, FullName, re, option == SearchOption.AllDirectories);
            return dirs.ToArray();
        }

        public override DiscFileInfo[] GetFiles()
        {
            Directory dir = _fileSystem.GetDirectory(_path);
            DirectoryEntry[] entries = dir.GetFiles();

            List<FatFileInfo> files = new List<FatFileInfo>(entries.Length);
            foreach (DirectoryEntry dirEntry in entries)
            {
                files.Add(new FatFileInfo(_fileSystem, FullName + dirEntry.Name));
            }

            return files.ToArray();
        }

        public override DiscFileInfo[] GetFiles(string pattern)
        {
            return GetFiles(pattern, SearchOption.TopDirectoryOnly);
        }

        public override DiscFileInfo[] GetFiles(string pattern, SearchOption option)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(pattern);

            List<DiscFileInfo> results = new List<DiscFileInfo>();
            DoSearch(results, FullName, re, option == SearchOption.AllDirectories);
            return results.ToArray();
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos()
        {
            Directory dir = _fileSystem.GetDirectory(_path);
            DirectoryEntry[] entries = dir.Entries;

            List<DiscFileSystemInfo> result = new List<DiscFileSystemInfo>(entries.Length);
            foreach (DirectoryEntry dirEntry in entries)
            {
                if ((dirEntry.Attributes & FatAttributes.Directory) == 0)
                {
                    result.Add(new FatFileInfo(_fileSystem, FullName + dirEntry.Name));
                }
                else
                {
                    result.Add(new FatDirectoryInfo(_fileSystem, FullName + dirEntry.Name));
                }
            }
            return result.ToArray();
        }

        public override DiscFileSystemInfo[] GetFileSystemInfos(string pattern)
        {
            Regex re = Utilities.ConvertWildcardsToRegEx(pattern);

            Directory dir = _fileSystem.GetDirectory(_path);
            DirectoryEntry[] entries = dir.Entries;

            List<DiscFileSystemInfo> result = new List<DiscFileSystemInfo>(entries.Length);
            foreach (DirectoryEntry dirEntry in entries)
            {
                if (re.IsMatch(dirEntry.SearchName))
                {
                    if ((dirEntry.Attributes & FatAttributes.Directory) == 0)
                    {
                        result.Add(new FatFileInfo(_fileSystem, FullName + dirEntry.Name));
                    }
                    else
                    {
                        result.Add(new FatDirectoryInfo(_fileSystem, FullName + dirEntry.Name));
                    }
                }
            }
            return result.ToArray();
        }

        public override string Name
        {
            get {
                int sepIdx = _path.LastIndexOf('\\');
                if (sepIdx < 0)
                {
                    return _path;
                }
                else
                {
                    return _path.Substring(sepIdx + 1);
                }
            }
        }

        public override string FullName
        {
            get { return _path + "\\"; }
        }

        public override FileAttributes Attributes
        {
            get { return (FileAttributes)GetDirEntry().Attributes;}
            set { UpdateDirEntry((e) => { e.Attributes = (FatAttributes)value; }); }
        }

        public override DiscDirectoryInfo Parent
        {
            get
            {
                if (_path == "")
                {
                    return null;
                }
                else
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
        }

        public override bool Exists
        {
            get
            {
                DirectoryEntry dirEntry = _fileSystem.GetDirectoryEntry(_path);
                return (dirEntry != null && (dirEntry.Attributes & FatAttributes.Directory) != 0);
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

        private void DoSearch(List<DiscFileInfo> results, string path, Regex regex, bool subFolders)
        {
            Directory dir = _fileSystem.GetDirectory(path);
            DirectoryEntry[] entries = subFolders ? dir.Entries : dir.GetFiles();

            foreach (DirectoryEntry de in entries)
            {
                if ((de.Attributes & FatAttributes.Directory) == 0)
                {
                    if (regex.IsMatch(de.SearchName))
                    {
                        results.Add(new FatFileInfo(_fileSystem, path + de.Name));
                    }
                }
                else if(subFolders)
                {
                    DoSearch(results, path + de.Name + "\\", regex, true);
                }
            }
        }

        private void DoSearch(List<DiscDirectoryInfo> results, string path, Regex regex, bool subFolders)
        {
            Directory dir = _fileSystem.GetDirectory(path);
            foreach (DirectoryEntry de in dir.GetDirectories())
            {
                if (regex.IsMatch(de.SearchName))
                {
                    results.Add(new FatDirectoryInfo(_fileSystem, path + de.Name));
                }

                if (subFolders)
                {
                    DoSearch(results, path + de.Name + "\\", regex, true);
                }
            }
        }

        private Directory GetDirectory()
        {
            Directory dir = _fileSystem.GetDirectory(_path);
            if (dir != null)
            {
                return dir;
            }
            else
            {
                throw new DirectoryNotFoundException(String.Format("No such directory: {0}", _path));
            }
        }

        private DirectoryEntry GetDirEntry()
        {
            Directory parent;
            return GetDirEntry(out parent);
        }

        private DirectoryEntry GetDirEntry(out Directory parent)
        {
            DirectoryEntry dirEntry = _fileSystem.GetDirectoryEntry(_path, out parent);
            if (dirEntry == null)
            {
                throw new FileNotFoundException("File not found", _path);
            }
            return dirEntry;
        }

        private delegate void EntryUpdateAction(DirectoryEntry entry);

        private void UpdateDirEntry(EntryUpdateAction action)
        {
            Directory parent;
            DirectoryEntry entry = GetDirEntry(out parent);
            action(entry);
            parent.UpdateEntry(entry);
        }
    }
}

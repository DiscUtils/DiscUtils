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
    internal class Directory
    {
        private FatFileSystem _fileSystem;
        private Directory _parent;
        private DirectoryEntry _dirEntry;
        private Stream _dirStream;

        private List<DirectoryEntry> _entries;
        private Dictionary<int,long> _entryStreamPos;

        internal Directory(FatFileSystem fileSystem, Directory parent, DirectoryEntry dirEntry)
        {
            _fileSystem = fileSystem;
            _parent = parent;
            _dirEntry = dirEntry;
            _dirStream = _fileSystem.OpenExistingStream(FileMode.Open, FileAccess.Read, dirEntry.FirstCluster, uint.MaxValue);

            LoadEntries();
        }

        internal Directory(FatFileSystem fileSystem, Stream dirStream)
        {
            _fileSystem = fileSystem;
            _parent = null;
            _dirEntry = null;
            _dirStream = dirStream;

            LoadEntries();
        }

        public FatFileSystem FileSystem
        {
            get { return _fileSystem; }
        }

        public DirectoryEntry[] GetDirectories()
        {
            List<DirectoryEntry> dirs = new List<DirectoryEntry>(_entries.Count);
            foreach (DirectoryEntry dirEntry in _entries)
            {
                if ((dirEntry.Attributes & FatAttributes.Directory) != 0)
                {
                    dirs.Add(dirEntry);
                }
            }
            return dirs.ToArray();
        }

        public DirectoryEntry[] GetFiles()
        {
            List<DirectoryEntry> files = new List<DirectoryEntry>(_entries.Count);
            foreach (DirectoryEntry dirEntry in _entries)
            {
                if ((dirEntry.Attributes & FatAttributes.Directory) == 0)
                {
                    files.Add(dirEntry);
                }
            }
            return files.ToArray();
        }

        public DirectoryEntry[] Entries
        {
            get { return _entries.ToArray(); }
        }

        public DirectoryEntry GetEntry(string name)
        {
            int idx = FindEntryByNormalizedName(name);
            if (idx < 0)
            {
                return null;
            }
            else
            {
                return _entries[idx];
            }
        }

        public Directory GetChildDirectory(string name)
        {
            int idx = FindEntryByNormalizedName(name);
            if (idx < 0)
            {
                return null;
            }
            else
            {
                return _fileSystem.GetDirectory(_entries[idx], this);
            }
        }

        public DirectoryEntry Self
        {
            get { return _dirEntry; }
        }

        public Directory Parent
        {
            get { return _parent; }
        }

        public DateTime CreationTimeUtc
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _fileSystem.ConvertToUtc(_dirEntry.CreationTime); }
        }

        public DateTime LastAccessTimeUtc
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _fileSystem.ConvertToUtc(_dirEntry.LastAccessTime); }
        }

        public DateTime LastWriteTimeUtc
        {
            get { return (_dirEntry == null) ? FatFileSystem.Epoch : _fileSystem.ConvertToUtc(_dirEntry.LastWriteTime); }
        }

        public void UpdateEntry(DirectoryEntry entry)
        {
            int idx = FindEntryByNormalizedName(entry.NormalizedName);

            if (idx < 0)
            {
                throw new IOException("Couldn't find entry to update");
            }

            _dirStream.Position = _entryStreamPos[idx];
            entry.WriteTo(_dirStream);
            _entries[idx] = entry;
        }

        private void LoadEntries()
        {
            _entryStreamPos = new Dictionary<int, long>();
            _entries = new List<DirectoryEntry>();

            while (_dirStream.Position < _dirStream.Length)
            {
                long streamPos = _dirStream.Position;
                DirectoryEntry entry = new DirectoryEntry(_dirStream);

                // Long File Name entry
                if (entry.Attributes == (FatAttributes.ReadOnly | FatAttributes.Hidden | FatAttributes.System | FatAttributes.VolumeId))
                {
                    continue;
                }

                // E5 = Free Entry
                if (entry.NormalizedName[0] == 0xE5)
                {
                    continue;
                }

                // Special folders
                if (entry.NormalizedName[0] == '.')
                {
                    continue;
                }

                // 00 = Free Entry, no more entries available
                if (entry.NormalizedName[0] == 0x00)
                {
                    break;
                }

                _entryStreamPos[_entryStreamPos.Count] = streamPos;
                _entries.Add(entry);
            }
        }

        public int FindEntryByNormalizedName(string name)
        {
            for (int i = 0; i < _entries.Count; ++i)
            {
                DirectoryEntry focus = _entries[i];
                if (focus.NormalizedName == name)
                {
                    return i;
                }
            }

            return -1;
        }

        internal Stream OpenFile(string name, FileMode mode, FileAccess fileAccess)
        {
            if (mode != FileMode.Open)
            {
                throw new NotImplementedException();
            }

            return _fileSystem.OpenFile(this, name, fileAccess);
        }
    }
}

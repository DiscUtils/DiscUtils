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
        private List<long> _freeEntries;
        private long _endOfEntries;

        internal Directory(FatFileSystem fileSystem, Directory parent, DirectoryEntry dirEntry)
        {
            _fileSystem = fileSystem;
            _parent = parent;
            _dirEntry = dirEntry;
            _dirStream = _fileSystem.OpenExistingStream(FileMode.Open, FileAccess.ReadWrite, dirEntry.FirstCluster, uint.MaxValue);

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
            else if ((_entries[idx].Attributes & FatAttributes.Directory) == 0)
            {
                return null;
            }
            else
            {
                return _fileSystem.GetDirectory(_entries[idx], this);
            }
        }

        internal Directory CreateChildDirectory(string normalizedName)
        {
            int idx = FindEntryByNormalizedName(normalizedName);
            if (idx >= 0)
            {
                if ((_entries[idx].Attributes & FatAttributes.Directory) == 0)
                {
                    throw new IOException("A file exists with the same name");
                }
                else
                {
                    return _fileSystem.GetDirectory(_entries[idx], this);
                }
            }
            else
            {
                try
                {
                    // Get a free cluster
                    uint freeCluster;
                    if (!_fileSystem.FAT.TryGetFreeCluster(out freeCluster))
                    {
                        throw new IOException("Out of disk space");
                    }
                    _fileSystem.FAT.SetEndOfChain(freeCluster);

                    DirectoryEntry newEntry = new DirectoryEntry(normalizedName, FatAttributes.Directory);
                    newEntry.FirstCluster = freeCluster;
                    newEntry.CreationTime = _fileSystem.ConvertFromUtc(DateTime.UtcNow);
                    newEntry.LastWriteTime = newEntry.CreationTime;

                    AddEntry(newEntry);

                    PopulateNewChildDirectory(newEntry);

                    // Rather than just creating a new instance, pull it through the fileSystem cache
                    // to ensure the cache model is preserved.
                    return _fileSystem.GetDirectory(newEntry, this);
                }
                finally
                {
                    _fileSystem.FAT.Flush();
                }
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

        private void LoadEntries()
        {
            _entryStreamPos = new Dictionary<int, long>();
            _entries = new List<DirectoryEntry>();
            _freeEntries = new List<long>();

            while (_dirStream.Position < _dirStream.Length)
            {
                long streamPos = _dirStream.Position;
                DirectoryEntry entry = new DirectoryEntry(_dirStream);

                if (entry.Attributes == (FatAttributes.ReadOnly | FatAttributes.Hidden | FatAttributes.System | FatAttributes.VolumeId))
                {
                    // Long File Name entry
                }
                else if (entry.NormalizedName[0] == 0xE5)
                {
                    // E5 = Free Entry
                    _freeEntries.Add(streamPos);
                }
                else if (entry.NormalizedName[0] == '.')
                {
                    // Special folders
                }
                else if (entry.NormalizedName[0] == 0x00)
                {
                    // 00 = Free Entry, no more entries available
                    _endOfEntries = streamPos;
                    break;
                }
                else
                {
                    _entryStreamPos[_entryStreamPos.Count] = streamPos;
                    _entries.Add(entry);
                }
            }
        }

        internal int FindEntryByNormalizedName(string name)
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

        private void AddEntry(DirectoryEntry newEntry)
        {
            // Unlink an entry from the free list (or add to the end of the existing directory)
            long pos;
            if (_freeEntries.Count > 0)
            {
                pos = _freeEntries[0];
                _freeEntries.RemoveAt(0);
            }
            else
            {
                pos = _endOfEntries;
                _endOfEntries += 32;
            }

            // Put the new entry into it's slot
            _dirStream.Position = pos;
            newEntry.WriteTo(_dirStream);

            // Update internal structures to reflect new entry (as if read from disk)
            _entryStreamPos[_entryStreamPos.Count] = pos;
            _entries.Add(newEntry);
        }

        internal void UpdateEntry(DirectoryEntry entry)
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

        private void PopulateNewChildDirectory(DirectoryEntry newEntry)
        {
            // Populate new directory with initial (special) entries.  First one is easy, just change the name!
            using (Stream stream = _fileSystem.OpenExistingStream(FileMode.Open, FileAccess.ReadWrite, newEntry.FirstCluster, uint.MaxValue))
            {
                newEntry.NormalizedName = ".          ";
                newEntry.WriteTo(stream);

                // Second one is a clone of ours, or if we're the root, then mostly empty...
                DirectoryEntry newParent = new DirectoryEntry("..         ", FatAttributes.Directory);
                if (_dirEntry != null)
                {
                    newParent.Attributes = _dirEntry.Attributes;
                    newParent.CreationTime = _dirEntry.CreationTime;
                    newParent.FileSize = _dirEntry.FileSize;
                    newParent.FirstCluster = _dirEntry.FirstCluster;
                    newParent.LastAccessTime = _dirEntry.LastAccessTime;
                    newParent.LastWriteTime = _dirEntry.LastWriteTime;
                }
                newParent.WriteTo(stream);
            }
        }

    }
}

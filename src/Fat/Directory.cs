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
        private long _parentId;
        private Stream _dirStream;

        private Dictionary<long,DirectoryEntry> _entries;
        private List<long> _freeEntries;
        private long _endOfEntries;

        internal Directory(Directory parent, long parentId)
        {
            _fileSystem = parent._fileSystem;
            _parent = parent;
            _parentId = parentId;

            DirectoryEntry dirEntry = _parent.GetEntry(parentId);
            _dirStream = _fileSystem.OpenExistingStream(dirEntry.FirstCluster, uint.MaxValue);

            LoadEntries();
        }

        /// <summary>
        /// Loads the root directory of a file system
        /// </summary>
        /// <param name="fileSystem">The file system</param>
        /// <param name="dirStream">The stream containing the directory info</param>
        internal Directory(FatFileSystem fileSystem, Stream dirStream)
        {
            _fileSystem = fileSystem;
            _dirStream = dirStream;

            LoadEntries();
        }

        public FatFileSystem FileSystem
        {
            get { return _fileSystem; }
        }

        public bool IsEmpty
        {
            get { return _entries.Count == 0; }
        }

        public DirectoryEntry[] GetDirectories()
        {
            List<DirectoryEntry> dirs = new List<DirectoryEntry>(_entries.Count);
            foreach (DirectoryEntry dirEntry in _entries.Values)
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
            foreach (DirectoryEntry dirEntry in _entries.Values)
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
            get { return new List<DirectoryEntry>(_entries.Values).ToArray(); }
        }

        public DirectoryEntry GetEntry(long id)
        {
            return (id < 0) ? null : _entries[id];
        }


        public Directory GetChildDirectory(string name)
        {
            long id = FindEntryByNormalizedName(name);
            if (id < 0)
            {
                return null;
            }
            else if ((_entries[id].Attributes & FatAttributes.Directory) == 0)
            {
                return null;
            }
            else
            {
                return _fileSystem.GetDirectory(this, id);
            }
        }

        internal Directory CreateChildDirectory(string normalizedName)
        {
            long id = FindEntryByNormalizedName(normalizedName);
            if (id >= 0)
            {
                if ((_entries[id].Attributes & FatAttributes.Directory) == 0)
                {
                    throw new IOException("A file exists with the same name");
                }
                else
                {
                    return _fileSystem.GetDirectory(this, id);
                }
            }
            else
            {
                try
                {
                    DirectoryEntry newEntry = new DirectoryEntry(normalizedName, FatAttributes.Directory);
                    newEntry.FirstCluster = 0; // i.e. Zero-length
                    newEntry.CreationTime = _fileSystem.ConvertFromUtc(DateTime.UtcNow);
                    newEntry.LastWriteTime = newEntry.CreationTime;

                    id = AddEntry(newEntry);

                    PopulateNewChildDirectory(id, newEntry);

                    // Rather than just creating a new instance, pull it through the fileSystem cache
                    // to ensure the cache model is preserved.
                    return _fileSystem.GetDirectory(this, id);
                }
                finally
                {
                    _fileSystem.FAT.Flush();
                }
            }
        }

        internal void AttachChildDirectory(string normalizedName, DirectoryEntry directoryEntry)
        {
            long id = FindEntryByNormalizedName(normalizedName);
            if (id >= 0)
            {
                throw new IOException("Directory entry already exists");
            }

            DirectoryEntry newEntry = new DirectoryEntry(directoryEntry);
            newEntry.NormalizedName = normalizedName;
            AddEntry(newEntry);
        }

        private void LoadEntries()
        {
            _entries = new Dictionary<long,DirectoryEntry>();
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
                    _entries.Add(streamPos, entry);
                }
            }
        }

        internal long FindEntryByNormalizedName(string name)
        {
            foreach(long id in _entries.Keys)
            {
                DirectoryEntry focus = _entries[id];
                if (focus.NormalizedName == name)
                {
                    return id;
                }
            }

            return -1;
        }

        internal Stream OpenFile(string name, FileMode mode, FileAccess fileAccess)
        {
            if (mode == FileMode.Append || mode == FileMode.Truncate)
            {
                throw new NotImplementedException();
            }

            bool exists = FindEntryByNormalizedName(name) != -1;

            if (mode == FileMode.CreateNew && exists)
            {
                throw new IOException("File already exists");
            }
            else if (mode == FileMode.Open && !exists)
            {
                throw new FileNotFoundException("File not found", name);
            }
            else if ((mode == FileMode.Open || mode == FileMode.OpenOrCreate || mode == FileMode.Create) && exists)
            {
                Stream stream = _fileSystem.OpenExistingFile(this, name, fileAccess);
                if (mode == FileMode.Create)
                {
                    stream.SetLength(0);
                }

                HandleAccessed(false);

                return stream;
            }
            else if ((mode == FileMode.OpenOrCreate || mode == FileMode.CreateNew || mode == FileMode.Create) && !exists)
            {
                // Create new file
                DirectoryEntry newEntry = new DirectoryEntry(name, FatAttributes.Archive);
                newEntry.FirstCluster = 0; // i.e. Zero-length
                newEntry.CreationTime = _fileSystem.ConvertFromUtc(DateTime.UtcNow);
                newEntry.LastWriteTime = newEntry.CreationTime;

                AddEntry(newEntry);

                return _fileSystem.OpenExistingFile(this, name, fileAccess);
            }
            else
            {
                // Should never get here...
                throw new NotImplementedException();
            }
        }

        private long AddEntry(DirectoryEntry newEntry)
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
            _entries.Add(pos, newEntry);

            HandleAccessed(true);

            return pos;
        }

        internal void DeleteEntry(long id)
        {
            if (id < 0)
            {
                throw new IOException("Attempt to delete unknown directory entry");
            }

            try
            {
                DirectoryEntry entry = _entries[id];

                DirectoryEntry copy = new DirectoryEntry(entry);
                copy.NormalizedName = "\xE5" + entry.NormalizedName.Substring(1);
                _dirStream.Position = id;
                copy.WriteTo(_dirStream);

                _fileSystem.FAT.FreeChain(entry.FirstCluster);

                if ((entry.Attributes & FatAttributes.Directory) != 0)
                {
                    _fileSystem.ForgetDirectory(entry);
                }

                _entries.Remove(id);
                _freeEntries.Add(id);

                HandleAccessed(true);
            }
            finally
            {
                _fileSystem.FAT.Flush();
            }
        }

        internal void UpdateEntry(long id, DirectoryEntry entry)
        {
            if (id < 0)
            {
                throw new IOException("Attempt to update unknown directory entry");
            }

            _dirStream.Position = id;
            entry.WriteTo(_dirStream);
            _entries[id] = entry;
        }

        private void HandleAccessed(bool forWrite)
        {
            if (_parent != null && _parentId >= 0)
            {
                DateTime now = DateTime.Now;
                DirectoryEntry entry = _parent.GetEntry(_parentId);

                DateTime oldAccessTime = entry.LastAccessTime;
                DateTime oldWriteTime = entry.LastWriteTime;

                entry.LastAccessTime = now;
                if( forWrite)
                {
                    entry.LastWriteTime = now;
                }

                if (entry.LastAccessTime != oldAccessTime || entry.LastWriteTime != oldWriteTime)
                {
                    _parent.UpdateEntry(_parentId, entry);
                }
            }
        }

        private void PopulateNewChildDirectory(long newEntryId, DirectoryEntry newEntry)
        {
            // Populate new directory with initial (special) entries.  First one is easy, just change the name!
            using (ClusterStream stream = _fileSystem.OpenExistingStream(newEntry.FirstCluster, uint.MaxValue))
            {
                // Update the entry for the child when the first cluster is actually allocated
                stream.FirstClusterAllocated += (cluster) => {
                    newEntry.FirstCluster = cluster;
                    UpdateEntry(newEntryId, newEntry);
                };

                DirectoryEntry selfEntry = new DirectoryEntry(newEntry);
                selfEntry.NormalizedName = ".          ";
                selfEntry.WriteTo(stream);

                // Second one is a clone of ours, or if we're the root, then mostly empty...
                if (_parent != null)
                {
                    DirectoryEntry parentEntry = new DirectoryEntry(_parent.GetEntry(_parentId));
                    parentEntry.NormalizedName = "..         ";
                    parentEntry.WriteTo(stream);
                }
                else
                {
                    DirectoryEntry newParent = new DirectoryEntry("..         ", FatAttributes.Directory);
                    newParent.WriteTo(stream);
                }
            }
        }
    }
}

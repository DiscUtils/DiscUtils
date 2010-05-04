//
// Copyright (c) 2008-2010, Kenneth Bell
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
    internal class Directory : IDisposable
    {
        private const string SelfEntryNormalizedName = ".          ";
        private const string ParentEntryNormalizedName = "..         ";

        private FatFileSystem _fileSystem;
        private Directory _parent;
        private long _parentId;
        private Stream _dirStream;

        private Dictionary<long,DirectoryEntry> _entries;
        private List<long> _freeEntries;
        private long _endOfEntries;

        private DirectoryEntry _selfEntry;
        private long _selfEntryLocation;
        private DirectoryEntry _parentEntry;
        private long _parentEntryLocation;

        /// <summary>
        /// Creates an instance of Directory representing a non-root directory.
        /// </summary>
        /// <param name="parent">The parent directory</param>
        /// <param name="parentId">The identity of the entry representing this directory in the parent</param>
        internal Directory(Directory parent, long parentId)
        {
            _fileSystem = parent._fileSystem;
            _parent = parent;
            _parentId = parentId;

            DirectoryEntry dirEntry = ParentsChildEntry;
            _dirStream = new ClusterStream(_fileSystem, FileAccess.ReadWrite, dirEntry.FirstCluster, uint.MaxValue);

            LoadEntries();
        }

        /// <summary>
        /// Creates and instance representing the root directory of a file system.
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
                if ((dirEntry.Attributes & (FatAttributes.Directory | FatAttributes.VolumeId)) == 0)
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
                    uint firstCluster;
                    if( !_fileSystem.Fat.TryGetFreeCluster(out firstCluster))
                    {
                        throw new IOException("Failed to allocate first cluster for new directory");
                    }
                    _fileSystem.Fat.SetEndOfChain(firstCluster);

                    DirectoryEntry newEntry = new DirectoryEntry(normalizedName, FatAttributes.Directory);
                    newEntry.FirstCluster = firstCluster;
                    newEntry.CreationTime = _fileSystem.ConvertFromUtc(DateTime.UtcNow);
                    newEntry.LastWriteTime = newEntry.CreationTime;

                    id = AddEntry(newEntry);

                    PopulateNewChildDirectory(newEntry);

                    // Rather than just creating a new instance, pull it through the fileSystem cache
                    // to ensure the cache model is preserved.
                    return _fileSystem.GetDirectory(this, id);
                }
                finally
                {
                    _fileSystem.Fat.Flush();
                }
            }
        }

        internal void AttachChildDirectory(string normalizedName, Directory newChild)
        {
            long id = FindEntryByNormalizedName(normalizedName);
            if (id >= 0)
            {
                throw new IOException("Directory entry already exists");
            }

            DirectoryEntry newEntry = new DirectoryEntry(newChild.ParentsChildEntry);
            newEntry.NormalizedName = normalizedName;
            AddEntry(newEntry);

            DirectoryEntry newParentEntry = new DirectoryEntry(SelfEntry);
            newParentEntry.NormalizedName = ParentEntryNormalizedName;
            newChild.ParentEntry = newParentEntry;
        }

        #region Convenient accessors for special entries
        internal DirectoryEntry ParentsChildEntry
        {
            get
            {
                if (_parent == null)
                {
                    return new DirectoryEntry(ParentEntryNormalizedName, FatAttributes.Directory);
                }
                else
                {
                    return _parent.GetEntry(_parentId);
                }
            }

            set
            {
                if (_parent != null)
                {
                    _parent.UpdateEntry(_parentId, value);
                }
            }
        }

        internal DirectoryEntry SelfEntry
        {
            get
            {
                if (_parent == null)
                {
                    // If we're the root directory, simulate the parent entry with a dummy record
                    return new DirectoryEntry("", FatAttributes.Directory);
                }
                else
                {
                    return _selfEntry;
                }
            }

            set
            {
                if (_selfEntryLocation >= 0)
                {
                    _dirStream.Position = _selfEntryLocation;
                    value.WriteTo(_dirStream);
                    _selfEntry = value;
                }
            }
        }

        internal DirectoryEntry ParentEntry
        {
            get
            {
                return _parentEntry;
            }

            set
            {
                if (_parentEntryLocation < 0)
                {
                    throw new IOException("No parent entry on disk to update");
                }
                _dirStream.Position = _parentEntryLocation;
                value.WriteTo(_dirStream);
                _parentEntry = value;
            }
        }
        #endregion

        private void LoadEntries()
        {
            _entries = new Dictionary<long,DirectoryEntry>();
            _freeEntries = new List<long>();

            _selfEntryLocation = -1;
            _parentEntryLocation = -1;

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
                    if (entry.NormalizedName == SelfEntryNormalizedName)
                    {
                        _selfEntry = entry;
                        _selfEntryLocation = streamPos;
                    }
                    else if (entry.NormalizedName == ParentEntryNormalizedName)
                    {
                        _parentEntry = entry;
                        _parentEntryLocation = streamPos;
                    }
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

        internal long FindVolumeId()
        {
            foreach (long id in _entries.Keys)
            {
                DirectoryEntry focus = _entries[id];
                if ((focus.Attributes & FatAttributes.VolumeId) != 0)
                {
                    return id;
                }
            }

            return -1;
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

            long fileId = FindEntryByNormalizedName(name);
            bool exists = fileId != -1;

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
                Stream stream = new FatFileStream(_fileSystem, this, fileId, fileAccess);
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

                fileId = AddEntry(newEntry);

                return new FatFileStream(_fileSystem, this, fileId, fileAccess);
            }
            else
            {
                // Should never get here...
                throw new NotImplementedException();
            }
        }

        internal long AddEntry(DirectoryEntry newEntry)
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

        internal void DeleteEntry(long id, bool releaseContents)
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

                if (releaseContents)
                {
                    _fileSystem.Fat.FreeChain(entry.FirstCluster);
                }

                _entries.Remove(id);
                _freeEntries.Add(id);

                HandleAccessed(true);
            }
            finally
            {
                _fileSystem.Fat.Flush();
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
            if (_fileSystem.CanWrite && _parent != null)
            {
                DateTime now = DateTime.Now;
                DirectoryEntry entry = SelfEntry;

                DateTime oldAccessTime = entry.LastAccessTime;
                DateTime oldWriteTime = entry.LastWriteTime;

                entry.LastAccessTime = now;
                if (forWrite)
                {
                    entry.LastWriteTime = now;
                }

                if (entry.LastAccessTime != oldAccessTime || entry.LastWriteTime != oldWriteTime)
                {
                    SelfEntry = entry;

                    DirectoryEntry parentEntry = ParentsChildEntry;
                    parentEntry.LastAccessTime = entry.LastAccessTime;
                    parentEntry.LastWriteTime = entry.LastWriteTime;
                    ParentsChildEntry = parentEntry;
                }
            }
        }

        private void PopulateNewChildDirectory(DirectoryEntry newEntry)
        {
            // Populate new directory with initial (special) entries.  First one is easy, just change the name!
            using (ClusterStream stream = new ClusterStream(_fileSystem, FileAccess.Write, newEntry.FirstCluster, uint.MaxValue))
            {
                // First is the self-referencing entry...
                DirectoryEntry selfEntry = new DirectoryEntry(newEntry);
                selfEntry.NormalizedName = SelfEntryNormalizedName;
                selfEntry.WriteTo(stream);

                // Second is a clone of our self entry (i.e. parent) - though dates are odd...
                DirectoryEntry parentEntry = new DirectoryEntry(SelfEntry);
                parentEntry.NormalizedName = ParentEntryNormalizedName;
                parentEntry.CreationTime = newEntry.CreationTime;
                parentEntry.LastWriteTime = newEntry.LastWriteTime;
                parentEntry.WriteTo(stream);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dirStream.Dispose();
            }
        }

        #endregion
    }
}

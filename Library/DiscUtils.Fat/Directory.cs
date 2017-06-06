//
// Copyright (c) 2008-2011, Kenneth Bell
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
using DiscUtils.Streams;

namespace DiscUtils.Fat
{
    internal class Directory : IDisposable
    {
        private readonly Stream _dirStream;
        private readonly Directory _parent;
        private readonly long _parentId;
        private long _endOfEntries;

        private Dictionary<long, DirectoryEntry> _entries;
        private List<long> _freeEntries;
        private DirectoryEntry _parentEntry;
        private long _parentEntryLocation;

        private DirectoryEntry _selfEntry;
        private long _selfEntryLocation;

        /// <summary>
        /// Initializes a new instance of the Directory class.  Use this constructor to represent non-root directories.
        /// </summary>
        /// <param name="parent">The parent directory.</param>
        /// <param name="parentId">The identity of the entry representing this directory in the parent.</param>
        internal Directory(Directory parent, long parentId)
        {
            FileSystem = parent.FileSystem;
            _parent = parent;
            _parentId = parentId;

            DirectoryEntry dirEntry = ParentsChildEntry;
            _dirStream = new ClusterStream(FileSystem, FileAccess.ReadWrite, dirEntry.FirstCluster, uint.MaxValue);

            LoadEntries();
        }

        /// <summary>
        /// Initializes a new instance of the Directory class.  Use this constructor to represent the root directory.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="dirStream">The stream containing the directory info.</param>
        internal Directory(FatFileSystem fileSystem, Stream dirStream)
        {
            FileSystem = fileSystem;
            _dirStream = dirStream;

            LoadEntries();
        }

        public DirectoryEntry[] Entries
        {
            get { return new List<DirectoryEntry>(_entries.Values).ToArray(); }
        }

        public FatFileSystem FileSystem { get; }

        public bool IsEmpty
        {
            get { return _entries.Count == 0; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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

        public DirectoryEntry GetEntry(long id)
        {
            return id < 0 ? null : _entries[id];
        }

        public Directory GetChildDirectory(FileName name)
        {
            long id = FindEntry(name);
            if (id < 0)
            {
                return null;
            }
            if ((_entries[id].Attributes & FatAttributes.Directory) == 0)
            {
                return null;
            }
            return FileSystem.GetDirectory(this, id);
        }

        internal Directory CreateChildDirectory(FileName name)
        {
            long id = FindEntry(name);
            if (id >= 0)
            {
                if ((_entries[id].Attributes & FatAttributes.Directory) == 0)
                {
                    throw new IOException("A file exists with the same name");
                }
                return FileSystem.GetDirectory(this, id);
            }
            try
            {
                uint firstCluster;
                if (!FileSystem.Fat.TryGetFreeCluster(out firstCluster))
                {
                    throw new IOException("Failed to allocate first cluster for new directory");
                }

                FileSystem.Fat.SetEndOfChain(firstCluster);

                DirectoryEntry newEntry = new DirectoryEntry(FileSystem.FatOptions, name, FatAttributes.Directory,
                    FileSystem.FatVariant);
                newEntry.FirstCluster = firstCluster;
                newEntry.CreationTime = FileSystem.ConvertFromUtc(DateTime.UtcNow);
                newEntry.LastWriteTime = newEntry.CreationTime;

                id = AddEntry(newEntry);

                PopulateNewChildDirectory(newEntry);

                // Rather than just creating a new instance, pull it through the fileSystem cache
                // to ensure the cache model is preserved.
                return FileSystem.GetDirectory(this, id);
            }
            finally
            {
                FileSystem.Fat.Flush();
            }
        }

        internal void AttachChildDirectory(FileName name, Directory newChild)
        {
            long id = FindEntry(name);
            if (id >= 0)
            {
                throw new IOException("Directory entry already exists");
            }

            DirectoryEntry newEntry = new DirectoryEntry(newChild.ParentsChildEntry);
            newEntry.Name = name;
            AddEntry(newEntry);

            DirectoryEntry newParentEntry = new DirectoryEntry(SelfEntry);
            newParentEntry.Name = FileName.ParentEntryName;
            newChild.ParentEntry = newParentEntry;
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

        internal long FindEntry(FileName name)
        {
            foreach (long id in _entries.Keys)
            {
                DirectoryEntry focus = _entries[id];
                if (focus.Name == name && (focus.Attributes & FatAttributes.VolumeId) == 0)
                {
                    return id;
                }
            }

            return -1;
        }

        internal SparseStream OpenFile(FileName name, FileMode mode, FileAccess fileAccess)
        {
            if (mode == FileMode.Append || mode == FileMode.Truncate)
            {
                throw new NotImplementedException();
            }

            long fileId = FindEntry(name);
            bool exists = fileId != -1;

            if (mode == FileMode.CreateNew && exists)
            {
                throw new IOException("File already exists");
            }
            if (mode == FileMode.Open && !exists)
            {
                throw new FileNotFoundException("File not found",
                    name.GetDisplayName(FileSystem.FatOptions.FileNameEncoding));
            }
            if ((mode == FileMode.Open || mode == FileMode.OpenOrCreate || mode == FileMode.Create) && exists)
            {
                SparseStream stream = new FatFileStream(FileSystem, this, fileId, fileAccess);
                if (mode == FileMode.Create)
                {
                    stream.SetLength(0);
                }

                HandleAccessed(false);

                return stream;
            }
            if ((mode == FileMode.OpenOrCreate || mode == FileMode.CreateNew || mode == FileMode.Create) && !exists)
            {
                // Create new file
                DirectoryEntry newEntry = new DirectoryEntry(FileSystem.FatOptions, name, FatAttributes.Archive,
                    FileSystem.FatVariant);
                newEntry.FirstCluster = 0; // i.e. Zero-length
                newEntry.CreationTime = FileSystem.ConvertFromUtc(DateTime.UtcNow);
                newEntry.LastWriteTime = newEntry.CreationTime;

                fileId = AddEntry(newEntry);

                return new FatFileStream(FileSystem, this, fileId, fileAccess);
            }

            // Should never get here...
            throw new NotImplementedException();
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
                copy.Name = entry.Name.Deleted();
                _dirStream.Position = id;
                copy.WriteTo(_dirStream);

                if (releaseContents)
                {
                    FileSystem.Fat.FreeChain(entry.FirstCluster);
                }

                _entries.Remove(id);
                _freeEntries.Add(id);

                HandleAccessed(true);
            }
            finally
            {
                FileSystem.Fat.Flush();
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

        private void LoadEntries()
        {
            _entries = new Dictionary<long, DirectoryEntry>();
            _freeEntries = new List<long>();

            _selfEntryLocation = -1;
            _parentEntryLocation = -1;

            while (_dirStream.Position < _dirStream.Length)
            {
                long streamPos = _dirStream.Position;
                DirectoryEntry entry = new DirectoryEntry(FileSystem.FatOptions, _dirStream, FileSystem.FatVariant);

                if (entry.Attributes ==
                    (FatAttributes.ReadOnly | FatAttributes.Hidden | FatAttributes.System | FatAttributes.VolumeId))
                {
                    // Long File Name entry
                }
                else if (entry.Name.IsDeleted())
                {
                    // E5 = Free Entry
                    _freeEntries.Add(streamPos);
                }
                else if (entry.Name == FileName.SelfEntryName)
                {
                    _selfEntry = entry;
                    _selfEntryLocation = streamPos;
                }
                else if (entry.Name == FileName.ParentEntryName)
                {
                    _parentEntry = entry;
                    _parentEntryLocation = streamPos;
                }
                else if (entry.Name.IsEndMarker())
                {
                    // Free Entry, no more entries available
                    _endOfEntries = streamPos;
                    break;
                }
                else
                {
                    _entries.Add(streamPos, entry);
                }
            }
        }

        private void HandleAccessed(bool forWrite)
        {
            if (FileSystem.CanWrite && _parent != null)
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
            using (
                ClusterStream stream = new ClusterStream(FileSystem, FileAccess.Write, newEntry.FirstCluster,
                    uint.MaxValue))
            {
                // First is the self-referencing entry...
                DirectoryEntry selfEntry = new DirectoryEntry(newEntry);
                selfEntry.Name = FileName.SelfEntryName;
                selfEntry.WriteTo(stream);

                // Second is a clone of our self entry (i.e. parent) - though dates are odd...
                DirectoryEntry parentEntry = new DirectoryEntry(SelfEntry);
                parentEntry.Name = FileName.ParentEntryName;
                parentEntry.CreationTime = newEntry.CreationTime;
                parentEntry.LastWriteTime = newEntry.LastWriteTime;
                parentEntry.WriteTo(stream);
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dirStream.Dispose();
            }
        }

        #region Convenient accessors for special entries

        internal DirectoryEntry ParentsChildEntry
        {
            get
            {
                if (_parent == null)
                {
                    return new DirectoryEntry(FileSystem.FatOptions, FileName.ParentEntryName, FatAttributes.Directory,
                        FileSystem.FatVariant);
                }
                return _parent.GetEntry(_parentId);
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
                    return new DirectoryEntry(FileSystem.FatOptions, FileName.Null, FatAttributes.Directory,
                        FileSystem.FatVariant);
                }
                return _selfEntry;
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
            get { return _parentEntry; }

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
    }
}
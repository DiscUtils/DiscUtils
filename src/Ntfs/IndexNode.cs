//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Ntfs
{
    internal delegate void IndexNodeStore(IndexNode node);

    internal class IndexNode
    {
        protected IndexNodeStore _store;
        protected IndexHeader _header;

        private Index _index;
        private IndexNode _parent;

        private List<IndexEntry> _entries;

        public IndexNode(IndexNodeStore store, Index index, IndexNode parent, uint allocatedSize)
        {
            _store = store;
            _index = index;
            _parent = parent;
            _header = new IndexHeader(allocatedSize);

            IndexEntry endEntry = new IndexEntry(_index.IsFileIndex);
            endEntry.Flags |= IndexEntryFlags.End;

            _entries = new List<IndexEntry>();
            _entries.Add(endEntry);
        }

        public IndexNode(IndexNodeStore store, Index index, IndexNode parent, byte[] buffer, int offset)
        {
            _store = store;
            _index = index;
            _parent = parent;
            _header = new IndexHeader(buffer, offset + 0);

            _entries = new List<IndexEntry>();
            int pos = (int)_header.OffsetToFirstEntry;
            while (pos < _header.TotalSizeOfEntries)
            {
                IndexEntry entry = new IndexEntry(index.IsFileIndex);
                entry.Read(buffer, offset + pos);
                _entries.Add(entry);

                if ((entry.Flags & IndexEntryFlags.End) != 0)
                {
                    break;
                }

                pos += entry.Size;
            }
        }

        public IndexHeader Header
        {
            get { return _header; }
        }

        public List<IndexEntry> Entries
        {
            get { return _entries; }
        }

        public void InternalAddEntries(IEnumerable<IndexEntry> newEntries)
        {
            uint totalNewSize = 0;
            foreach (var newEntry in newEntries)
            {
                totalNewSize += (uint)newEntry.Size;
            }

            if (_header.AllocatedSizeOfEntries < _header.TotalSizeOfEntries + totalNewSize)
            {
                throw new ArgumentException("Too many new entries to fit into node", "newEntries");
            }

            foreach (var newEntry in newEntries)
            {
                if ((newEntry.Flags & IndexEntryFlags.End) != 0)
                {
                    if ((newEntry.Flags & IndexEntryFlags.Node) != 0)
                    {
                        throw new IOException("Trying to add 'end' node with children during internal processing - fault");
                    }
                    else
                    {
                        // Skip this one...
                        continue;
                    }
                }

                for (int i = 0; i < _entries.Count; ++i)
                {
                    var focus = _entries[i];
                    int compVal;

                    if ((focus.Flags & IndexEntryFlags.End) != 0)
                    {
                        // No value when End flag is set.  Logically these nodes always
                        // compare 'bigger', so if there are children we'll visit them.
                        compVal = -1;
                    }
                    else
                    {
                        compVal = _index.Compare(newEntry.KeyBuffer, focus.KeyBuffer);
                    }

                    if (compVal == 0)
                    {
                        throw new InvalidOperationException("Entry already exists");
                    }
                    else if (compVal < 0)
                    {
                        _entries.Insert(i, newEntry);
                        break;
                    }
                }
            }
            _store(this);
        }

        public void AddEntry(byte[] key, byte[] data)
        {
            for (int i = 0; i < _entries.Count; ++i)
            {
                var focus = _entries[i];
                int compVal;

                if ((focus.Flags & IndexEntryFlags.End) != 0)
                {
                    // No value when End flag is set.  Logically these nodes always
                    // compare 'bigger', so if there are children we'll visit them.
                    compVal = -1;
                }
                else
                {
                    compVal = _index.Compare(key, focus.KeyBuffer);
                }

                if (compVal == 0)
                {
                    throw new InvalidOperationException("Entry already exists");
                }
                else if (compVal < 0)
                {
                    if ((focus.Flags & IndexEntryFlags.Node) != 0)
                    {
                        _index.GetSubBlock(this, focus).Node.AddEntry(key, data);
                    }
                    else
                    {
                        // Insert before this entry
                        IndexEntry newEntry = new IndexEntry(key, data, _index.IsFileIndex);
                        if (_header.AllocatedSizeOfEntries < _header.TotalSizeOfEntries + newEntry.Size)
                        {
                            if(_parent != null)
                            {
                                throw new NotImplementedException("Splitting a node");
                            }

                            IndexEntry newRootEntry = new IndexEntry(_index.IsFileIndex);
                            newRootEntry.Flags = IndexEntryFlags.End;

                            IndexBlock newBlock = _index.AllocateBlock(this, newRootEntry, _entries);
                            _entries.Clear();
                            _entries.Add(newRootEntry);
                            _store(this);
                            newBlock.Node.AddEntry(key, data);
                        }
                        else
                        {
                            _entries.Insert(i, newEntry);
                            _store(this);
                        }
                        break;
                    }
                }
            }
        }

        public void UpdateEntry(byte[] key, byte[] data)
        {
            for (int i = 0; i < _entries.Count; ++i)
            {
                var focus = _entries[i];
                int compVal = _index.Compare(key, focus.KeyBuffer);
                if (compVal == 0)
                {
                    IndexEntry newEntry = new IndexEntry(focus, key, data);
                    if (_entries[i].Size != newEntry.Size)
                    {
                        throw new NotImplementedException("Changing index entry sizes");
                    }
                    _entries[i] = newEntry;
                    _store(this);
                    return;
                }
            }

            throw new IOException("No such index entry");
        }

        public bool TryFindEntry(byte[] key, out IndexEntry entry, out IndexNode node)
        {
            foreach (var focus in _entries)
            {
                if ((focus.Flags & IndexEntryFlags.End) != 0)
                {
                    if ((focus.Flags & IndexEntryFlags.Node) != 0)
                    {
                        IndexBlock subNode = _index.GetSubBlock(this, focus);
                        return subNode.Node.TryFindEntry(key, out entry, out node);
                    }
                    break;
                }
                else
                {
                    int compVal = _index.Compare(key, focus.KeyBuffer);
                    if (compVal == 0)
                    {
                        entry = focus;
                        node = this;
                        return true;
                    }
                    else if (compVal < 0 && (focus.Flags & (IndexEntryFlags.End | IndexEntryFlags.Node)) != 0)
                    {
                        IndexBlock subNode = _index.GetSubBlock(this, focus);
                        return subNode.Node.TryFindEntry(key, out entry, out node);
                    }
                }
            }

            entry = null;
            node = null;
            return false;
        }

        public virtual ushort WriteTo(byte[] buffer, int offset, ushort updateSeqSize)
        {
            bool haveSubNodes = false;
            uint totalEntriesSize = 0;
            foreach (var entry in _entries)
            {
                totalEntriesSize += (uint)entry.Size;
                haveSubNodes |= ((entry.Flags & IndexEntryFlags.Node) != 0);
            }

            _header.OffsetToFirstEntry = (uint)Utilities.RoundUp(IndexHeader.Size + updateSeqSize, 8);
            _header.TotalSizeOfEntries = totalEntriesSize + _header.OffsetToFirstEntry;
            _header.HasChildNodes = (byte)(haveSubNodes ? 1 : 0);
            _header.WriteTo(buffer, offset + 0);

            int pos = (int)_header.OffsetToFirstEntry;
            foreach (var entry in _entries)
            {
                entry.WriteTo(buffer, offset + pos);
                pos += entry.Size;
            }

            return IndexHeader.Size;
        }

        public virtual int CalcSize(ushort updateSeqSize)
        {
            int totalEntriesSize = 0;
            foreach (var entry in _entries)
            {
                totalEntriesSize += entry.Size;
            }

            int firstEntryOffset = Utilities.RoundUp(IndexHeader.Size + updateSeqSize, 8);
            return firstEntryOffset + totalEntriesSize;
        }

    }
}

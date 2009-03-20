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
    internal delegate void IndexNodeStore<K,D>(IndexNode<K,D> node)
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new();

    internal class IndexNode<K, D>
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        private IndexNodeStore<K, D> _store;
        private Index<K, D> _index;
        private IndexNode<K, D> _parent;

        private IndexHeader _header;
        private List<IndexEntry<K, D>> _entries;

        public IndexNode(IndexNodeStore<K, D> store, Index<K,D> index, IndexNode<K, D> parent, byte[] buffer, int offset)
        {
            _store = store;
            _index = index;
            _parent = parent;
            _header = new IndexHeader(buffer, offset + 0);

            _entries = new List<IndexEntry<K, D>>();
            int pos = (int)_header.OffsetToFirstEntry;
            while (pos < _header.TotalSizeOfEntries)
            {
                IndexEntry<K, D> entry = new IndexEntry<K, D>(buffer, offset + pos);
                _entries.Add(entry);

                if ((entry.Flags & IndexEntryFlags.End) != 0)
                {
                    break;
                }

                pos += entry.Size;
            }
        }

        public ushort WriteTo(byte[] buffer, int offset, ushort updateSeqSize)
        {
            uint totalEntriesSize = 0;
            foreach (var entry in _entries)
            {
                totalEntriesSize += (uint)entry.Size;
            }

            _header.OffsetToFirstEntry = (uint)Utilities.RoundUp(IndexHeader.Size + updateSeqSize, 8);
            _header.TotalSizeOfEntries = totalEntriesSize + _header.OffsetToFirstEntry;
            _header.WriteTo(buffer, offset + 0);

            int pos = (int)_header.OffsetToFirstEntry;
            foreach (var entry in _entries)
            {
                entry.WriteTo(buffer, offset + pos);
                pos += entry.Size;
            }

            return IndexHeader.Size;
        }

        public IndexHeader Header
        {
            get { return _header; }
        }

        public List<IndexEntry<K, D>> Entries
        {
            get { return _entries; }
        }

        public void AddEntry(K key, IComparer<K> comparer, D data)
        {
            for (int i = 0; i < _entries.Count; ++i)
            {
                var focus = _entries[i];
                int compVal = comparer.Compare(key, focus.Key);
                if (compVal == 0)
                {
                    throw new InvalidOperationException("Entry already exists");
                }
                else if (compVal < 0 || (focus.Flags & IndexEntryFlags.End) != 0)
                {
                    if ((focus.Flags & IndexEntryFlags.Node) != 0)
                    {
                        _index.GetSubBlock(this, focus).Node.AddEntry(key, comparer, data);
                    }
                    else
                    {
                        // Insert before this entry
                        IndexEntry<K, D> newEntry = new IndexEntry<K, D>(key, data);
                        if (_header.AllocatedSizeOfEntries < _header.TotalSizeOfEntries + newEntry.Size)
                        {
                            throw new NotImplementedException("Re-balancing index nodes");
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

        public void UpdateEntry(K key, IComparer<K> comparer, D data)
        {
            for (int i = 0; i < _entries.Count; ++i)
            {
                var focus = _entries[i];
                int compVal = comparer.Compare(key, focus.Key);
                if (compVal == 0)
                {
                    IndexEntry<K, D> newEntry = new IndexEntry<K, D>(focus, key, data);
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

        public bool TryFindEntry(K key, IComparer<K> comparer, out IndexEntry<K, D> entry, out IndexNode<K, D> node)
        {
            foreach (var focus in _entries)
            {
                if ((focus.Flags & IndexEntryFlags.End) != 0)
                {
                    if ((focus.Flags & IndexEntryFlags.Node) != 0)
                    {
                        IndexBlock<K, D> subNode = _index.GetSubBlock(this, focus);
                        return subNode.Node.TryFindEntry(key, comparer, out entry, out node);
                    }
                    break;
                }
                else
                {
                    int compVal = comparer.Compare(key, focus.Key);
                    if (compVal == 0)
                    {
                        entry = focus;
                        node = this;
                        return true;
                    }
                    else if (compVal < 0 && (focus.Flags & (IndexEntryFlags.End | IndexEntryFlags.Node)) != 0)
                    {
                        IndexBlock<K, D> subNode = _index.GetSubBlock(this, focus);
                        return subNode.Node.TryFindEntry(key, comparer, out entry, out node);
                    }
                }
            }

            entry = null;
            node = null;
            return false;
        }

    }
}

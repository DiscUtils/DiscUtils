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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiscUtils.Ntfs.Attributes;

namespace DiscUtils.Ntfs
{
    internal class Index<K,D> : IDictionary<K,D>
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        private File _file;
        private string _name;
        private BiosParameterBlock _bpb;

        private IComparer<K> _comparer;

        private List<IndexEntry<K, D>> _rootEntries;
        private Stream _indexStream;

        public Index(File file, string name, BiosParameterBlock bpb, IComparer<K> comparer)
        {
            _file = file;
            _name = name;
            _bpb = bpb;
            _comparer = comparer;

            ReadRootEntries();

            if (_file.GetAttribute(AttributeType.IndexAllocation, _name) != null)
            {
                _indexStream = _file.OpenAttribute(AttributeType.IndexAllocation, _name, FileAccess.Read);
            }
        }

        public IEnumerable<KeyValuePair<K, D>> Entries
        {
            get
            {
                return from entry in Enumerate(_rootEntries)
                       select new KeyValuePair<K, D>(entry.Key, entry.Data);
            }
        }

        public IEnumerable<KeyValuePair<K, D>> FindAll(IComparable<K> query)
        {
            return from entry in FindAllIn(query, _rootEntries)
                   select new KeyValuePair<K, D>(entry.Key, entry.Data);
        }

        public KeyValuePair<K, D> FindFirst(IComparable<K> query)
        {
            foreach (var entry in FindAll(query))
            {
                return entry;
            }

            return default(KeyValuePair<K,D>);
        }

        #region IDictionary<K,D> Members

        public D this[K key]
        {
            get
            {
                D value;
                if (TryGetValue(key, out value))
                {
                    return value;
                }
                else
                {
                    throw new KeyNotFoundException();
                }
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public ICollection<K> Keys
        {
            get
            {
                IEnumerable<K> keys = from entry in Entries
                                      select entry.Key;
                return new List<K>(keys);
            }
        }

        public ICollection<D> Values
        {
            get
            {
                IEnumerable<D> values = from entry in Entries
                                        select entry.Value;
                return new List<D>(values);
            }
        }

        public void Add(K key, D value)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(K key)
        {
            D value;
            return TryGetValue(key, out value);
        }

        public bool Remove(K key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, out D value)
        {
            //
            // TODO: This is sucky, should be using the index rather than enumerating all items!
            //

            foreach (var entry in Entries)
            {
                if (_comparer.Compare(entry.Key, key) == 0)
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = default(D);
            return false;
        }

        #endregion

        #region ICollection<KeyValuePair<K,D>> Members

        public void Add(KeyValuePair<K, D> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<K, D> item)
        {
            D value;
            return TryGetValue(item.Key, out value);
        }

        public void CopyTo(KeyValuePair<K, D>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get
            {
                int i = 0;
                foreach (var entry in Entries)
                {
                    ++i;
                }
                return i;
            }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool Remove(KeyValuePair<K, D> item)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerable<KeyValuePair<K,D>> Members

        public IEnumerator<KeyValuePair<K, D>> GetEnumerator()
        {
            foreach (var entry in Entries)
            {
                yield return entry;
            }
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        private IEnumerable<IndexEntry<K, D>> Enumerate(IEnumerable<IndexEntry<K, D>> entryList)
        {
            foreach (var focus in entryList)
            {
                if ((focus.Flags & IndexEntryFlags.Node) != 0)
                {
                    _indexStream.Position = focus.ChildrenVirtualCluster * _bpb.BytesPerSector * _bpb.SectorsPerCluster;
                    byte[] buffer = Utilities.ReadFully(_indexStream, _bpb.IndexBufferSize);
                    IndexBlock<K, D> block = new IndexBlock<K, D>(_bpb.BytesPerSector);
                    block.FromBytes(buffer, 0);
                    buffer = null;

                    foreach (var subEntry in Enumerate(block.IndexEntries))
                    {
                        yield return subEntry;
                    }
                }

                if ((focus.Flags & IndexEntryFlags.End) == 0)
                {
                    yield return focus;
                }
            }
        }

        private IEnumerable<IndexEntry<K, D>> FindAllIn(IComparable<K> query, IEnumerable<IndexEntry<K, D>> entryList)
        {
            foreach (var focus in entryList)
            {
                bool searchChildren = true;
                bool matches = false;
                bool keepIterating = true;

                if ((focus.Flags & IndexEntryFlags.End) == 0)
                {
                    int compVal = query.CompareTo(focus.Key);
                    if (compVal == 0)
                    {
                        matches = true;
                    }
                    else if (compVal > 0)
                    {
                        searchChildren = false;
                    }
                    else if (compVal < 0)
                    {
                        keepIterating = false;
                    }
                }

                if (searchChildren && (focus.Flags & IndexEntryFlags.Node) != 0)
                {
                    _indexStream.Position = focus.ChildrenVirtualCluster * _bpb.BytesPerSector * _bpb.SectorsPerCluster;
                    byte[] buffer = Utilities.ReadFully(_indexStream, _bpb.IndexBufferSize);
                    IndexBlock<K, D> block = new IndexBlock<K, D>(_bpb.BytesPerSector);
                    block.FromBytes(buffer, 0);
                    buffer = null;

                    foreach (var entry in FindAllIn(query, block.IndexEntries))
                    {
                        yield return entry;
                    }
                }

                if (matches)
                {
                    yield return focus;
                }

                if (!keepIterating)
                {
                    yield break;
                }
            }
        }


        private void ReadRootEntries()
        {
            _rootEntries = new List<IndexEntry<K, D>>();
            IndexRootAttribute indexRoot = (IndexRootAttribute)_file.GetAttribute(AttributeType.IndexRoot, _name);
            using (Stream s = indexRoot.Open(FileAccess.Read))
            {
                byte[] buffer = Utilities.ReadFully(s, (int)indexRoot.Length);

                int bytesRemaining = (int)indexRoot.Header.TotalSizeOfEntries - 0x10;
                uint pos = indexRoot.Header.OffsetToFirstEntry + 0x10;
                while (bytesRemaining > 0)
                {
                    IndexEntry<K, D> entry = new IndexEntry<K, D>(buffer, (int)pos);
                    _rootEntries.Add(entry);

                    if ((entry.Flags & IndexEntryFlags.End) != 0)
                    {
                        break;
                    }

                    pos += entry.Length;
                    bytesRemaining -= entry.Length;
                }
            }
        }

    }
}

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

        private IndexRoot _root;
        private IndexNode<K, D> _rootNode;
        private Stream _indexStream;

        public Index(File file, string name, BiosParameterBlock bpb, IComparer<K> comparer)
        {
            _file = file;
            _name = name;
            _bpb = bpb;
            _comparer = comparer;

            _root = _file.GetAttributeContent<IndexRoot>(AttributeType.IndexRoot, _name);

            using (Stream s = _file.OpenAttribute(AttributeType.IndexRoot, _name, FileAccess.Read))
            {
                byte[] buffer = Utilities.ReadFully(s, (int)s.Length);
                _rootNode = new IndexNode<K, D>(StoreRootNode, this, null, buffer, IndexRoot.HeaderOffset);
            }

            if (_file.GetAttribute(AttributeType.IndexAllocation, _name) != null)
            {
                _indexStream = _file.OpenAttribute(AttributeType.IndexAllocation, _name, FileAccess.ReadWrite);
            }
        }

        public IEnumerable<KeyValuePair<K, D>> Entries
        {
            get
            {
                return from entry in Enumerate(_rootNode)
                       select new KeyValuePair<K, D>(entry.Key, entry.Data);
            }
        }

        public IEnumerable<KeyValuePair<K, D>> FindAll(IComparable<K> query)
        {
            return from entry in FindAllIn(query, _rootNode)
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
                IndexEntry<K,D> oldEntry;
                IndexNode<K,D> node;
                if (_rootNode.TryFindEntry(key, _comparer, out oldEntry, out node))
                {
                    node.UpdateEntry(key, _comparer, value);
                }
                else
                {
                    _rootNode.AddEntry(key, _comparer, value);
                }
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

        internal Stream AllocationStream
        {
            get { return _indexStream; }
        }

        internal uint IndexBufferSize
        {
            get { return _root.IndexAllocationSize; }
        }

        internal IndexBlock<K, D> GetSubBlock(IndexNode<K, D> parentNode, IndexEntry<K, D> parentEntry)
        {
            return new IndexBlock<K, D>(this, parentNode, parentEntry, _bpb);
        }

        internal void StoreRootNode(IndexNode<K, D> node)
        {
            _rootNode = node;
            WriteRootNodeToDisk();
        }

        private bool TryFindEntry(K key, out IndexEntry<K, D> entry, out IndexNode<K, D> node)
        {
            return _rootNode.TryFindEntry(key, _comparer, out entry, out node);
        }

        private IEnumerable<IndexEntry<K, D>> Enumerate(IndexNode<K,D> node)
        {
            foreach (var focus in node.Entries)
            {
                if ((focus.Flags & IndexEntryFlags.Node) != 0)
                {
                    IndexBlock<K, D> block = GetSubBlock(node, focus);
                    foreach (var subEntry in Enumerate(block.Node))
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

        private IEnumerable<IndexEntry<K, D>> FindAllIn(IComparable<K> query, IndexNode<K, D> node)
        {
            foreach (var focus in node.Entries)
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
                    IndexBlock<K, D> block = GetSubBlock(node, focus);
                    foreach (var entry in FindAllIn(query, block.Node))
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

        private void WriteRootNodeToDisk()
        {
            byte[] buffer = new byte[_rootNode.Header.AllocatedSizeOfEntries];
            _rootNode.WriteTo(buffer, 0, 0);
            using (Stream s = _file.OpenAttribute(AttributeType.IndexRoot, _name, FileAccess.Write))
            {
                s.Position = IndexRoot.HeaderOffset;
                s.Write(buffer, 0, buffer.Length);
                s.SetLength(s.Position);
            }
        }
    }
}

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
    internal class Index : IDictionary<byte[], byte[]>
    {
        protected File _file;
        protected string _name;
        protected BiosParameterBlock _bpb;
        private UpperCase _upCase;
        private bool _isFileIndex;

        private IComparer<byte[]> _comparer;

        private IndexRoot _root;
        private IndexNode _rootNode;
        private Stream _indexStream;
        private Bitmap _indexBitmap;

        private ObjectCache<long, IndexBlock> _blockCache;

        public Index(File file, string name, BiosParameterBlock bpb, UpperCase upCase)
        {
            _file = file;
            _name = name;
            _bpb = bpb;
            _upCase = upCase;
            _isFileIndex = (name == "$I30");

            _blockCache = new ObjectCache<long, IndexBlock>();

            _root = _file.GetAttributeContent<IndexRoot>(AttributeType.IndexRoot, _name);
            _comparer = _root.GetCollator(upCase);

            using (Stream s = _file.OpenAttribute(AttributeType.IndexRoot, _name, FileAccess.Read))
            {
                byte[] buffer = Utilities.ReadFully(s, (int)s.Length);
                _rootNode = new IndexNode(WriteRootNodeToDisk, 0, this, null, buffer, IndexRoot.HeaderOffset);

                // Give the attribute some room to breathe, so long as it doesn't squeeze others out
                // BROKEN, BROKEN, BROKEN - how to figure this out?  Query at the point of adding entries to the root node?
                _rootNode.TotalSpaceAvailable += _file.MftRecordFreeSpace - 100;
            }

            if (_file.GetAttribute(AttributeType.IndexAllocation, _name) != null)
            {
                _indexStream = _file.OpenAttribute(AttributeType.IndexAllocation, _name, FileAccess.ReadWrite);
            }

            NtfsAttribute bitmapAttr = _file.GetAttribute(AttributeType.Bitmap, _name);
            if (bitmapAttr != null)
            {
                _indexBitmap = new Bitmap(_file.OpenAttribute(bitmapAttr.Id, FileAccess.ReadWrite), long.MaxValue);
            }
        }

        internal Stream AllocationStream
        {
            get { return _indexStream; }
        }

        internal uint IndexBufferSize
        {
            get { return _root.IndexAllocationSize; }
        }

        public IEnumerable<KeyValuePair<byte[],byte[]>> Entries
        {
            get
            {
                return from entry in Enumerate(_rootNode)
                       select new KeyValuePair<byte[],byte[]>(entry.KeyBuffer, entry.DataBuffer);
            }
        }

        public KeyValuePair<byte[], byte[]> FindFirst(IComparable<byte[]> query)
        {
            foreach (var entry in FindAll(query))
            {
                return entry;
            }

            return new KeyValuePair<byte[], byte[]>();
        }

        public IEnumerable<KeyValuePair<byte[],byte[]>> FindAll(IComparable<byte[]> query)
        {
            return from entry in FindAllIn(query, _rootNode)
                   select new KeyValuePair<byte[], byte[]>(entry.KeyBuffer, entry.DataBuffer);
        }

        internal bool IsFileIndex
        {
            get { return _isFileIndex; }
        }

        internal IndexBlock GetSubBlock(IndexNode parentNode, IndexEntry parentEntry)
        {
            IndexBlock block = _blockCache[parentEntry.ChildrenVirtualCluster];
            if (block == null)
            {
                block = new IndexBlock(this, parentNode, parentEntry, _bpb);
                _blockCache[parentEntry.ChildrenVirtualCluster] = block;
            }

            return block;
        }

        internal IndexBlock GetSubBlockIfCached(IndexNode parentNode, IndexEntry parentEntry)
        {
            return _blockCache[parentEntry.ChildrenVirtualCluster];
        }

        internal IndexBlock AllocateBlock(IndexNode parentNode, IndexEntry parentEntry)
        {
            if (_indexStream == null)
            {
                ushort iaId = _file.CreateAttribute(AttributeType.IndexAllocation, _name);
                _indexStream = _file.OpenAttribute(iaId, FileAccess.ReadWrite);
            }

            if (_indexBitmap == null)
            {
                ushort ibId = _file.CreateAttribute(AttributeType.Bitmap, _name);
                _indexBitmap = new Bitmap(_file.OpenAttribute(ibId, FileAccess.ReadWrite), long.MaxValue);
            }

            long idx = _indexBitmap.AllocateFirstAvailable(0);
            parentEntry.ChildrenVirtualCluster = idx * Utilities.Ceil(_bpb.IndexBufferSize, _bpb.SectorsPerCluster * _bpb.BytesPerSector);
            parentEntry.Flags |= IndexEntryFlags.Node;

            IndexBlock block = IndexBlock.Initialize(this, parentNode, parentEntry, _bpb);
            _blockCache[parentEntry.ChildrenVirtualCluster] = block;
            return block;
        }

        internal int Compare(byte[] x, byte[] y)
        {
            return _comparer.Compare(x, y);
        }

        private void WriteRootNodeToDisk()
        {
            _rootNode.Header.AllocatedSizeOfEntries = (uint)_rootNode.CalcSize();
            byte[] buffer = new byte[_rootNode.Header.AllocatedSizeOfEntries];
            _rootNode.WriteTo(buffer, 0);
            using (Stream s = _file.OpenAttribute(AttributeType.IndexRoot, _name, FileAccess.Write))
            {
                s.Position = IndexRoot.HeaderOffset;
                s.Write(buffer, 0, buffer.Length);
                s.SetLength(s.Position);
            }
        }

        protected IEnumerable<IndexEntry> Enumerate(IndexNode node)
        {
            foreach (var focus in node.Entries)
            {
                if ((focus.Flags & IndexEntryFlags.Node) != 0)
                {
                    IndexBlock block = GetSubBlock(node, focus);
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

        private IEnumerable<IndexEntry> FindAllIn(IComparable<byte[]> query, IndexNode node)
        {
            foreach (var focus in node.Entries)
            {
                bool searchChildren = true;
                bool matches = false;
                bool keepIterating = true;

                if ((focus.Flags & IndexEntryFlags.End) == 0)
                {
                    int compVal = query.CompareTo(focus.KeyBuffer);
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
                    IndexBlock block = GetSubBlock(node, focus);
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

        #region IDictionary<byte[],byte[]> Members

        public byte[] this[byte[] key]
        {
            get
            {
                byte[] value;
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
                IndexEntry oldEntry;
                IndexNode node;
                if (_rootNode.TryFindEntry(key, out oldEntry, out node))
                {
                    node.UpdateEntry(key, value);
                }
                else
                {
                    _rootNode.AddEntry(key, value);
                }
            }
        }

        public ICollection<byte[]> Keys
        {
            get
            {
                IEnumerable<byte[]> keys = from entry in Entries
                                           select entry.Key;
                return new List<byte[]>(keys);
            }
        }

        public ICollection<byte[]> Values
        {
            get
            {
                IEnumerable<byte[]> values = from entry in Entries
                                             select entry.Value;
                return new List<byte[]>(values);
            }
        }

        public void Add(byte[] key, byte[] value)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(byte[] key)
        {
            byte[] value;
            return TryGetValue(key, out value);
        }

        public bool Remove(byte[] key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(byte[] key, out byte[] value)
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

            value = default(byte[]);
            return false;
        }

        #endregion

        #region ICollection<KeyValuePair<byte[],byte[]>> Members

        public void Add(KeyValuePair<byte[], byte[]> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<byte[], byte[]> item)
        {
            byte[] value;
            return TryGetValue(item.Key, out value);
        }

        public void CopyTo(KeyValuePair<byte[], byte[]>[] array, int arrayIndex)
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

        public bool Remove(KeyValuePair<byte[], byte[]> item)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerable<KeyValuePair<byte[],byte[]>> Members

        public IEnumerator<KeyValuePair<byte[], byte[]>> GetEnumerator()
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

    }

    internal class Index<K, D> : Index
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        public Index(File file, string name, BiosParameterBlock bpb, UpperCase upCase)
            : base(file, name, bpb, upCase)
        {
        }

        public new IEnumerable<KeyValuePair<K, D>> Entries
        {
            get
            {
                foreach (var entry in base.Entries)
                {
                    yield return new KeyValuePair<K, D>(Convert<K>(entry.Key), Convert<D>(entry.Value));
                }
            }
        }

        public IEnumerable<KeyValuePair<K, D>> FindAll(IComparable<K> query)
        {
            foreach (var entry in FindAll(new ComparableConverter(query)))
            {
                yield return new KeyValuePair<K, D>(Convert<K>(entry.Key), Convert<D>(entry.Value));
            }
        }

        public KeyValuePair<K, D> FindFirst(IComparable<K> query)
        {
            foreach (var entry in FindAll(query))
            {
                return entry;
            }

            return default(KeyValuePair<K, D>);
        }

        public D this[K key]
        {
            get
            {
                return Convert<D>(base[Unconvert(key)]);
            }

            set
            {
                base[Unconvert(key)] = Unconvert<D>(value);
            }
        }

        public bool ContainsKey(K key)
        {
            return base.ContainsKey(Unconvert(key));
        }

        private static T Convert<T>(byte[] data)
            where T : IByteArraySerializable, new()
        {
            T result = new T();
            result.ReadFrom(data, 0);
            return result;
        }

        private static byte[] Unconvert<T>(T value)
            where T : IByteArraySerializable, new()
        {
            byte[] buffer = new byte[value.Size];
            value.WriteTo(buffer, 0);
            return buffer;
        }

        private class ComparableConverter : IComparable<byte[]>
        {
            private IComparable<K> _wrapped;

            public ComparableConverter(IComparable<K> toWrap)
            {
                _wrapped = toWrap;
            }

            public int CompareTo(byte[] other)
            {
                return _wrapped.CompareTo(Convert<K>(other));
            }
        }

    }
}

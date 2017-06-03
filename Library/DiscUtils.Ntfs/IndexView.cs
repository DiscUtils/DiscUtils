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
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class IndexView<K, D>
        where K : IByteArraySerializable, new()
        where D : IByteArraySerializable, new()
    {
        private readonly Index _index;

        public IndexView(Index index)
        {
            _index = index;
        }

        public int Count
        {
            get { return _index.Count; }
        }

        public IEnumerable<KeyValuePair<K, D>> Entries
        {
            get
            {
                foreach (KeyValuePair<byte[], byte[]> entry in _index.Entries)
                {
                    yield return new KeyValuePair<K, D>(Convert<K>(entry.Key), Convert<D>(entry.Value));
                }
            }
        }

        public D this[K key]
        {
            get { return Convert<D>(_index[Unconvert(key)]); }

            set { _index[Unconvert(key)] = Unconvert(value); }
        }

        public IEnumerable<KeyValuePair<K, D>> FindAll(IComparable<byte[]> query)
        {
            foreach (KeyValuePair<byte[], byte[]> entry in _index.FindAll(query))
            {
                yield return new KeyValuePair<K, D>(Convert<K>(entry.Key), Convert<D>(entry.Value));
            }
        }

        public KeyValuePair<K, D> FindFirst(IComparable<byte[]> query)
        {
            foreach (KeyValuePair<K, D> entry in FindAll(query))
            {
                return entry;
            }

            return default(KeyValuePair<K, D>);
        }

        public IEnumerable<KeyValuePair<K, D>> FindAll(IComparable<K> query)
        {
            foreach (KeyValuePair<byte[], byte[]> entry in _index.FindAll(new ComparableConverter(query)))
            {
                yield return new KeyValuePair<K, D>(Convert<K>(entry.Key), Convert<D>(entry.Value));
            }
        }

        public KeyValuePair<K, D> FindFirst(IComparable<K> query)
        {
            foreach (KeyValuePair<K, D> entry in FindAll(query))
            {
                return entry;
            }

            return default(KeyValuePair<K, D>);
        }

        public bool TryGetValue(K key, out D data)
        {
            byte[] value;
            if (_index.TryGetValue(Unconvert(key), out value))
            {
                data = Convert<D>(value);
                return true;
            }
            data = default(D);
            return false;
        }

        public bool ContainsKey(K key)
        {
            return _index.ContainsKey(Unconvert(key));
        }

        public void Remove(K key)
        {
            _index.Remove(Unconvert(key));
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
            private readonly IComparable<K> _wrapped;

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
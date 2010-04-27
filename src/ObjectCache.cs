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
using System.Collections;
using System.Collections.Generic;

namespace DiscUtils
{
    internal class ObjectCache<K,V> : IEnumerable<V>
    {
        private Dictionary<K, WeakReference> _entries;

        public ObjectCache()
        {
            _entries = new Dictionary<K, WeakReference>();
        }

        public V this[K key]
        {
            get
            {
                WeakReference wRef;
                if (_entries.TryGetValue(key, out wRef))
                {
                    return (V)wRef.Target;
                }
                return default(V);
            }
            set
            {
                _entries[key] = new WeakReference(value);
            }
        }

        internal bool ContainsKey(K key)
        {
            return _entries.ContainsKey(key);
        }

        internal void Remove(K key)
        {
            _entries.Remove(key);
        }

        #region IEnumerable<V> Members

        public IEnumerator<V> GetEnumerator()
        {
            foreach (var wRef in _entries.Values)
            {
                V value = (V)wRef.Target;
                if (value != null)
                {
                    yield return value;
                }
            }
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var wRef in _entries.Values)
            {
                V value = (V)wRef.Target;
                if (value != null)
                {
                    yield return value;
                }
            }
        }

        #endregion
    }
}

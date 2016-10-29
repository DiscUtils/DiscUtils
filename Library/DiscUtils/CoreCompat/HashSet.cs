#if NET20
using System.Collections.Generic;

namespace System.Collections.Generic
{
    public class HashSet<T> : ICollection<T>
    {
        private Dictionary<T, bool> _innerDictionary;

        public HashSet()
        {
            _innerDictionary = new Dictionary<T, bool>();
        }

        void ICollection<T>.Add(T item)
        {
            AddInternal(item);
        }

        private void AddInternal(T item)
        {
            _innerDictionary.Add(item, false);
        }

        public bool Add(T item)
        {
            if (_innerDictionary.ContainsKey(item))
                return false;

            AddInternal(item);
            return true;
        }

        public void Clear()
        {
            _innerDictionary.Clear();
            _innerDictionary = new Dictionary<T, bool>();
        }

        public bool Contains(T item)
        {
            return _innerDictionary.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _innerDictionary.Keys.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _innerDictionary.Keys.Count; }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool Remove(T item)
        {
            return _innerDictionary.Remove(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _innerDictionary.Keys.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
#endif
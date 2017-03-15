#if NET20
using System.Collections.Generic;

namespace System.Collections.Generic
{
    /// <summary>
    /// Represents a set of values.
    /// </summary>
    /// <typeparam name="T">The type of elements in the HashSet.</typeparam>
    public class HashSet<T> : ICollection<T>
    {
        private Dictionary<T, bool> _innerDictionary;

        /// <summary>
        /// Initializes a new instance of a HashSet
        /// that is empty and uses the default equality comparer for the set type.
        /// </summary>
        public HashSet()
        {
            _innerDictionary = new Dictionary<T, bool>();
        }

        /// <summary>
        /// Adds the specified element to a HashSet. 
        /// </summary>
        /// <param name="item">The element to add to the set.</param>
        void ICollection<T>.Add(T item)
        {
            AddInternal(item);
        }

        private void AddInternal(T item)
        {
            _innerDictionary.Add(item, false);
        }

        /// <summary>
        /// Adds the specified element to a HashSet. 
        /// </summary>
        /// <param name="item">The element to add to the set.</param>
        /// <returns>true if the element is added to the HashSet object;
        /// false if the element is already present.</returns>
        public bool Add(T item)
        {
            if (_innerDictionary.ContainsKey(item))
                return false;

            AddInternal(item);
            return true;
        }

        /// <summary>
        /// Removes all elements from a HashSet.
        /// </summary>
        public void Clear()
        {
            _innerDictionary.Clear();
            _innerDictionary = new Dictionary<T, bool>();
        }

        /// <summary>
        /// Determines whether a HashSet contains the specified element.
        /// </summary>
        /// <param name="item">The element to locate in the HashSet.</param>
        /// <returns>true if the HashSet contains the specified element; otherwise, false.</returns>
        public bool Contains(T item)
        {
            return _innerDictionary.ContainsKey(item);
        }

        /// <summary>
        /// Copies the elements of a HashSet to an array, starting at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from
        /// the HashSet. The array must have zero-based indexing</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            _innerDictionary.Keys.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of elements that are contained in a HashSet.
        /// </summary>
        public int Count
        {
            get { return _innerDictionary.Keys.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the HashSet is read-only.
        /// This property is always false.
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removes the specified element from a HashSet.
        /// </summary>
        /// <param name="item">The element to remove.</param>
        /// <returns>true if the element is successfully found and removed; otherwise, false. This
        /// method returns false if item is not found in the HashSet</returns>
        public bool Remove(T item)
        {
            return _innerDictionary.Remove(item);
        }

        /// <summary>
        /// Returns an enumerator that iterates through a HashSet.
        /// </summary>
        /// <returns>A HashSet.Enumerator object for the HashSet.</returns>
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
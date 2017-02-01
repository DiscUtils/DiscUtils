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

using System.Collections.Generic;

namespace DiscUtils.Registry
{
    internal abstract class ListCell : Cell
    {
        public ListCell(int index)
            : base(index) {}

        /// <summary>
        /// Gets the number of subkeys in this list.
        /// </summary>
        internal abstract int Count { get; }

        /// <summary>
        /// Searches for a key with a given name.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="cellIndex">The index of the cell, if found.</param>
        /// <returns>The search result.</returns>
        internal abstract int FindKey(string name, out int cellIndex);

        /// <summary>
        /// Enumerates all of the keys in the list.
        /// </summary>
        /// <param name="names">The list to populate.</param>
        internal abstract void EnumerateKeys(List<string> names);

        /// <summary>
        /// Enumerates all of the keys in the list.
        /// </summary>
        /// <returns>Enumeration of key cells.</returns>
        internal abstract IEnumerable<KeyNodeCell> EnumerateKeys();

        /// <summary>
        /// Adds a subkey to this list.
        /// </summary>
        /// <param name="name">The name of the subkey.</param>
        /// <param name="cellIndex">The cell index of the subkey.</param>
        /// <returns>The new cell index of the list, which may have changed.</returns>
        internal abstract int LinkSubKey(string name, int cellIndex);

        /// <summary>
        /// Removes a subkey from this list.
        /// </summary>
        /// <param name="name">The name of the subkey.</param>
        /// <returns>The new cell index of the list, which may have changed.</returns>
        internal abstract int UnlinkSubKey(string name);
    }
}
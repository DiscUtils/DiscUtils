//
// Copyright (c) 2008-2013, Kenneth Bell
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
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    /// <summary>
    /// Class providing information about a VHDX region table.
    /// </summary>
    public sealed class RegionTableInfo : ICollection<RegionInfo>
    {
        private readonly RegionTable _table;

        internal RegionTableInfo(RegionTable table)
        {
            _table = table;
        }

        /// <summary>
        /// Gets the checksum of the region table.
        /// </summary>
        public int Checksum
        {
            get { return (int)_table.Checksum; }
        }

        private IEnumerable<RegionInfo> Entries
        {
            get
            {
                foreach (KeyValuePair<Guid, RegionEntry> entry in _table.Regions)
                {
                    yield return new RegionInfo(entry.Value);
                }
            }
        }

        /// <summary>
        /// Gets the signature of the region table.
        /// </summary>
        public string Signature
        {
            get
            {
                byte[] buffer = new byte[4];
                EndianUtilities.WriteBytesLittleEndian(_table.Signature, buffer, 0);
                return EndianUtilities.BytesToString(buffer, 0, 4);
            }
        }

        /// <summary>
        /// Gets the number of metadata items present.
        /// </summary>
        public int Count
        {
            get { return (int)_table.EntryCount; }
        }

        /// <summary>
        /// Gets a value indicating whether this table is read-only (always true).
        /// </summary>
        public bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// Always throws InvalidOperationException.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void Add(RegionInfo item)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Always throws InvalidOperationException.
        /// </summary>
        public void Clear()
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Determines if the specified region is present already.
        /// </summary>
        /// <param name="item">The item to look for.</param>
        /// <returns><c>true</c> if present, else <c>false</c>.</returns>
        /// <remarks>The comparison is based on the region identity.</remarks>
        public bool Contains(RegionInfo item)
        {
            foreach (KeyValuePair<Guid, RegionEntry> entry in _table.Regions)
            {
                if (entry.Key == item.Guid)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Copies this region table to an array.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The index of the first item to populate in the array.</param>
        public void CopyTo(RegionInfo[] array, int arrayIndex)
        {
            int offset = 0;
            foreach (KeyValuePair<Guid, RegionEntry> entry in _table.Regions)
            {
                array[arrayIndex + offset] = new RegionInfo(entry.Value);
                ++offset;
            }
        }

        /// <summary>
        /// Removes an item from the table.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns><c>true</c> if the item was removed, else <c>false</c>.</returns>
        /// <remarks>Always throws InvalidOperationException as the table is read-only.</remarks>
        public bool Remove(RegionInfo item)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Gets an enumerator for the regions.
        /// </summary>
        /// <returns>A new enumerator.</returns>
        public IEnumerator<RegionInfo> GetEnumerator()
        {
            return Entries.GetEnumerator();
        }

        /// <summary>
        /// Gets an enumerator for the regions.
        /// </summary>
        /// <returns>A new enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Entries.GetEnumerator();
        }
    }
}
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
using System.Collections.Generic;
using System.Globalization;

namespace DiscUtils.Registry
{
    internal sealed class SubKeyHashedListCell : ListCell
    {
        private string _hashType;
        private short _numElements;
        private List<int> _subKeyIndexes;
        private List<uint> _nameHashes;
        private RegistryHive _hive;

        public SubKeyHashedListCell(RegistryHive hive, string hashType)
            : base(-1)
        {
            _hive = hive;
            _hashType = hashType;
            _subKeyIndexes = new List<int>();
            _nameHashes = new List<uint>();
        }

        public SubKeyHashedListCell(RegistryHive hive, int index)
            : base(index)
        {
            _hive = hive;
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            _hashType = Utilities.BytesToString(buffer, offset, 2);
            _numElements = Utilities.ToInt16LittleEndian(buffer, offset + 2);

            _subKeyIndexes = new List<int>(_numElements);
            _nameHashes = new List<uint>(_numElements);
            for (int i = 0; i < _numElements; ++i)
            {
                _subKeyIndexes.Add(Utilities.ToInt32LittleEndian(buffer, offset + 0x4 + (i * 0x8)));
                _nameHashes.Add(Utilities.ToUInt32LittleEndian(buffer, offset + 0x4 + (i * 0x8) + 0x4));
            }
            return 0x4 + _numElements * 0x8;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            Utilities.StringToBytes(_hashType, buffer, offset, 2);
            Utilities.WriteBytesLittleEndian(_numElements, buffer, offset + 0x2);
            for (int i = 0; i < _numElements; ++i)
            {
                Utilities.WriteBytesLittleEndian(_subKeyIndexes[i], buffer, offset + 0x4 + (i * 0x8));
                Utilities.WriteBytesLittleEndian(_nameHashes[i], buffer, offset + 0x4 + (i * 0x8) + 0x4);
            }
        }

        public override int Size
        {
            get { return 0x4 + _numElements * 0x8; }
        }

        /// <summary>
        /// Adds a new entry.
        /// </summary>
        /// <param name="name">The name of the subkey</param>
        /// <param name="cellIndex">The cell index of the subkey</param>
        /// <returns>The index of the new entry</returns>
        internal int Add(string name, int cellIndex)
        {
            for (int i = 0; i < _numElements; ++i)
            {
                KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(_subKeyIndexes[i]);
                if (string.Compare(cell.Name, name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    _subKeyIndexes.Insert(i, cellIndex);
                    _nameHashes.Insert(i, CalcHash(name));
                    _numElements++;
                    return i;
                }
            }

            _subKeyIndexes.Add(cellIndex);
            _nameHashes.Add(CalcHash(name));
            return _numElements++;
        }

        internal override int Count
        {
            get { return _subKeyIndexes.Count; }
        }

        internal override int FindKey(string name, out int cellIndex)
        {
            // Check first and last, to early abort if the name is outside the range of this list
            int result = FindKeyAt(name, 0, out cellIndex);
            if (result <= 0)
            {
                return result;
            }
            result = FindKeyAt(name, _subKeyIndexes.Count - 1, out cellIndex);
            if (result >= 0)
            {
                return result;
            }

            KeyFinder finder = new KeyFinder(_hive, name);
            int idx = _subKeyIndexes.BinarySearch(-1, finder);
            cellIndex = finder.CellIndex;
            return (idx < 0) ? -1 : 0;
        }

        internal override void EnumerateKeys(List<string> names)
        {
            for (int i = 0; i < _subKeyIndexes.Count; ++i)
            {
                names.Add(_hive.GetCell<KeyNodeCell>(_subKeyIndexes[i]).Name);
            }
        }

        internal override IEnumerable<KeyNodeCell> EnumerateKeys()
        {
            for (int i = 0; i < _subKeyIndexes.Count; ++i)
            {
                yield return _hive.GetCell<KeyNodeCell>(_subKeyIndexes[i]);
            }
        }

        internal override int LinkSubKey(string name, int cellIndex)
        {
            Add(name, cellIndex);
            return _hive.UpdateCell(this, true);
        }

        internal override int UnlinkSubKey(string name)
        {
            int index = IndexOf(name);
            if (index >= 0)
            {
                RemoveAt(index);
                return _hive.UpdateCell(this, true);
            }

            return Index;
        }

        /// <summary>
        /// Finds a subkey cell, returning it's index in this list.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal int IndexOf(string name)
        {
            foreach (var index in Find(name, 0))
            {
                KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(_subKeyIndexes[index]);
                if (cell.Name.ToUpperInvariant() == name.ToUpperInvariant())
                {
                    return index;
                }
            }

            return -1;
        }

        internal void RemoveAt(int index)
        {
            _nameHashes.RemoveAt(index);
            _subKeyIndexes.RemoveAt(index);
            _numElements--;
        }

        private uint CalcHash(string name)
        {
            uint hash = 0;
            if (_hashType == "lh")
            {
                for (int i = 0; i < name.Length; ++i)
                {
                    hash *= 37;
                    hash += char.ToUpper(name[i], CultureInfo.InvariantCulture);
                }
            }
            else
            {
                string hashStr = name + "\0\0\0\0";
                for (int i = 0; i < 4; ++i)
                {
                    hash |= (uint)((hashStr[i] & 0xFF) << (i * 8));
                }
            }
            return hash;
        }

        private int FindKeyAt(string name, int listIndex, out int cellIndex)
        {
            Cell cell = _hive.GetCell<Cell>(_subKeyIndexes[listIndex]);
            if (cell == null)
            {
                cellIndex = 0;
                return -1;
            }

            ListCell listCell = cell as ListCell;
            if (listCell != null)
            {
                return listCell.FindKey(name, out cellIndex);
            }

            cellIndex = _subKeyIndexes[listIndex];
            return string.Compare(name, ((KeyNodeCell)cell).Name, StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<int> Find(string name, int start)
        {
            if (_hashType == "lh")
            {
                return FindByHash(name, start);
            }
            else
            {
                return FindByPrefix(name, start);
            }
        }

        private IEnumerable<int> FindByHash(string name, int start)
        {
            uint hash = CalcHash(name);

            for (int i = start; i < _nameHashes.Count; ++i)
            {
                if (_nameHashes[i] == hash)
                {
                    yield return i;
                }
            }
        }

        private IEnumerable<int> FindByPrefix(string name, int start)
        {
            int compChars = Math.Min(name.Length, 4);
            string compStr = name.Substring(0, compChars).ToUpperInvariant() + "\0\0\0\0";

            for (int i = start; i < _nameHashes.Count; ++i)
            {
                bool match = true;
                uint hash = _nameHashes[i];

                for (int j = 0; j < 4; ++j)
                {
                    char ch = (char)((hash >> (j * 8)) & 0xFF);
                    if (char.ToUpperInvariant(ch) != compStr[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    yield return i;
                }
            }
        }

        private class KeyFinder : IComparer<int>
        {
            private RegistryHive _hive;
            private string _searchName;

            public KeyFinder(RegistryHive hive, string searchName)
            {
                _hive = hive;
                _searchName = searchName;
            }

            public int CellIndex { get; set; }

            #region IComparer<int> Members

            public int Compare(int x, int y)
            {
                // TODO: Be more efficient at ruling out no-hopes by using the hash values

                KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(x);
                int result = string.Compare(((KeyNodeCell)cell).Name, _searchName, StringComparison.OrdinalIgnoreCase);
                if (result == 0)
                {
                    CellIndex = x;
                }
                return result;
            }

            #endregion
        }
    }

}

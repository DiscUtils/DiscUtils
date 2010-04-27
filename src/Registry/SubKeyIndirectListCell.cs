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

namespace DiscUtils.Registry
{
    internal sealed class SubKeyIndirectListCell : ListCell
    {
        private RegistryHive _hive;
        private string _listType;
        private List<int> _listIndexes;

        public SubKeyIndirectListCell(RegistryHive hive, int index)
            : base(index)
        {
            _hive = hive;
        }

        public string ListType
        {
            get { return _listType; }
        }

        public List<int> CellIndexes
        {
            get { return _listIndexes; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            _listType = Utilities.BytesToString(buffer, offset, 2);
            int numElements = Utilities.ToInt16LittleEndian(buffer, offset + 2);
            _listIndexes = new List<int>(numElements);

            for (int i = 0; i < numElements; ++i)
            {
                _listIndexes.Add(Utilities.ToInt32LittleEndian(buffer, offset + 0x4 + (i * 0x4)));
            }

            return 4 + (_listIndexes.Count * 4);
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            Utilities.StringToBytes(_listType, buffer, offset, 2);
            Utilities.WriteBytesLittleEndian((ushort)_listIndexes.Count, buffer, offset + 2);
            for (int i = 0; i < _listIndexes.Count; ++i)
            {
                Utilities.WriteBytesLittleEndian(_listIndexes[i], buffer, offset + 4 + (i * 4));
            }
        }

        public override int Size
        {
            get { return 4 + (_listIndexes.Count * 4); }
        }

        internal override int Count
        {
            get
            {
                int total = 0;
                foreach (var cellIndex in _listIndexes)
                {
                    Cell cell = _hive.GetCell<Cell>(cellIndex);
                    ListCell listCell = cell as ListCell;
                    if (listCell != null)
                    {
                        total += listCell.Count;
                    }
                    else
                    {
                        total++;
                    }
                }
                return total;
            }
        }

        internal override int FindKey(string name, out int cellIndex)
        {
            if (_listIndexes.Count <= 0)
            {
                cellIndex = 0;
                return -1;
            }

            // Check first and last, to early abort if the name is outside the range of this list
            int result = DoFindKey(name, 0, out cellIndex);
            if (result <= 0)
            {
                return result;
            }
            result = DoFindKey(name, _listIndexes.Count - 1, out cellIndex);
            if (result >= 0)
            {
                return result;
            }

            KeyFinder finder = new KeyFinder(_hive, name);
            int idx = _listIndexes.BinarySearch(-1, finder);
            cellIndex = finder.CellIndex;
            return (idx < 0) ? -1 : 0;
        }

        internal override void EnumerateKeys(List<string> names)
        {
            for (int i = 0; i < _listIndexes.Count; ++i)
            {
                Cell cell = _hive.GetCell<Cell>(_listIndexes[i]);
                ListCell listCell = cell as ListCell;
                if (listCell != null)
                {
                    listCell.EnumerateKeys(names);
                }
                else
                {
                    names.Add(((KeyNodeCell)cell).Name);
                }
            }
        }

        internal override IEnumerable<KeyNodeCell> EnumerateKeys()
        {
            for (int i = 0; i < _listIndexes.Count; ++i)
            {
                Cell cell = _hive.GetCell<Cell>(_listIndexes[i]);
                ListCell listCell = cell as ListCell;
                if (listCell != null)
                {
                    foreach (var keyNodeCell in listCell.EnumerateKeys())
                    {
                        yield return keyNodeCell;
                    }
                }
                else
                {
                    yield return (KeyNodeCell)cell;
                }
            }
        }

        internal override int LinkSubKey(string name, int cellIndex)
        {
            // Look for the first sublist that has a subkey name greater than name
            if (ListType == "ri")
            {
                if (_listIndexes.Count == 0)
                {
                    throw new NotImplementedException("Empty indirect list");
                }

                for (int i = 0; i < _listIndexes.Count - 1; ++i)
                {
                    int tempIndex;
                    ListCell cell = _hive.GetCell<ListCell>(_listIndexes[i]);
                    if (cell.FindKey(name, out tempIndex) <= 0)
                    {
                        _listIndexes[i] = cell.LinkSubKey(name, cellIndex);
                        return _hive.UpdateCell(this, false);
                    }
                }

                ListCell lastCell = _hive.GetCell<ListCell>(_listIndexes[_listIndexes.Count - 1]);
                _listIndexes[_listIndexes.Count - 1] = lastCell.LinkSubKey(name, cellIndex);
                return _hive.UpdateCell(this, false);
            }
            else
            {
                for (int i = 0; i < _listIndexes.Count; ++i)
                {
                    KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(_listIndexes[i]);
                    if (string.Compare(name, cell.Name, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        _listIndexes.Insert(i, cellIndex);
                        return _hive.UpdateCell(this, true);
                    }
                }

                _listIndexes.Add(cellIndex);
                return _hive.UpdateCell(this, true);
            }
        }

        internal override int UnlinkSubKey(string name)
        {
            if (ListType == "ri")
            {
                if (_listIndexes.Count == 0)
                {
                    throw new NotImplementedException("Empty indirect list");
                }

                for (int i = 0; i < _listIndexes.Count; ++i)
                {
                    int tempIndex;
                    ListCell cell = _hive.GetCell<ListCell>(_listIndexes[i]);
                    if (cell.FindKey(name, out tempIndex) <= 0)
                    {
                        _listIndexes[i] = cell.UnlinkSubKey(name);
                        if (cell.Count == 0)
                        {
                            _hive.FreeCell(_listIndexes[i]);
                            _listIndexes.RemoveAt(i);
                        }
                        return _hive.UpdateCell(this, false);
                    }
                }
            }
            else
            {
                for (int i = 0; i < _listIndexes.Count; ++i)
                {
                    KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(_listIndexes[i]);
                    if (string.Compare(name, cell.Name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        _listIndexes.RemoveAt(i);
                        return _hive.UpdateCell(this, true);
                    }
                }
            }

            return Index;
        }

        private int DoFindKey(string name, int listIndex, out int cellIndex)
        {
            Cell cell = _hive.GetCell<Cell>(_listIndexes[listIndex]);
            ListCell listCell = cell as ListCell;
            if (listCell != null)
            {
                return listCell.FindKey(name, out cellIndex);
            }

            cellIndex = _listIndexes[listIndex];
            return string.Compare(name, ((KeyNodeCell)cell).Name, StringComparison.OrdinalIgnoreCase);
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
                Cell cell = _hive.GetCell<Cell>(x);
                ListCell listCell = cell as ListCell;

                int result;
                if (listCell != null)
                {
                    int cellIndex;
                    result = listCell.FindKey(_searchName, out cellIndex);
                    if (result == 0)
                    {
                        CellIndex = cellIndex;
                    }
                    return -result;
                }
                else
                {
                    result = string.Compare(((KeyNodeCell)cell).Name, _searchName, StringComparison.OrdinalIgnoreCase);
                    if (result == 0)
                    {
                        CellIndex = x;
                    }
                }

                return result;
            }

            #endregion
        }
    }


}

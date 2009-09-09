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

        public override void ReadFrom(byte[] buffer, int offset)
        {
            _listType = Utilities.BytesToString(buffer, offset, 2);
            int numElements = Utilities.ToInt16LittleEndian(buffer, offset + 2);
            _listIndexes = new List<int>(numElements);

            for (int i = 0; i < numElements; ++i)
            {
                _listIndexes.Add(Utilities.ToInt32LittleEndian(buffer, offset + 0x4 + (i * 0x4)));
            }
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override int Size
        {
            get { throw new NotImplementedException(); }
        }

        internal override int FindKey(string name, out int cellIndex)
        {
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

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
using System.Security.AccessControl;
using System.Text;

namespace DiscUtils.Registry
{
    /// <summary>
    /// A key within a registry hive.
    /// </summary>
    public sealed class RegistryKey
    {
        private RegistryHive _hive;
        private int _cellIndex;
        private KeyNodeCell _cell;

        internal RegistryKey(RegistryHive hive, int cellIndex, KeyNodeCell cell)
        {
            _hive = hive;
            _cellIndex = cellIndex;
            _cell = cell;
        }

        /// <summary>
        /// Gets the Security Descriptor applied to the registry key.
        /// </summary>
        /// <returns>The security descriptor as a RegistrySecurity instance.</returns>
        public RegistrySecurity GetAccessControl()
        {
            if (_cell.SecurityIndex > 0)
            {
                SecurityCell secCell = _hive.GetCell<SecurityCell>(_cell.SecurityIndex);
                return secCell.SecurityDescriptor;
            }
            return null;
        }

        /// <summary>
        /// Gets the names of all child sub keys.
        /// </summary>
        /// <returns>The names of the sub keys</returns>
        public string[] GetSubKeyNames()
        {
            List<string> names = new List<string>();

            if (_cell.NumSubKeys != 0)
            {
                Cell list = _hive.GetCell<Cell>(_cell.SubKeysIndex);

                SubKeyIndirectListCell indirectList = list as SubKeyIndirectListCell;
                if (indirectList != null)
                {
                    foreach (int listIndex in indirectList.Lists)
                    {
                        SubKeyHashedListCell hashList = _hive.GetCell<SubKeyHashedListCell>(listIndex);
                        foreach (int index in hashList.SubKeys)
                        {
                            names.Add(_hive.GetCell<KeyNodeCell>(index).Name);
                        }
                    }
                }
                else
                {
                    SubKeyHashedListCell hashList = (SubKeyHashedListCell)list;
                    foreach (int index in hashList.SubKeys)
                    {
                        names.Add(_hive.GetCell<KeyNodeCell>(index).Name);
                    }
                }
            }

            return names.ToArray();
        }

        /// <summary>
        /// Gets a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to retrieve</param>
        /// <returns>The value as a .NET object, <see cref="RegistryValue.Value"/>.</returns>
        public object GetValue(string name)
        {
            RegistryValue regVal = GetRegistryValue(name);
            if (regVal != null)
            {
                return regVal.Value;
            }
            return null;
        }

        /// <summary>
        /// Gets a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to retrieve.</param>
        /// <param name="defaultValue">The default value to return, if no existing value is stored.</param>
        /// <returns>The value as a .NET object, <see cref="RegistryValue.Value"/>.</returns>
        public object GetValue(string name, object defaultValue)
        {
            return GetValue(name) ?? defaultValue;
        }

        /// <summary>
        /// Deletes a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to delete.</param>
        public void DeleteValue(string name)
        {
            DeleteValue(name, true);
        }

        /// <summary>
        /// Deletes a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to delete.</param>
        /// <param name="throwOnMissingValue">Throws ArgumentException if <c>name</c> doesn't exist</param>
        public void DeleteValue(string name, bool throwOnMissingValue)
        {
            bool foundValue = false;

            if (_cell.NumValues != 0)
            {
                byte[] valueList = _hive.RawCellData(_cell.ValueListIndex, _cell.NumValues * 4);

                int i = 0;
                while (i < _cell.NumValues)
                {
                    int valueIndex = Utilities.ToInt32LittleEndian(valueList, i * 4);
                    ValueCell valueCell = _hive.GetCell<ValueCell>(valueIndex);
                    if (string.Compare(valueCell.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        foundValue = true;
                        _hive.FreeCell(valueIndex);
                        _cell.NumValues--;
                        _cellIndex = _hive.UpdateCell(_cellIndex, _cell);
                        break;
                    }

                    ++i;
                }

                // Move following value's to fill gap
                if (i < _cell.NumValues)
                {
                    while (i < _cell.NumValues)
                    {
                        int valueIndex = Utilities.ToInt32LittleEndian(valueList, (i + 1) * 4);
                        Utilities.WriteBytesLittleEndian(valueIndex, valueList, i * 4);

                        ++i;
                    }

                    _hive.WriteRawCellData(_cell.ValueListIndex, valueList, 0, _cell.NumValues * 4);
                }

                // TODO: Update maxbytes for value name and value content if this was the largest value for either.
                // Windows seems to repair this info, if not accurate, though.
            }

            if (throwOnMissingValue && !foundValue)
            {
                throw new ArgumentException("No such value: " + name, "name");
            }
        }

        /// <summary>
        /// Gets the type of a named value.
        /// </summary>
        /// <param name="name">The name of the value to inspect.</param>
        /// <returns>The value's type.</returns>
        public RegistryValueType GetValueType(string name)
        {
            RegistryValue regVal = GetRegistryValue(name);
            if (regVal != null)
            {
                return regVal.DataType;
            }
            return RegistryValueType.None;
        }

        /// <summary>
        /// Gets the names of all values in this key.
        /// </summary>
        /// <returns>An array of strings containing the value names</returns>
        public string[] GetValueNames()
        {
            List<string> names = new List<string>();
            foreach (var value in Values)
            {
                names.Add(value.Name);
            }
            return names.ToArray();
        }

        /// <summary>
        /// Gets the name of this key.
        /// </summary>
        public string Name
        {
            get
            {
                RegistryKey parent = Parent;
                if (parent != null && ((parent.Flags & RegistryKeyFlags.Root) == 0))
                {
                    return parent.Name + @"\" + _cell.Name;
                }
                else
                {
                    return _cell.Name;
                }
            }
        }

        /// <summary>
        /// Gets the number of child keys.
        /// </summary>
        public int SubKeyCount
        {
            get { return _cell.NumSubKeys; }
        }

        /// <summary>
        /// Gets the number of values in this key.
        /// </summary>
        public int ValueCount
        {
            get { return _cell.NumValues; }
        }

        /// <summary>
        /// Gets the time the key was last modified.
        /// </summary>
        public DateTime Timestamp
        {
            get { return _cell.Timestamp; }
        }

        /// <summary>
        /// Gets the parent key, or <c>null</c> if this is the root key.
        /// </summary>
        public RegistryKey Parent
        {
            get
            {
                if ((_cell.Flags & RegistryKeyFlags.Root) == 0)
                {
                    return new RegistryKey(_hive, _cell.ParentIndex, _hive.GetCell<KeyNodeCell>(_cell.ParentIndex));
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the flags of this registry key.
        /// </summary>
        public RegistryKeyFlags Flags
        {
            get { return _cell.Flags; }
        }

        /// <summary>
        /// Gets the class name of this registry key.
        /// </summary>
        /// <remarks>Class name is rarely used.</remarks>
        public string ClassName
        {
            get
            {
                if (_cell.ClassNameIndex > 0)
                {
                    return Encoding.Unicode.GetString(_hive.RawCellData(_cell.ClassNameIndex, _cell.ClassNameLength));
                }
                return null;
            }
        }

        /// <summary>
        /// Opens a sub key.
        /// </summary>
        /// <param name="path">The relative path to the sub key.</param>
        /// <returns>The sub key, or <c>null</c> if not found.</returns>
        public RegistryKey OpenSubKey(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return this;
            }

            string[] split = path.Split(new char[] { '\\' }, 2);
            int cellIndex;
            KeyNodeCell cell = SearchSubKeys(split[0], out cellIndex);

            if (cell == null)
            {
                return null;
            }
            else if (split.Length == 1)
            {
                return new RegistryKey(_hive, cellIndex, cell);
            }
            else
            {
                return new RegistryKey(_hive, cellIndex, cell).OpenSubKey(split[1]);
            }
        }

        /// <summary>
        /// Gets an enumerator over all sub child keys.
        /// </summary>
        public IEnumerable<RegistryKey> SubKeys
        {
            get
            {
                if (_cell.NumSubKeys != 0)
                {
                    Cell list = _hive.GetCell<Cell>(_cell.SubKeysIndex);

                    SubKeyIndirectListCell indirectList = list as SubKeyIndirectListCell;
                    if (indirectList != null)
                    {
                        foreach (int listIndex in indirectList.Lists)
                        {
                            SubKeyHashedListCell hashList = _hive.GetCell<SubKeyHashedListCell>(listIndex);
                            foreach (int index in hashList.SubKeys)
                            {
                                yield return new RegistryKey(_hive, index, _hive.GetCell<KeyNodeCell>(index));
                            }
                        }
                    }
                    else
                    {
                        SubKeyHashedListCell hashList = (SubKeyHashedListCell)list;
                        foreach (int index in hashList.SubKeys)
                        {
                            yield return new RegistryKey(_hive, index, _hive.GetCell<KeyNodeCell>(index));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets an enumerator over all values in this key.
        /// </summary>
        public IEnumerable<RegistryValue> Values
        {
            get
            {
                if (_cell.NumValues != 0)
                {
                    byte[] valueList = _hive.RawCellData(_cell.ValueListIndex, _cell.NumValues * 4);

                    for (int i = 0; i < _cell.NumValues; ++i)
                    {
                        int valueIndex = Utilities.ToInt32LittleEndian(valueList, i * 4);
                        yield return new RegistryValue(_hive, _hive.GetCell<ValueCell>(valueIndex));
                    }
                }
            }
        }

        private RegistryValue GetRegistryValue(string name)
        {
            if (_cell.NumValues != 0)
            {
                byte[] valueList = _hive.RawCellData(_cell.ValueListIndex, _cell.NumValues * 4);

                for (int i = 0; i < _cell.NumValues; ++i)
                {
                    int valueIndex = Utilities.ToInt32LittleEndian(valueList, i * 4);
                    ValueCell cell = _hive.GetCell<ValueCell>(valueIndex);
                    if (string.Compare(cell.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return new RegistryValue(_hive, cell);
                    }
                }
            }

            return null;
        }

        private KeyNodeCell SearchSubKeys(string name, out int index)
        {
            if (_cell.NumSubKeys != 0)
            {
                Cell list = _hive.GetCell<Cell>(_cell.SubKeysIndex);

                SubKeyIndirectListCell indirectList = list as SubKeyIndirectListCell;
                if (indirectList != null)
                {
                    foreach (int listIndex in indirectList.Lists)
                    {
                        SubKeyHashedListCell hashList = _hive.GetCell<SubKeyHashedListCell>(listIndex);
                        foreach(int keyIndex in hashList.Find(name, 0))
                        {
                            KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(keyIndex);
                            if (cell.Name.ToUpperInvariant() == name.ToUpperInvariant())
                            {
                                index = keyIndex;
                                return cell;
                            }
                        }
                    }
                }
                else
                {
                    SubKeyHashedListCell hashList = (SubKeyHashedListCell)list;
                    foreach (int keyIndex in hashList.Find(name, 0))
                    {
                        KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(keyIndex);
                        if (cell.Name.ToUpperInvariant() == name.ToUpperInvariant())
                        {
                            index = keyIndex;
                            return cell;
                        }
                    }
                }
            }

            index = 0;
            return null;
        }
    }
}

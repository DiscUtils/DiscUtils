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
using System.Security.AccessControl;
using System.Text;
using DiscUtils.Streams;
using Microsoft.Win32;

namespace DiscUtils.Registry
{
    /// <summary>
    /// A key within a registry hive.
    /// </summary>
    public sealed class RegistryKey
    {
        private readonly KeyNodeCell _cell;
        private readonly RegistryHive _hive;

        internal RegistryKey(RegistryHive hive, KeyNodeCell cell)
        {
            _hive = hive;
            _cell = cell;
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
        /// Gets the flags of this registry key.
        /// </summary>
        public RegistryKeyFlags Flags
        {
            get { return _cell.Flags; }
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
                return _cell.Name;
            }
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
                    return new RegistryKey(_hive, _hive.GetCell<KeyNodeCell>(_cell.ParentIndex));
                }
                return null;
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
        /// Gets an enumerator over all sub child keys.
        /// </summary>
        public IEnumerable<RegistryKey> SubKeys
        {
            get
            {
                if (_cell.NumSubKeys != 0)
                {
                    ListCell list = _hive.GetCell<ListCell>(_cell.SubKeysIndex);
                    foreach (KeyNodeCell key in list.EnumerateKeys())
                    {
                        yield return new RegistryKey(_hive, key);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the time the key was last modified.
        /// </summary>
        public DateTime Timestamp
        {
            get { return _cell.Timestamp; }
        }

        /// <summary>
        /// Gets the number of values in this key.
        /// </summary>
        public int ValueCount
        {
            get { return _cell.NumValues; }
        }

        /// <summary>
        /// Gets an enumerator over all values in this key.
        /// </summary>
        private IEnumerable<RegistryValue> Values
        {
            get
            {
                if (_cell.NumValues != 0)
                {
                    byte[] valueList = _hive.RawCellData(_cell.ValueListIndex, _cell.NumValues * 4);

                    for (int i = 0; i < _cell.NumValues; ++i)
                    {
                        int valueIndex = EndianUtilities.ToInt32LittleEndian(valueList, i * 4);
                        yield return new RegistryValue(_hive, _hive.GetCell<ValueCell>(valueIndex));
                    }
                }
            }
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
        /// <returns>The names of the sub keys.</returns>
        public string[] GetSubKeyNames()
        {
            List<string> names = new List<string>();

            if (_cell.NumSubKeys != 0)
            {
                _hive.GetCell<ListCell>(_cell.SubKeysIndex).EnumerateKeys(names);
            }

            return names.ToArray();
        }

        /// <summary>
        /// Gets a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to retrieve.</param>
        /// <returns>The value as a .NET object.</returns>
        /// <remarks>The mapping from registry type of .NET type is as follows:
        /// <list type="table">
        ///   <listheader>
        ///     <term>Value Type</term>
        ///     <term>.NET type</term>
        ///   </listheader>
        ///   <item>
        ///     <description>String</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>ExpandString</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>Link</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>DWord</description>
        ///     <description>uint</description>
        ///   </item>
        ///   <item>
        ///     <description>DWordBigEndian</description>
        ///     <description>uint</description>
        ///   </item>
        ///   <item>
        ///     <description>MultiString</description>
        ///     <description>string[]</description>
        ///   </item>
        ///   <item>
        ///     <description>QWord</description>
        ///     <description>ulong</description>
        ///   </item>
        /// </list>
        /// </remarks>
        public object GetValue(string name)
        {
            return GetValue(name, null, RegistryValueOptions.None);
        }

        /// <summary>
        /// Gets a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to retrieve.</param>
        /// <param name="defaultValue">The default value to return, if no existing value is stored.</param>
        /// <returns>The value as a .NET object.</returns>
        /// <remarks>The mapping from registry type of .NET type is as follows:
        /// <list type="table">
        ///   <listheader>
        ///     <term>Value Type</term>
        ///     <term>.NET type</term>
        ///   </listheader>
        ///   <item>
        ///     <description>String</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>ExpandString</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>Link</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>DWord</description>
        ///     <description>uint</description>
        ///   </item>
        ///   <item>
        ///     <description>DWordBigEndian</description>
        ///     <description>uint</description>
        ///   </item>
        ///   <item>
        ///     <description>MultiString</description>
        ///     <description>string[]</description>
        ///   </item>
        ///   <item>
        ///     <description>QWord</description>
        ///     <description>ulong</description>
        ///   </item>
        /// </list>
        /// </remarks>
        public object GetValue(string name, object defaultValue)
        {
            return GetValue(name, defaultValue, RegistryValueOptions.None);
        }

        /// <summary>
        /// Gets a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to retrieve.</param>
        /// <param name="defaultValue">The default value to return, if no existing value is stored.</param>
        /// <param name="options">Flags controlling how the value is processed before it's returned.</param>
        /// <returns>The value as a .NET object.</returns>
        /// <remarks>The mapping from registry type of .NET type is as follows:
        /// <list type="table">
        ///   <listheader>
        ///     <term>Value Type</term>
        ///     <term>.NET type</term>
        ///   </listheader>
        ///   <item>
        ///     <description>String</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>ExpandString</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>Link</description>
        ///     <description>string</description>
        ///   </item>
        ///   <item>
        ///     <description>DWord</description>
        ///     <description>uint</description>
        ///   </item>
        ///   <item>
        ///     <description>DWordBigEndian</description>
        ///     <description>uint</description>
        ///   </item>
        ///   <item>
        ///     <description>MultiString</description>
        ///     <description>string[]</description>
        ///   </item>
        ///   <item>
        ///     <description>QWord</description>
        ///     <description>ulong</description>
        ///   </item>
        /// </list>
        /// </remarks>
        public object GetValue(string name, object defaultValue, RegistryValueOptions options)
        {
            RegistryValue regVal = GetRegistryValue(name);
            if (regVal != null)
            {
                if (regVal.DataType == RegistryValueType.ExpandString &&
                    (options & RegistryValueOptions.DoNotExpandEnvironmentNames) == 0)
                {
                    return Environment.ExpandEnvironmentVariables((string)regVal.Value);
                }
                return regVal.Value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Sets a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to store.</param>
        /// <param name="value">The value to store.</param>
        public void SetValue(string name, object value)
        {
            SetValue(name, value, RegistryValueType.None);
        }

        /// <summary>
        /// Sets a named value stored within this key.
        /// </summary>
        /// <param name="name">The name of the value to store.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="valueType">The registry type of the data.</param>
        public void SetValue(string name, object value, RegistryValueType valueType)
        {
            RegistryValue valObj = GetRegistryValue(name);
            if (valObj == null)
            {
                valObj = AddRegistryValue(name);
            }

            valObj.SetValue(value, valueType);
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
        /// <param name="throwOnMissingValue">Throws ArgumentException if <c>name</c> doesn't exist.</param>
        public void DeleteValue(string name, bool throwOnMissingValue)
        {
            bool foundValue = false;

            if (_cell.NumValues != 0)
            {
                byte[] valueList = _hive.RawCellData(_cell.ValueListIndex, _cell.NumValues * 4);

                int i = 0;
                while (i < _cell.NumValues)
                {
                    int valueIndex = EndianUtilities.ToInt32LittleEndian(valueList, i * 4);
                    ValueCell valueCell = _hive.GetCell<ValueCell>(valueIndex);
                    if (string.Compare(valueCell.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        foundValue = true;
                        _hive.FreeCell(valueIndex);
                        _cell.NumValues--;
                        _hive.UpdateCell(_cell, false);
                        break;
                    }

                    ++i;
                }

                // Move following value's to fill gap
                if (i < _cell.NumValues)
                {
                    while (i < _cell.NumValues)
                    {
                        int valueIndex = EndianUtilities.ToInt32LittleEndian(valueList, (i + 1) * 4);
                        EndianUtilities.WriteBytesLittleEndian(valueIndex, valueList, i * 4);

                        ++i;
                    }

                    _hive.WriteRawCellData(_cell.ValueListIndex, valueList, 0, _cell.NumValues * 4);
                }

                // TODO: Update maxbytes for value name and value content if this was the largest value for either.
                // Windows seems to repair this info, if not accurate, though.
            }

            if (throwOnMissingValue && !foundValue)
            {
                throw new ArgumentException("No such value: " + name, nameof(name));
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
        /// <returns>An array of strings containing the value names.</returns>
        public string[] GetValueNames()
        {
            List<string> names = new List<string>();
            foreach (RegistryValue value in Values)
            {
                names.Add(value.Name);
            }

            return names.ToArray();
        }

        /// <summary>
        /// Creates or opens a subkey.
        /// </summary>
        /// <param name="subkey">The relative path the the subkey.</param>
        /// <returns>The subkey.</returns>
        public RegistryKey CreateSubKey(string subkey)
        {
            if (string.IsNullOrEmpty(subkey))
            {
                return this;
            }

            string[] split = subkey.Split(new[] { '\\' }, 2);
            int cellIndex = FindSubKeyCell(split[0]);

            if (cellIndex < 0)
            {
                KeyNodeCell newKeyCell = new KeyNodeCell(split[0], _cell.Index);
                newKeyCell.SecurityIndex = _cell.SecurityIndex;
                ReferenceSecurityCell(newKeyCell.SecurityIndex);
                _hive.UpdateCell(newKeyCell, true);

                LinkSubKey(split[0], newKeyCell.Index);

                if (split.Length == 1)
                {
                    return new RegistryKey(_hive, newKeyCell);
                }
                return new RegistryKey(_hive, newKeyCell).CreateSubKey(split[1]);
            }
            KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(cellIndex);
            if (split.Length == 1)
            {
                return new RegistryKey(_hive, cell);
            }
            return new RegistryKey(_hive, cell).CreateSubKey(split[1]);
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

            string[] split = path.Split(new[] { '\\' }, 2);
            int cellIndex = FindSubKeyCell(split[0]);

            if (cellIndex < 0)
            {
                return null;
            }
            KeyNodeCell cell = _hive.GetCell<KeyNodeCell>(cellIndex);
            if (split.Length == 1)
            {
                return new RegistryKey(_hive, cell);
            }
            return new RegistryKey(_hive, cell).OpenSubKey(split[1]);
        }

        /// <summary>
        /// Deletes a subkey and any child subkeys recursively. The string subkey is not case-sensitive.
        /// </summary>
        /// <param name="subkey">The subkey to delete.</param>
        public void DeleteSubKeyTree(string subkey)
        {
            RegistryKey subKeyObj = OpenSubKey(subkey);
            if (subKeyObj == null)
            {
                return;
            }

            if ((subKeyObj.Flags & RegistryKeyFlags.Root) != 0)
            {
                throw new ArgumentException("Attempt to delete root key");
            }

            foreach (string child in subKeyObj.GetSubKeyNames())
            {
                subKeyObj.DeleteSubKeyTree(child);
            }

            DeleteSubKey(subkey);
        }

        /// <summary>
        /// Deletes the specified subkey. The string subkey is not case-sensitive.
        /// </summary>
        /// <param name="subkey">The subkey to delete.</param>
        public void DeleteSubKey(string subkey)
        {
            DeleteSubKey(subkey, true);
        }

        /// <summary>
        /// Deletes the specified subkey. The string subkey is not case-sensitive.
        /// </summary>
        /// <param name="subkey">The subkey to delete.</param>
        /// <param name="throwOnMissingSubKey"><c>true</c> to throw an argument exception if <c>subkey</c> doesn't exist.</param>
        public void DeleteSubKey(string subkey, bool throwOnMissingSubKey)
        {
            if (string.IsNullOrEmpty(subkey))
            {
                throw new ArgumentException("Invalid SubKey", nameof(subkey));
            }

            string[] split = subkey.Split(new[] { '\\' }, 2);

            int subkeyCellIndex = FindSubKeyCell(split[0]);
            if (subkeyCellIndex < 0)
            {
                if (throwOnMissingSubKey)
                {
                    throw new ArgumentException("No such SubKey", nameof(subkey));
                }
                return;
            }

            KeyNodeCell subkeyCell = _hive.GetCell<KeyNodeCell>(subkeyCellIndex);

            if (split.Length == 1)
            {
                if (subkeyCell.NumSubKeys != 0)
                {
                    throw new InvalidOperationException("The registry key has subkeys");
                }

                if (subkeyCell.ClassNameIndex != -1)
                {
                    _hive.FreeCell(subkeyCell.ClassNameIndex);
                    subkeyCell.ClassNameIndex = -1;
                    subkeyCell.ClassNameLength = 0;
                }

                if (subkeyCell.SecurityIndex != -1)
                {
                    DereferenceSecurityCell(subkeyCell.SecurityIndex);
                    subkeyCell.SecurityIndex = -1;
                }

                if (subkeyCell.SubKeysIndex != -1)
                {
                    FreeSubKeys(subkeyCell);
                }

                if (subkeyCell.ValueListIndex != -1)
                {
                    FreeValues(subkeyCell);
                }

                UnlinkSubKey(subkey);
                _hive.FreeCell(subkeyCellIndex);
                _hive.UpdateCell(_cell, false);
            }
            else
            {
                new RegistryKey(_hive, subkeyCell).DeleteSubKey(split[1], throwOnMissingSubKey);
            }
        }

        private RegistryValue GetRegistryValue(string name)
        {
            if (name != null && name.Length == 0)
            {
                name = null;
            }

            if (_cell.NumValues != 0)
            {
                byte[] valueList = _hive.RawCellData(_cell.ValueListIndex, _cell.NumValues * 4);

                for (int i = 0; i < _cell.NumValues; ++i)
                {
                    int valueIndex = EndianUtilities.ToInt32LittleEndian(valueList, i * 4);
                    ValueCell cell = _hive.GetCell<ValueCell>(valueIndex);
                    if (string.Compare(cell.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return new RegistryValue(_hive, cell);
                    }
                }
            }

            return null;
        }

        private RegistryValue AddRegistryValue(string name)
        {
            byte[] valueList = _hive.RawCellData(_cell.ValueListIndex, _cell.NumValues * 4);
            if (valueList == null)
            {
                valueList = new byte[0];
            }

            int insertIdx = 0;
            while (insertIdx < _cell.NumValues)
            {
                int valueCellIndex = EndianUtilities.ToInt32LittleEndian(valueList, insertIdx * 4);
                ValueCell cell = _hive.GetCell<ValueCell>(valueCellIndex);
                if (string.Compare(name, cell.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    break;
                }

                ++insertIdx;
            }

            // Allocate a new value cell (note _hive.UpdateCell does actual allocation).
            ValueCell valueCell = new ValueCell(name);
            _hive.UpdateCell(valueCell, true);

            // Update the value list, re-allocating if necessary
            byte[] newValueList = new byte[_cell.NumValues * 4 + 4];
            Array.Copy(valueList, 0, newValueList, 0, insertIdx * 4);
            EndianUtilities.WriteBytesLittleEndian(valueCell.Index, newValueList, insertIdx * 4);
            Array.Copy(valueList, insertIdx * 4, newValueList, insertIdx * 4 + 4, (_cell.NumValues - insertIdx) * 4);
            if (_cell.ValueListIndex == -1 ||
                !_hive.WriteRawCellData(_cell.ValueListIndex, newValueList, 0, newValueList.Length))
            {
                int newListCellIndex = _hive.AllocateRawCell(MathUtilities.RoundUp(newValueList.Length, 8));
                _hive.WriteRawCellData(newListCellIndex, newValueList, 0, newValueList.Length);

                if (_cell.ValueListIndex != -1)
                {
                    _hive.FreeCell(_cell.ValueListIndex);
                }

                _cell.ValueListIndex = newListCellIndex;
            }

            // Record the new value and save this cell
            _cell.NumValues++;
            _hive.UpdateCell(_cell, false);

            // Finally, set the data in the value cell
            return new RegistryValue(_hive, valueCell);
        }

        private int FindSubKeyCell(string name)
        {
            if (_cell.NumSubKeys != 0)
            {
                ListCell listCell = _hive.GetCell<ListCell>(_cell.SubKeysIndex);

                int cellIndex;
                if (listCell.FindKey(name, out cellIndex) == 0)
                {
                    return cellIndex;
                }
            }

            return -1;
        }

        private void LinkSubKey(string name, int cellIndex)
        {
            if (_cell.SubKeysIndex == -1)
            {
                SubKeyHashedListCell newListCell = new SubKeyHashedListCell(_hive, "lf");
                newListCell.Add(name, cellIndex);
                _hive.UpdateCell(newListCell, true);
                _cell.NumSubKeys = 1;
                _cell.SubKeysIndex = newListCell.Index;
            }
            else
            {
                ListCell list = _hive.GetCell<ListCell>(_cell.SubKeysIndex);
                _cell.SubKeysIndex = list.LinkSubKey(name, cellIndex);
                _cell.NumSubKeys++;
            }

            _hive.UpdateCell(_cell, false);
        }

        private void UnlinkSubKey(string name)
        {
            if (_cell.SubKeysIndex == -1 || _cell.NumSubKeys == 0)
            {
                throw new InvalidOperationException("No subkey list");
            }

            ListCell list = _hive.GetCell<ListCell>(_cell.SubKeysIndex);
            _cell.SubKeysIndex = list.UnlinkSubKey(name);
            _cell.NumSubKeys--;
        }

        private void ReferenceSecurityCell(int cellIndex)
        {
            SecurityCell sc = _hive.GetCell<SecurityCell>(cellIndex);
            sc.UsageCount++;
            _hive.UpdateCell(sc, false);
        }

        private void DereferenceSecurityCell(int cellIndex)
        {
            SecurityCell sc = _hive.GetCell<SecurityCell>(cellIndex);
            sc.UsageCount--;
            if (sc.UsageCount == 0)
            {
                SecurityCell prev = _hive.GetCell<SecurityCell>(sc.PreviousIndex);
                prev.NextIndex = sc.NextIndex;
                _hive.UpdateCell(prev, false);

                SecurityCell next = _hive.GetCell<SecurityCell>(sc.NextIndex);
                next.PreviousIndex = sc.PreviousIndex;
                _hive.UpdateCell(next, false);

                _hive.FreeCell(cellIndex);
            }
            else
            {
                _hive.UpdateCell(sc, false);
            }
        }

        private void FreeValues(KeyNodeCell cell)
        {
            if (cell.NumValues != 0 && cell.ValueListIndex != -1)
            {
                byte[] valueList = _hive.RawCellData(cell.ValueListIndex, cell.NumValues * 4);

                for (int i = 0; i < cell.NumValues; ++i)
                {
                    int valueIndex = EndianUtilities.ToInt32LittleEndian(valueList, i * 4);
                    _hive.FreeCell(valueIndex);
                }

                _hive.FreeCell(cell.ValueListIndex);
                cell.ValueListIndex = -1;
                cell.NumValues = 0;
                cell.MaxValDataBytes = 0;
                cell.MaxValNameBytes = 0;
            }
        }

        private void FreeSubKeys(KeyNodeCell subkeyCell)
        {
            if (subkeyCell.SubKeysIndex == -1)
            {
                throw new InvalidOperationException("No subkey list");
            }

            Cell list = _hive.GetCell<Cell>(subkeyCell.SubKeysIndex);

            SubKeyIndirectListCell indirectList = list as SubKeyIndirectListCell;
            if (indirectList != null)
            {
                ////foreach (int listIndex in indirectList.CellIndexes)
                for (int i = 0; i < indirectList.CellIndexes.Count; ++i)
                {
                    int listIndex = indirectList.CellIndexes[i];
                    _hive.FreeCell(listIndex);
                }
            }

            _hive.FreeCell(list.Index);
        }
    }
}
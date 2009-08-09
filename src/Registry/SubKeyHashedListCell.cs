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
using System.Globalization;

namespace DiscUtils.Registry
{
    internal sealed class SubKeyHashedListCell : Cell
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

        public IEnumerable<int> SubKeys
        {
            get { return _subKeyIndexes; }
        }

        public override void ReadFrom(byte[] buffer, int offset)
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

        public int Count
        {
            get { return _numElements; }
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
                if (string.CompareOrdinal(cell.Name, name) > 0)
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

        /// <summary>
        /// Tries to match a subkey cell, returning it's absolute offset.
        /// </summary>
        /// <param name="name">The name of the subkey</param>
        /// <returns></returns>
        internal int FindCell(string name)
        {
            int index = IndexOf(name);
            if (index >= 0)
            {
                return _subKeyIndexes[index];
            }
            return -1;
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

        internal int IndexToCellIndex(int index)
        {
            return _subKeyIndexes[index];
        }

        internal uint CalcHash(string name)
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
                string hashStr = name.Substring(0, 4) + "\0\0\0\0";
                for (int i = 0; i < 4; ++i)
                {
                    hash |= (uint)((hashStr[i] & 0xFF) << (i * 8));
                }
            }
            return hash;
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
    }

}

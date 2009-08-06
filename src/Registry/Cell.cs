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
using System.IO;
using System.Security.AccessControl;

namespace DiscUtils.Registry
{
    /// <summary>
    /// The per-key flags present on registry keys.
    /// </summary>
    [Flags]
    public enum RegistryKeyFlags : int
    {
        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0001 = 0x0001,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0002 = 0x0002,

        /// <summary>
        /// The key is the root key in the registry hive.
        /// </summary>
        Root = 0x0004,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0008 = 0x0008,

        /// <summary>
        /// The key is a link to another key.
        /// </summary>
        Link = 0x0010,

        /// <summary>
        /// This is a normal key.
        /// </summary>
        Normal = 0x0020,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0040 = 0x0040,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0080 = 0x0080,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0100 = 0x0100,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0200 = 0x0200,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0400 = 0x0400,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown0800 = 0x0800,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown1000 = 0x1000,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown2000 = 0x2000,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown4000 = 0x4000,

        /// <summary>
        /// Unknown purpose.
        /// </summary>
        Unknown8000 = 0x8000
    }

    /// <summary>
    /// The types of registry values.
    /// </summary>
    public enum RegistryValueType : int
    {
        /// <summary>
        /// Unknown type.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Unicode string.
        /// </summary>
        String = 0x01,

        /// <summary>
        /// String containing environment variables.
        /// </summary>
        ExpandString = 0x02,

        /// <summary>
        /// Binary data.
        /// </summary>
        Binary = 0x03,

        /// <summary>
        /// 32-bit integer.
        /// </summary>
        Dword = 0x04,

        /// <summary>
        /// 32-bit integer.
        /// </summary>
        DwordBigEndian = 0x05,

        /// <summary>
        /// Link.
        /// </summary>
        Link = 0x06,

        /// <summary>
        /// A multistring.
        /// </summary>
        MultiString = 0x07,

        /// <summary>
        /// An unknown binary format.
        /// </summary>
        ResourceList = 0x08,

        /// <summary>
        /// An unknown binary format.
        /// </summary>
        FullResourceDescriptor = 0x09,

        /// <summary>
        /// An unknown binary format.
        /// </summary>
        ResourceRequirementsList = 0x0A,

        /// <summary>
        /// A 64-bit integer.
        /// </summary>
        QWord = 0x0B,
    }

    [Flags]
    internal enum ValueFlags : ushort
    {
        Named = 0x0001,
        Unknown0002 = 0x0002,
        Unknown0004 = 0x0004,
        Unknown0008 = 0x0008,
        Unknown0010 = 0x0010,
        Unknown0020 = 0x0020,
        Unknown0040 = 0x0040,
        Unknown0080 = 0x0080,
        Unknown0100 = 0x0100,
        Unknown0200 = 0x0200,
        Unknown0400 = 0x0400,
        Unknown0800 = 0x0800,
        Unknown1000 = 0x1000,
        Unknown2000 = 0x2000,
        Unknown4000 = 0x4000,
        Unknown8000 = 0x8000
    }


    internal abstract class Cell : IByteArraySerializable
    {
        public Cell()
        {
        }

        internal static Cell Parse(byte[] buffer, int pos)
        {
            string type = Utilities.BytesToString(buffer, pos, 2);

            Cell result = null;

            switch(type)
            {
                case "nk":
                    result = new KeyNodeCell();
                    break;

                case "sk":
                    result = new SecurityCell();
                    break;

                case "vk":
                    result = new ValueCell();
                    break;

                case "lh":
                case "lf":
                    result = new SubKeyHashedListCell();
                    break;

                case "ri":
                    result = new SubKeyIndirectListCell();
                    break;

                default:
                    Console.WriteLine("Unknown cell type: {0:X2} {1:X2}", buffer[pos], buffer[pos + 1]);
                    return null;
                    //throw new NotImplementedException("Unknown cell type '" + type + "'");
            }

            result.ReadFrom(buffer, pos);
            return result;
        }

        #region IByteArraySerializable Members

        public abstract void ReadFrom(byte[] buffer, int offset);
        public abstract void WriteTo(byte[] buffer, int offset);
        public abstract int Size
        {
            get;
        }

        #endregion
    }

    internal class KeyNodeCell : Cell
    {
        private RegistryKeyFlags _flags;
        private DateTime _timestamp;
        private int _parentIndex;
        private int _numSubKeys;
        private int _subKeysIndex;
        private int _numValues;
        private int _valueListIndex;
        private int _securityIndex;
        private int _classNameIndex;

        /// <summary>
        /// Number of bytes to represent largest subkey name in Unicode - no null terminator
        /// </summary>
        private int _maxSubKeyNameBytes;

        /// <summary>
        /// Number of bytes to represent largest value name in Unicode - no null terminator
        /// </summary>
        private int _maxValNameBytes;

        /// <summary>
        /// Number of bytes to represent largest value content (strings in Unicode, with null terminator - if stored)
        /// </summary>
        private int _maxValDataBytes;

        private int _indexInParent;
        private int _classNameLength;
        private string _name;

        public RegistryKeyFlags Flags
        {
            get { return _flags; }
        }

        public DateTime Timestamp
        {
            get { return _timestamp; }
        }

        public int ParentIndex
        {
            get { return _parentIndex; }
        }

        public int NumSubKeys
        {
            get { return _numSubKeys; }
        }

        public int SubKeysIndex
        {
            get { return _subKeysIndex; }
        }

        public int NumValues
        {
            get { return _numValues; }
            set { _numValues = value; }
        }

        public int ValueListIndex
        {
            get { return _valueListIndex; }
        }

        public int SecurityIndex
        {
            get { return _securityIndex; }
        }

        public int ClassNameIndex
        {
            get { return _classNameIndex; }
        }

        public int IndexInParent
        {
            get { return _indexInParent; }
        }

        public int ClassNameLength
        {
            get { return _classNameLength; }
        }

        public string Name
        {
            get { return _name; }
        }

        public override void ReadFrom(byte[] buffer, int offset)
        {
            _flags = (RegistryKeyFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            _timestamp = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x04));
            _parentIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x10);
            _numSubKeys = Utilities.ToInt32LittleEndian(buffer, offset + 0x14);
            _subKeysIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x1C);
            _numValues = Utilities.ToInt32LittleEndian(buffer, offset + 0x24);
            _valueListIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x28);
            _securityIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x2C);
            _classNameIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x30);
            _maxSubKeyNameBytes = Utilities.ToInt32LittleEndian(buffer, offset + 0x34);
            _maxValNameBytes = Utilities.ToInt32LittleEndian(buffer, offset + 0x3C);
            _maxValDataBytes = Utilities.ToInt32LittleEndian(buffer, offset + 0x40);
            _indexInParent = Utilities.ToInt32LittleEndian(buffer, offset + 0x44);
            int nameLength = Utilities.ToInt16LittleEndian(buffer, offset + 0x48);
            _classNameLength = Utilities.ToInt16LittleEndian(buffer, offset + 0x4A);
            _name = Utilities.BytesToString(buffer, offset + 0x4C, nameLength);

            //uint unknown0 = Utilities.ToUInt32LittleEndian(buffer, offset + 0x0C);

            //// 
            //uint unknown1 = Utilities.ToUInt32LittleEndian(buffer, offset + 0x18);

            //uint unknown2 = Utilities.ToUInt32LittleEndian(buffer, offset + 0x20);
            //uint unknown4 = Utilities.ToUInt32LittleEndian(buffer, offset + 0x38);

            //if (unknown0 != 0 || unknown1 != 0 || unknown4 != 0 || unknown2 != 0xffffffff)
            //{
            //    throw new InvalidDataException("Interesting value");
            //}
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            Utilities.StringToBytes("nk", buffer, offset, 2);
            Utilities.WriteBytesLittleEndian((ushort)_flags, buffer, offset + 0x02);
            Utilities.WriteBytesLittleEndian(_timestamp.ToFileTimeUtc(), buffer, offset + 0x04);
            Utilities.WriteBytesLittleEndian(_parentIndex, buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian(_numSubKeys, buffer, offset + 0x14);
            Utilities.WriteBytesLittleEndian(_subKeysIndex, buffer, offset + 0x1C);
            Utilities.WriteBytesLittleEndian(_numValues, buffer, offset + 0x24);
            Utilities.WriteBytesLittleEndian(_valueListIndex, buffer, offset + 0x28);
            Utilities.WriteBytesLittleEndian(_securityIndex, buffer, offset + 0x2C);
            Utilities.WriteBytesLittleEndian(_classNameIndex, buffer, offset + 0x30);
            Utilities.WriteBytesLittleEndian(_indexInParent, buffer, offset + 0x44);
            Utilities.WriteBytesLittleEndian((ushort)_name.Length, buffer, offset + 0x48);
            Utilities.WriteBytesLittleEndian(_classNameLength, buffer, offset + 0x4A);
            Utilities.StringToBytes(_name, buffer, offset + 0x4C, _name.Length);
        }

        public override int Size
        {
            get { return 0x4C + _name.Length; }
        }

        public override string ToString()
        {
            return "Key:" + _name + "[" + _flags + "] <" + _timestamp + ">";
        }
    }

    internal class SubKeyIndirectListCell : Cell
    {
        private int _numElements;
        private int[] _listIndexes;

        public IEnumerable<int> Lists
        {
            get { return _listIndexes; }
        }

        public override void ReadFrom(byte[] buffer, int offset)
        {
            _numElements = Utilities.ToInt16LittleEndian(buffer, offset + 2);

            _listIndexes = new int[_numElements];
            for (int i = 0; i < _numElements; ++i)
            {
                _listIndexes[i] = Utilities.ToInt32LittleEndian(buffer, offset + 0x4 + (i * 0x4));
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
    }

    internal class SubKeyHashedListCell : Cell
    {
        private string _hashType;
        private int _numElements;
        private int[] _subKeyIndexes;
        private uint[] _nameHashes;

        public IEnumerable<int> SubKeys
        {
            get { return _subKeyIndexes; }
        }

        public override void ReadFrom(byte[] buffer, int offset)
        {
            _hashType = Utilities.BytesToString(buffer, offset, 2);
            _numElements = Utilities.ToInt16LittleEndian(buffer, offset + 2);

            _subKeyIndexes = new int[_numElements];
            _nameHashes = new uint[_numElements];
            for (int i = 0; i < _numElements; ++i)
            {
                _subKeyIndexes[i] = Utilities.ToInt32LittleEndian(buffer, offset + 0x4 + (i * 0x8));
                _nameHashes[i] = Utilities.ToUInt32LittleEndian(buffer, offset + 0x4 + (i * 0x8) + 0x4);
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

        internal IEnumerable<int> Find(string name, int start)
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
            uint hash = 0;
            for (int i = 0; i < name.Length; ++i)
            {
                hash *= 37;
                hash += char.ToUpper(name[i], CultureInfo.InvariantCulture);
            }

            for (int i = start; i < _nameHashes.Length; ++i)
            {
                if (_nameHashes[i] == hash)
                {
                    yield return _subKeyIndexes[i];
                }
            }
        }

        private IEnumerable<int> FindByPrefix(string name, int start)
        {
            int compChars = Math.Min(name.Length, 4);
            string compStr = name.Substring(0, compChars).ToUpperInvariant() + "\0\0\0\0";

            for (int i = start; i < _nameHashes.Length; ++i)
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
                    yield return _subKeyIndexes[i];
                }
            }
        }
    }

    internal class SecurityCell : Cell
    {
        private int _prevIndex;
        private int _nextIndex;
        private int _usageCount;
        private RegistrySecurity _secDesc;

        public int PreviousIndex
        {
            get { return _prevIndex; }
        }

        public int NextIndex
        {
            get { return _nextIndex; }
        }

        public int UsageCount
        {
            get { return _usageCount; }
        }

        public RegistrySecurity SecurityDescriptor
        {
            get { return _secDesc; }
        }

        public override void ReadFrom(byte[] buffer, int offset)
        {
            _prevIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x04);
            _nextIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x08);
            _usageCount = Utilities.ToInt32LittleEndian(buffer, offset + 0x0C);
            int secDescSize = Utilities.ToInt32LittleEndian(buffer, offset + 0x10);

            byte[] secDesc = new byte[secDescSize];
            Array.Copy(buffer, offset + 0x14, secDesc, 0, secDescSize);
            _secDesc = new RegistrySecurity();
            _secDesc.SetSecurityDescriptorBinaryForm(secDesc);
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override int Size
        {
            get { throw new NotImplementedException(); }
        }

        public override string ToString()
        {
            return "SecDesc:" + _secDesc.GetSecurityDescriptorSddlForm(AccessControlSections.All) + " (refCount:" + _usageCount + ")";
        }
    }

    internal class ValueCell : Cell
    {
        private int _dataLength;
        private int _dataIndex;
        private RegistryValueType _type;
        private ValueFlags _flags;
        private string _name;

        public int DataLength
        {
            get { return _dataLength; }
        }

        public int DataIndex
        {
            get { return _dataIndex; }
        }

        public RegistryValueType DataType
        {
            get { return _type; }
        }

        public string Name
        {
            get { return _name; }
        }

        public override void ReadFrom(byte[] buffer, int offset)
        {
            int nameLen = Utilities.ToUInt16LittleEndian(buffer, offset + 0x02);
            _dataLength = Utilities.ToInt32LittleEndian(buffer, offset + 0x04);
            _dataIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x08);
            _type = (RegistryValueType)Utilities.ToInt32LittleEndian(buffer, offset + 0x0C);
            _flags = (ValueFlags)Utilities.ToUInt16LittleEndian(buffer, offset + 0x10);

            if ((_flags & ValueFlags.Named) != 0)
            {
                _name = Utilities.BytesToString(buffer, offset + 0x14, nameLen).Trim('\0');
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
    }
}

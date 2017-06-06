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
using System.Globalization;
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Registry
{
    /// <summary>
    /// A registry value.
    /// </summary>
    internal sealed class RegistryValue
    {
        private readonly ValueCell _cell;
        private readonly RegistryHive _hive;

        internal RegistryValue(RegistryHive hive, ValueCell cell)
        {
            _hive = hive;
            _cell = cell;
        }

        /// <summary>
        /// Gets the type of the value.
        /// </summary>
        public RegistryValueType DataType
        {
            get { return _cell.DataType; }
        }

        /// <summary>
        /// Gets the name of the value, or empty string if unnamed.
        /// </summary>
        public string Name
        {
            get { return _cell.Name ?? string.Empty; }
        }

        /// <summary>
        /// Gets the value data mapped to a .net object.
        /// </summary>
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
        public object Value
        {
            get { return ConvertToObject(GetData(), DataType); }
        }

        /// <summary>
        /// The raw value data as a byte array.
        /// </summary>
        /// <returns>The value as a raw byte array.</returns>
        public byte[] GetData()
        {
            if (_cell.DataLength < 0)
            {
                int len = _cell.DataLength & 0x7FFFFFFF;
                byte[] buffer = new byte[4];
                EndianUtilities.WriteBytesLittleEndian(_cell.DataIndex, buffer, 0);

                byte[] result = new byte[len];
                Array.Copy(buffer, result, len);
                return result;
            }

            return _hive.RawCellData(_cell.DataIndex, _cell.DataLength);
        }

        /// <summary>
        /// Sets the value as raw bytes, with no validation that enough data is specified for the given value type.
        /// </summary>
        /// <param name="data">The data to store.</param>
        /// <param name="offset">The offset within <c>data</c> of the first byte to store.</param>
        /// <param name="count">The number of bytes to store.</param>
        /// <param name="valueType">The type of the data.</param>
        public void SetData(byte[] data, int offset, int count, RegistryValueType valueType)
        {
            // If we can place the data in the DataIndex field, do that to save space / allocation
            if ((valueType == RegistryValueType.Dword || valueType == RegistryValueType.DwordBigEndian) && count <= 4)
            {
                if (_cell.DataLength >= 0)
                {
                    _hive.FreeCell(_cell.DataIndex);
                }

                _cell.DataLength = (int)((uint)count | 0x80000000);
                _cell.DataIndex = EndianUtilities.ToInt32LittleEndian(data, offset);
                _cell.DataType = valueType;
            }
            else
            {
                if (_cell.DataIndex == -1 || _cell.DataLength < 0)
                {
                    _cell.DataIndex = _hive.AllocateRawCell(count);
                }

                if (!_hive.WriteRawCellData(_cell.DataIndex, data, offset, count))
                {
                    int newDataIndex = _hive.AllocateRawCell(count);
                    _hive.WriteRawCellData(newDataIndex, data, offset, count);
                    _hive.FreeCell(_cell.DataIndex);
                    _cell.DataIndex = newDataIndex;
                }

                _cell.DataLength = count;
                _cell.DataType = valueType;
            }

            _hive.UpdateCell(_cell, false);
        }

        /// <summary>
        /// Sets the value stored.
        /// </summary>
        /// <param name="value">The value to store.</param>
        /// <param name="valueType">The registry type of the data.</param>
        public void SetValue(object value, RegistryValueType valueType)
        {
            if (valueType == RegistryValueType.None)
            {
                if (value is int)
                {
                    valueType = RegistryValueType.Dword;
                }
                else if (value is byte[])
                {
                    valueType = RegistryValueType.Binary;
                }
                else if (value is string[])
                {
                    valueType = RegistryValueType.MultiString;
                }
                else
                {
                    valueType = RegistryValueType.String;
                }
            }

            byte[] data = ConvertToData(value, valueType);
            SetData(data, 0, data.Length, valueType);
        }

        /// <summary>
        /// Gets a string representation of the registry value.
        /// </summary>
        /// <returns>The registry value as a string.</returns>
        public override string ToString()
        {
            return Name + ":" + DataType + ":" + DataAsString();
        }

        private static object ConvertToObject(byte[] data, RegistryValueType type)
        {
            switch (type)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                case RegistryValueType.Link:
                    return Encoding.Unicode.GetString(data).Trim('\0');

                case RegistryValueType.Dword:
                    return EndianUtilities.ToInt32LittleEndian(data, 0);

                case RegistryValueType.DwordBigEndian:
                    return EndianUtilities.ToInt32BigEndian(data, 0);

                case RegistryValueType.MultiString:
                    string multiString = Encoding.Unicode.GetString(data).Trim('\0');
                    return multiString.Split('\0');

                case RegistryValueType.QWord:
                    return string.Empty + EndianUtilities.ToUInt64LittleEndian(data, 0);

                default:
                    return data;
            }
        }

        private static byte[] ConvertToData(object value, RegistryValueType valueType)
        {
            if (valueType == RegistryValueType.None)
            {
                throw new ArgumentException("Specific registry value type must be specified", nameof(valueType));
            }

            byte[] data;
            switch (valueType)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                    string strValue = value.ToString();
                    data = new byte[strValue.Length * 2 + 2];
                    Encoding.Unicode.GetBytes(strValue, 0, strValue.Length, data, 0);
                    break;

                case RegistryValueType.Dword:
                    data = new byte[4];
                    EndianUtilities.WriteBytesLittleEndian((int)value, data, 0);
                    break;

                case RegistryValueType.DwordBigEndian:
                    data = new byte[4];
                    EndianUtilities.WriteBytesBigEndian((int)value, data, 0);
                    break;

                case RegistryValueType.MultiString:
                    string multiStrValue = string.Join("\0", (string[])value) + "\0";
                    data = new byte[multiStrValue.Length * 2 + 2];
                    Encoding.Unicode.GetBytes(multiStrValue, 0, multiStrValue.Length, data, 0);
                    break;

                default:
                    data = (byte[])value;
                    break;
            }

            return data;
        }

        private string DataAsString()
        {
            switch (DataType)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                case RegistryValueType.Link:
                case RegistryValueType.Dword:
                case RegistryValueType.DwordBigEndian:
                case RegistryValueType.QWord:
                    return ConvertToObject(GetData(), DataType).ToString();

                case RegistryValueType.MultiString:
                    return string.Join(",", (string[])ConvertToObject(GetData(), DataType));

                default:
                    byte[] data = GetData();
                    string result = string.Empty;
                    for (int i = 0; i < Math.Min(data.Length, 8); ++i)
                    {
                        result += string.Format(CultureInfo.InvariantCulture, "{0:X2} ", (int)data[i]);
                    }

                    return result + string.Format(CultureInfo.InvariantCulture, " ({0} bytes)", data.Length);
            }
        }
    }
}
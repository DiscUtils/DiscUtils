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
using System.Globalization;
using System.Text;

namespace DiscUtils.Registry
{
    /// <summary>
    /// A registry value.
    /// </summary>
    public class RegistryValue
    {
        private RegistryHive _hive;
        private ValueCell _cell;

        internal RegistryValue(RegistryHive hive, ValueCell cell)
        {
            _hive = hive;
            _cell = cell;
        }

        /// <summary>
        /// The name of the value, or empty string if unnamed.
        /// </summary>
        public string Name
        {
            get { return _cell.Name ?? ""; }
        }

        /// <summary>
        /// The type of the value.
        /// </summary>
        public RegistryValueType Type
        {
            get { return _cell.Type; }
        }

        /// <summary>
        /// The raw value data as a byte array.
        /// </summary>
        public byte[] Data
        {
            get
            {
                if (_cell.DataLength < 0)
                {
                    int len = _cell.DataLength & 0x7FFFFFFF;
                    byte[] buffer = new byte[4];
                    Utilities.WriteBytesLittleEndian(_cell.DataIndex, buffer, 0);

                    byte[] result = new byte[len];
                    Array.Copy(buffer, result, len);
                    return result;
                }
                return _hive.RawCellData(_cell.DataIndex, _cell.DataLength);
            }
        }

        /// <summary>
        /// The value data mapped to a .net object.
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
            get
            {
                return ConvertToObject(Data, Type);
            }
        }

        /// <summary>
        /// Gets a string representation of the registry value.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name + ":" + Type + ":" + DataAsString();
        }

        private string DataAsString()
        {
            switch (Type)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                case RegistryValueType.Link:
                case RegistryValueType.Dword:
                case RegistryValueType.DwordBigEndian:
                case RegistryValueType.QWord:
                    return ConvertToObject(Data, Type).ToString();

                case RegistryValueType.MultiString:
                    return string.Join(",", (string[])ConvertToObject(Data, Type));

                default:
                    byte[] data = Data;
                    string result = "";
                    for (int i = 0; i < Math.Min(data.Length, 8); ++i)
                    {
                        result += string.Format(CultureInfo.InvariantCulture, "{0:X2} ", (int)data[i]);
                    }
                    return result + string.Format(CultureInfo.InvariantCulture, " ({0} bytes)", data.Length);
            }
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
                    return Utilities.ToUInt32LittleEndian(data, 0);

                case RegistryValueType.DwordBigEndian:
                    return Utilities.ToUInt32BigEndian(data, 0);

                case RegistryValueType.MultiString:
                    string multiString = Encoding.Unicode.GetString(data).Trim('\0');
                    return multiString.Split('\0');

                case RegistryValueType.QWord:
                    return "" + Utilities.ToUInt64LittleEndian(data, 0);

                default:
                    return data;
            }
        }
    }
}

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
using System.Globalization;
using System.Collections.Generic;

namespace DiscUtils.BootConfig
{
    /// <summary>
    /// The value of an element.
    /// </summary>
    public abstract class ElementValue
    {
        /// <summary>
        /// Gets the format of the value.
        /// </summary>
        public abstract ElementFormat Format { get; }

        /// <summary>
        /// Gets the parent object (only for Device values).
        /// </summary>
        public virtual Guid ParentObject { get { return Guid.Empty; } }

        /// <summary>
        /// Gets a value representing a device (aka partition).
        /// </summary>
        /// <param name="parentObject">Object containing detailed information about the device.</param>
        /// <param name="physicalVolume">The volume to represent</param>
        /// <returns>The value as an object</returns>
        public static ElementValue ForDevice(Guid parentObject, PhysicalVolumeInfo physicalVolume)
        {
            return new DeviceElementValue(parentObject, physicalVolume);
        }

        /// <summary>
        /// Gets a value representing the logical boot device.
        /// </summary>
        /// <returns>The boot pseudo-device as an object</returns>
        public static ElementValue ForBootDevice()
        {
            return new DeviceElementValue();
        }

        /// <summary>
        /// Gets a value representing a string value.
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The value as an object</returns>
        public static ElementValue ForString(string value)
        {
            return new StringElementValue(value);
        }

        /// <summary>
        /// Gets a value representing an integer value.
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The value as an object</returns>
        public static ElementValue ForInteger(long value)
        {
            return new IntegerElementValue((ulong)value);
        }

        /// <summary>
        /// Gets a value representing an integer list value.
        /// </summary>
        /// <param name="values">The value</param>
        /// <returns>The value as an object</returns>
        public static ElementValue ForIntegerList(long[] values)
        {
            ulong[] ulValues = new ulong[values.Length];
            for (int i = 0; i < values.Length; ++i)
            {
                ulValues[i] = (ulong)values[i];
            }
            return new IntegerListElementValue(ulValues);
        }

        /// <summary>
        /// Gets a value representing a boolean value.
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The value as an object</returns>
        public static ElementValue ForBoolean(bool value)
        {
            return new BooleanElementValue(value);
        }

        /// <summary>
        /// Gets a value representing a GUID value.
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The value as an object</returns>
        public static ElementValue ForGuid(Guid value)
        {
            return new GuidElementValue(value.ToString("B"));
        }

        /// <summary>
        /// Gets a value representing a GUID list value.
        /// </summary>
        /// <param name="values">The value</param>
        /// <returns>The value as an object</returns>
        public static ElementValue ForGuidList(Guid[] values)
        {
            string[] strValues = new string[values.Length];
            for (int i = 0; i < values.Length; ++i)
            {
                strValues[i] = values[i].ToString("B");
            }
            return new GuidListElementValue(strValues);
        }
    }

    internal class StringElementValue : ElementValue
    {
        private string _value;

        public StringElementValue(string value)
        {
            _value = value;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.String; }
        }

        public override string ToString()
        {
            return _value;
        }
    }

    internal class IntegerElementValue : ElementValue
    {
        private ulong _value;

        public IntegerElementValue(byte[] value)
        {
            // Actual bytes stored may be less than 8
            byte[] buffer = new byte[8];
            Array.Copy(value, buffer, value.Length);

            _value = Utilities.ToUInt64LittleEndian(buffer, 0);
        }

        public IntegerElementValue(ulong value)
        {
            _value = value;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.Integer; }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}", _value);
        }

        internal byte[] GetBytes()
        {
            byte[] bytes = new byte[8];
            Utilities.WriteBytesLittleEndian(_value, bytes, 0);
            return bytes;
        }
    }

    internal class IntegerListElementValue : ElementValue
    {
        private ulong[] _values;

        public IntegerListElementValue(byte[] value)
        {
            _values = new ulong[value.Length / 8];
            for (int i = 0; i < _values.Length; ++i)
            {
                _values[i] = Utilities.ToUInt64LittleEndian(value, i * 8);
            }
        }

        public IntegerListElementValue(ulong[] values)
        {
            _values = values;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.IntegerList; }
        }

        public override string ToString()
        {
            if (_values == null || _values.Length == 0)
            {
                return "<none>";
            }

            string result = "";
            for (int i = 0; i < _values.Length; ++i)
            {
                if (i != 0)
                {
                    result += " ";
                }

                result += _values[i].ToString("X16", CultureInfo.InvariantCulture);
            }

            return result;
        }

        internal byte[] GetBytes()
        {
            byte[] bytes = new byte[_values.Length * 8];
            for (int i = 0; i < _values.Length; ++i)
            {
                Utilities.WriteBytesLittleEndian(_values[i], bytes, i * 8);
            }
            return bytes;
        }
    }

    internal class BooleanElementValue : ElementValue
    {
        private bool _value;

        public BooleanElementValue(byte[] value)
        {
            _value = value[0] != 0;
        }

        public BooleanElementValue(bool value)
        {
            _value = value;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.Boolean; }
        }

        public override string ToString()
        {
            return _value ? "True" : "False";
        }

        internal byte[] GetBytes()
        {
            return new byte[] { (_value ? (byte)1 : (byte)0) };
        }
    }

    internal class GuidElementValue : ElementValue
    {
        private string _value;

        public GuidElementValue(string value)
        {
            _value = value;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.Guid; }
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(_value))
            {
                return "<none>";
            }

            return _value;
        }
    }

    internal class GuidListElementValue : ElementValue
    {
        private string[] _values;

        public GuidListElementValue(string[] values)
        {
            _values = values;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.GuidList; }
        }

        public override string ToString()
        {
            if (_values == null || _values.Length == 0)
            {
                return "<none>";
            }

            string result = _values[0];
            for (int i = 1; i < _values.Length; ++i)
            {
                result += "," + _values[i];
            }

            return result;
        }

        internal string[] GetGuidStrings()
        {
            return _values;
        }
    }

    internal class DeviceElementValue : ElementValue
    {
        private Guid _parentObject;
        private DeviceRecord _record;

        public DeviceElementValue()
        {
            _parentObject = Guid.Empty;

            PartitionRecord record = new PartitionRecord();
            record.Type = 5;
            _record = record;
        }

        public DeviceElementValue(Guid parentObject, PhysicalVolumeInfo pvi)
        {
            _parentObject = parentObject;

            PartitionRecord record = new PartitionRecord();
            record.Type = 6;
            if (pvi.VolumeType == PhysicalVolumeType.BiosPartition)
            {
                record.PartitionType = 1;
                record.DiskIdentity = new byte[4];
                Utilities.WriteBytesLittleEndian(pvi.DiskSignature, record.DiskIdentity, 0);
                record.PartitionIdentity = new byte[8];
                Utilities.WriteBytesLittleEndian(pvi.PhysicalStartSector * 512, record.PartitionIdentity, 0);
            }
            else if (pvi.VolumeType == PhysicalVolumeType.GptPartition)
            {
                record.PartitionType = 0;
                record.DiskIdentity = new byte[16];
                Utilities.WriteBytesLittleEndian(pvi.DiskIdentity, record.DiskIdentity, 0);
                record.PartitionIdentity = new byte[16];
                Utilities.WriteBytesLittleEndian(pvi.PartitionIdentity, record.PartitionIdentity, 0);
            }
            else
            {
                throw new NotImplementedException(string.Format(CultureInfo.InvariantCulture, "Unknown how to convert volume type {0} to a Device element", pvi.VolumeType));
            }
            _record = record;
        }

        public DeviceElementValue(byte[] value)
        {
            _parentObject = Utilities.ToGuidLittleEndian(value, 0x00);

            // -- Start of data structure --

            _record = DeviceRecord.Parse(value, 0x10);
        }

        internal byte[] GetBytes()
        {
            byte[] buffer = new byte[_record.Size + 0x10];

            Utilities.WriteBytesLittleEndian(_parentObject, buffer, 0);
            _record.GetBytes(buffer, 0x10);

            return buffer;
        }

        public override Guid ParentObject
        {
            get { return _parentObject; }
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.Device; }
        }


        public override string ToString()
        {
            if (_parentObject != Guid.Empty)
            {
                return _parentObject.ToString() + ":" + _record.ToString();
            }
            else if (_record != null)
            {
                return _record.ToString();
            }
            else
            {
                return "<unknown>";
            }
        }
    }
}

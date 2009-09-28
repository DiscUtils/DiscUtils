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
using System.Collections.Generic;

namespace DiscUtils.Bcd
{
    public abstract class ElementValue
    {
        public abstract ElementFormat Format { get; }

        public virtual Guid ParentObject { get { return Guid.Empty; } }

        public static ElementValue FromVolume(Guid parentObject, PhysicalVolumeInfo pvi)
        {
            return new DeviceElementValue(parentObject, pvi);
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

        public override ElementFormat Format
        {
            get { return ElementFormat.Integer; }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}", _value);
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
    }

    internal class BooleanElementValue : ElementValue
    {
        private bool _value;

        public BooleanElementValue(byte[] value)
        {
            _value = value[0] != 0;
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.Boolean; }
        }

        public override string ToString()
        {
            return _value ? "True" : "False";
        }
    }

    public class GuidElementValue : ElementValue
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

    public class GuidListElementValue : ElementValue
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

        public IEnumerable<Guid> Guids
        {
            get
            {
                for (int i = 0; i < _values.Length; ++i)
                {
                    yield return new Guid(_values[i]);
                }
            }
        }
    }

    internal class DeviceElementValue : ElementValue
    {
        private Guid _parentObject;
        private int _deviceType;
        private int _length;
        private int _partitionType;
        private byte[] _diskIdentity;
        private byte[] _partitionIdentity;

        public DeviceElementValue(Guid parentObject, PhysicalVolumeInfo pvi)
        {
            _parentObject = parentObject;
            _deviceType = 6;
            _length = 0x48;
            if (pvi.VolumeType == PhysicalVolumeType.BiosPartition)
            {
                _partitionType = 1;
                _diskIdentity = new byte[4];
                Utilities.WriteBytesLittleEndian(pvi.DiskSignature, _diskIdentity, 0);
                _partitionIdentity = new byte[8];
                Utilities.WriteBytesLittleEndian(pvi.PhysicalStartSector * 512, _partitionIdentity, 0);
            }
            else if (pvi.VolumeType == PhysicalVolumeType.GptPartition)
            {
                _partitionType = 0;
                _diskIdentity = new byte[16];
                Utilities.WriteBytesLittleEndian(pvi.DiskIdentity, _diskIdentity, 0);
                _partitionIdentity = new byte[16];
                Utilities.WriteBytesLittleEndian(pvi.PartitionIdentity, _partitionIdentity, 0);
            }
            else
            {
                throw new NotImplementedException(string.Format(CultureInfo.InvariantCulture, "Unknown how to convert volume type {0} to a Device element", pvi.VolumeType));
            }
        }

        public DeviceElementValue(byte[] value)
        {
            _parentObject = Utilities.ToGuidLittleEndian(value, 0x00);

            // -- Start of data structure --

            _deviceType = Utilities.ToInt32LittleEndian(value, 0x10);
            _length = Utilities.ToInt32LittleEndian(value, 0x18);

            if (_deviceType == 6)
            {
                _partitionType = Utilities.ToInt32LittleEndian(value, 0x34);

                if (_partitionType == 1)
                {
                    // BIOS disk
                    _diskIdentity = new byte[4];
                    Array.Copy(value, 0x38, _diskIdentity, 0, 4);
                    _partitionIdentity = new byte[8];
                    Array.Copy(value, 0x20, _partitionIdentity, 0, 8);
                }
                else if (_partitionType == 0)
                {
                    // GPT disk
                    _diskIdentity = new byte[16];
                    Array.Copy(value, 0x38, _diskIdentity, 0, 16);
                    _partitionIdentity = new byte[16];
                    Array.Copy(value, 0x20, _partitionIdentity, 0, 16);
                }
                else
                {
                    throw new NotImplementedException("Unknown partition type: " + _partitionType);
                }
            }
            else if (_deviceType == 0)
            {
                // Pseudo 'boot' device
            }
            else
            {
                throw new NotImplementedException("Unknown device type: " + _deviceType);
            }
        }

        internal byte[] GetBytes()
        {
            byte[] buffer = new byte[_length + 0x10];
            Utilities.WriteBytesLittleEndian(_parentObject, buffer, 0);

            Utilities.WriteBytesLittleEndian(_deviceType, buffer, 0x10);
            Utilities.WriteBytesLittleEndian(_length, buffer, 0x18);

            if (_deviceType == 6)
            {
                Utilities.WriteBytesLittleEndian(_partitionType, buffer, 0x34);

                if (_partitionType == 1)
                {
                    Array.Copy(_diskIdentity, 0, buffer, 0x38, 4);
                    Array.Copy(_partitionIdentity, 0, buffer, 0x20, 8);
                }
                else if (_partitionType == 0)
                {
                    Array.Copy(_diskIdentity, 0, buffer, 0x38, 16);
                    Array.Copy(_partitionIdentity, 0, buffer, 0x20, 16);
                }
                else
                {
                    throw new NotImplementedException("Unknown partition type: " + _partitionType);
                }
            }
            else if (_deviceType == 0)
            {
                // Pseudo 'boot' device
            }
            else
            {
                throw new NotImplementedException("Unknown device type: " + _deviceType);
            }

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
            if (_deviceType == 0)
            {
                return "<boot device>";
            }

            if (_partitionType == 1)
            {
                return string.Format("(disk:{0:X2}{1:X2}{2:X2}{3:X2} part-offset:{4})", _diskIdentity[0], _diskIdentity[1], _diskIdentity[2], _diskIdentity[3], Utilities.ToUInt64LittleEndian(_partitionIdentity, 0));
            }
            else
            {
                Guid diskGuid = Utilities.ToGuidLittleEndian(_diskIdentity, 0);
                Guid partitionGuid = Utilities.ToGuidLittleEndian(_partitionIdentity, 0);
                return string.Format("(disk:{0} partition:{1})", diskGuid, partitionGuid);
            }
        }
    }

    internal class NullElementValue : ElementValue
    {
        private ElementFormat _format;

        public NullElementValue(ElementFormat format)
        {
            _format = format;
        }

        public override ElementFormat Format
        {
            get { return _format; }
        }

        public override string ToString()
        {
            return "<not implemented>";
        }
    }
}

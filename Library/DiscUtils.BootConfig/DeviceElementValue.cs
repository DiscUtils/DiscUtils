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
using DiscUtils.Streams;

namespace DiscUtils.BootConfig
{
    internal class DeviceElementValue : ElementValue
    {
        private readonly Guid _parentObject;
        private readonly DeviceRecord _record;

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
                EndianUtilities.WriteBytesLittleEndian(pvi.DiskSignature, record.DiskIdentity, 0);
                record.PartitionIdentity = new byte[8];
                EndianUtilities.WriteBytesLittleEndian(pvi.PhysicalStartSector * 512, record.PartitionIdentity, 0);
            }
            else if (pvi.VolumeType == PhysicalVolumeType.GptPartition)
            {
                record.PartitionType = 0;
                record.DiskIdentity = new byte[16];
                EndianUtilities.WriteBytesLittleEndian(pvi.DiskIdentity, record.DiskIdentity, 0);
                record.PartitionIdentity = new byte[16];
                EndianUtilities.WriteBytesLittleEndian(pvi.PartitionIdentity, record.PartitionIdentity, 0);
            }
            else
            {
                throw new NotImplementedException(string.Format(CultureInfo.InvariantCulture,
                    "Unknown how to convert volume type {0} to a Device element", pvi.VolumeType));
            }

            _record = record;
        }

        public DeviceElementValue(byte[] value)
        {
            _parentObject = EndianUtilities.ToGuidLittleEndian(value, 0x00);
            _record = DeviceRecord.Parse(value, 0x10);
        }

        public override ElementFormat Format
        {
            get { return ElementFormat.Device; }
        }

        public override Guid ParentObject
        {
            get { return _parentObject; }
        }

        public override string ToString()
        {
            if (_parentObject != Guid.Empty)
            {
                return _parentObject + ":" + _record;
            }
            if (_record != null)
            {
                return _record.ToString();
            }
            return "<unknown>";
        }

        internal byte[] GetBytes()
        {
            byte[] buffer = new byte[_record.Size + 0x10];

            EndianUtilities.WriteBytesLittleEndian(_parentObject, buffer, 0);
            _record.GetBytes(buffer, 0x10);

            return buffer;
        }
    }
}
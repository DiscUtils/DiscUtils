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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.BootConfig
{
    internal abstract class DeviceRecord
    {
        public int Length { get; set; }

        public abstract int Size { get; }
        public int Type { get; set; }

        public static DeviceRecord Parse(byte[] data, int offset)
        {
            int type = EndianUtilities.ToInt32LittleEndian(data, offset);
            int length = EndianUtilities.ToInt32LittleEndian(data, offset + 0x8);
            if (offset + length > data.Length)
            {
                throw new InvalidDataException("Device record is truncated");
            }

            DeviceRecord newRecord = null;
            switch (type)
            {
                case 0:
                    newRecord = new DeviceAndPathRecord();
                    break;
                case 5: // Logical 'boot' device
                case 6: // Disk partition
                    newRecord = new PartitionRecord();
                    break;
                case 8: // custom:nnnnnn
                    break;
                default:
                    throw new NotImplementedException("Unknown device type: " + type);
            }

            if (newRecord != null)
            {
                newRecord.DoParse(data, offset);
            }

            return newRecord;
        }

        public abstract void GetBytes(byte[] data, int offset);

        protected virtual void DoParse(byte[] data, int offset)
        {
            Type = EndianUtilities.ToInt32LittleEndian(data, offset);
            Length = EndianUtilities.ToInt32LittleEndian(data, offset + 0x8);
        }

        protected void WriteHeader(byte[] data, int offset)
        {
            Length = Size;
            EndianUtilities.WriteBytesLittleEndian(Type, data, offset);
            EndianUtilities.WriteBytesLittleEndian(Size, data, offset + 0x8);
        }
    }
}
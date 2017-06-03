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
    internal class PartitionRecord : DeviceRecord
    {
        public byte[] DiskIdentity { get; set; }

        public byte[] PartitionIdentity { get; set; }
        public int PartitionType { get; set; }

        public override int Size
        {
            get { return 0x48; }
        }

        public override void GetBytes(byte[] data, int offset)
        {
            WriteHeader(data, offset);

            if (Type == 5)
            {
                Array.Clear(data, offset + 0x10, 0x38);
            }
            else if (Type == 6)
            {
                EndianUtilities.WriteBytesLittleEndian(PartitionType, data, offset + 0x24);

                if (PartitionType == 1)
                {
                    Array.Copy(DiskIdentity, 0, data, offset + 0x28, 4);
                    Array.Copy(PartitionIdentity, 0, data, offset + 0x10, 8);
                }
                else if (PartitionType == 0)
                {
                    Array.Copy(DiskIdentity, 0, data, offset + 0x28, 16);
                    Array.Copy(PartitionIdentity, 0, data, offset + 0x10, 16);
                }
                else
                {
                    throw new NotImplementedException("Unknown partition type: " + PartitionType);
                }
            }
            else
            {
                throw new NotImplementedException("Unknown device type: " + Type);
            }
        }

        public override string ToString()
        {
            if (Type == 5)
            {
                return "<boot device>";
            }
            if (Type == 6)
            {
                if (PartitionType == 1)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "(disk:{0:X2}{1:X2}{2:X2}{3:X2} part-offset:{4})",
                        DiskIdentity[0],
                        DiskIdentity[1],
                        DiskIdentity[2],
                        DiskIdentity[3],
                        EndianUtilities.ToUInt64LittleEndian(PartitionIdentity, 0));
                }
                Guid diskGuid = EndianUtilities.ToGuidLittleEndian(DiskIdentity, 0);
                Guid partitionGuid = EndianUtilities.ToGuidLittleEndian(PartitionIdentity, 0);
                return string.Format(CultureInfo.InvariantCulture, "(disk:{0} partition:{1})", diskGuid,
                    partitionGuid);
            }
            if (Type == 8)
            {
                return "custom:<unknown>";
            }
            return "<unknown>";
        }

        protected override void DoParse(byte[] data, int offset)
        {
            base.DoParse(data, offset);

            if (Type == 5)
            {
                // Nothing to do - just empty...
            }
            else if (Type == 6)
            {
                PartitionType = EndianUtilities.ToInt32LittleEndian(data, offset + 0x24);

                if (PartitionType == 1)
                {
                    // BIOS disk
                    DiskIdentity = new byte[4];
                    Array.Copy(data, offset + 0x28, DiskIdentity, 0, 4);
                    PartitionIdentity = new byte[8];
                    Array.Copy(data, offset + 0x10, PartitionIdentity, 0, 8);
                }
                else if (PartitionType == 0)
                {
                    // GPT disk
                    DiskIdentity = new byte[16];
                    Array.Copy(data, offset + 0x28, DiskIdentity, 0, 16);
                    PartitionIdentity = new byte[16];
                    Array.Copy(data, offset + 0x10, PartitionIdentity, 0, 16);
                }
                else
                {
                    throw new NotImplementedException("Unknown partition type: " + PartitionType);
                }
            }
            else
            {
                throw new NotImplementedException("Unknown device type: " + Type);
            }
        }
    }
}
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
using System.IO;

namespace DiscUtils.Sdi
{
    internal class FileHeader
    {
        public string Tag;
        public ulong Type;
        public ulong BootCodeOffset;
        public ulong BootCodeSize;
        public ulong VendorId;
        public ulong DeviceId;
        public Guid DeviceModel;
        public ulong DeviceRole;
        //Reserved ulong
        public Guid RuntimeGuid;
        public ulong RuntimeOEMRev;
        //Reserved ulong
        public long PageAlignment;
        public ulong Checksum;

        public void ReadFrom(byte[] buffer, int offset)
        {
            Tag = Utilities.BytesToString(buffer, offset, 8);

            if (Tag != "$SDI0001")
            {
                throw new InvalidDataException("SDI format marker not found");
            }

            Type = Utilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            BootCodeOffset = Utilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            BootCodeSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            VendorId = Utilities.ToUInt64LittleEndian(buffer, offset + 0x20);
            DeviceId = Utilities.ToUInt64LittleEndian(buffer, offset + 0x28);
            DeviceModel = Utilities.ToGuidLittleEndian(buffer, offset + 0x30);
            DeviceRole = Utilities.ToUInt64LittleEndian(buffer, offset + 0x40);
            RuntimeGuid = Utilities.ToGuidLittleEndian(buffer, offset + 0x50);
            RuntimeOEMRev = Utilities.ToUInt64LittleEndian(buffer, offset + 0x60);
            PageAlignment = Utilities.ToInt64LittleEndian(buffer, offset + 0x70);
            Checksum = Utilities.ToUInt64LittleEndian(buffer, offset + 0x1F8);
        }
    }
}

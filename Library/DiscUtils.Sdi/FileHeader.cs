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

namespace DiscUtils.Sdi
{
    internal class FileHeader
    {
        public ulong BootCodeOffset;
        public ulong BootCodeSize;
        public ulong Checksum;
        public ulong DeviceId;
        public Guid DeviceModel;
        public ulong DeviceRole;
        ////Reserved ulong
        public long PageAlignment;
        ////Reserved ulong
        public Guid RuntimeGuid;
        public ulong RuntimeOEMRev;
        public string Tag;
        public ulong Type;
        public ulong VendorId;

        public void ReadFrom(byte[] buffer, int offset)
        {
            Tag = EndianUtilities.BytesToString(buffer, offset, 8);

            if (Tag != "$SDI0001")
            {
                throw new InvalidDataException("SDI format marker not found");
            }

            Type = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x08);
            BootCodeOffset = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x10);
            BootCodeSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            VendorId = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x20);
            DeviceId = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x28);
            DeviceModel = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x30);
            DeviceRole = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x40);
            RuntimeGuid = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x50);
            RuntimeOEMRev = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x60);
            PageAlignment = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x70);
            Checksum = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x1F8);
        }
    }
}
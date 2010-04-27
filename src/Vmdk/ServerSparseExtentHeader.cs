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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Vmdk
{
    internal class ServerSparseExtentHeader : CommonSparseExtentHeader
    {
        public const uint CowdMagicNumber = 0x44574f43;

        public uint Flags;
        public uint NumGdEntries;
        public uint FreeSector;
        public uint SavedGeneration;
        public uint UncleanShutdown;

        public ServerSparseExtentHeader()
        {
            MagicNumber = CowdMagicNumber;
            Version = 1;
            GrainSize = 512;
            NumGTEsPerGT = 4096;
            Flags = 3;
        }

        public static ServerSparseExtentHeader Read(byte[] buffer, int offset)
        {
            ServerSparseExtentHeader hdr = new ServerSparseExtentHeader();

            hdr.MagicNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 0x00);
            hdr.Version = Utilities.ToUInt32LittleEndian(buffer, offset + 0x04);
            hdr.Flags = Utilities.ToUInt32LittleEndian(buffer, offset + 0x08);
            hdr.Capacity = Utilities.ToUInt32LittleEndian(buffer, offset + 0x0C);
            hdr.GrainSize = Utilities.ToUInt32LittleEndian(buffer, offset + 0x10);
            hdr.GdOffset = Utilities.ToUInt32LittleEndian(buffer, offset + 0x14);
            hdr.NumGdEntries = Utilities.ToUInt32LittleEndian(buffer, offset + 0x18);
            hdr.FreeSector = Utilities.ToUInt32LittleEndian(buffer, offset + 0x1C);

            hdr.SavedGeneration = Utilities.ToUInt32LittleEndian(buffer, offset + 0x660);
            hdr.UncleanShutdown = Utilities.ToUInt32LittleEndian(buffer, offset + 0x66C);

            hdr.NumGTEsPerGT = 4096;

            return hdr;
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Sizes.Sector * 4];
            Utilities.WriteBytesLittleEndian(MagicNumber, buffer, 0x00);
            Utilities.WriteBytesLittleEndian(Version, buffer, 0x04);
            Utilities.WriteBytesLittleEndian(Flags, buffer, 0x08);
            Utilities.WriteBytesLittleEndian((uint)Capacity, buffer, 0x0C);
            Utilities.WriteBytesLittleEndian((uint)GrainSize, buffer, 0x10);
            Utilities.WriteBytesLittleEndian((uint)GdOffset, buffer, 0x14);
            Utilities.WriteBytesLittleEndian(NumGdEntries, buffer, 0x18);
            Utilities.WriteBytesLittleEndian(FreeSector, buffer, 0x1C);

            Utilities.WriteBytesLittleEndian(SavedGeneration, buffer, 0x660);
            Utilities.WriteBytesLittleEndian(UncleanShutdown, buffer, 0x66C);

            return buffer;
        }
    }
}

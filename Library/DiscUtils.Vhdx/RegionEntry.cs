//
// Copyright (c) 2008-2012, Kenneth Bell
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
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class RegionEntry : IByteArraySerializable
    {
        public static readonly Guid BatGuid = new Guid("2dc27766-f623-4200-9d64-115e9bfd4a08");
        public static readonly Guid MetadataRegionGuid = new Guid("8b7ca206-4790-4b9a-b8fe-575f050f886e");
        public long FileOffset;
        public RegionFlags Flags;

        public Guid Guid;
        public uint Length;

        public int Size
        {
            get { return 32; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Guid = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0);
            FileOffset = EndianUtilities.ToInt64LittleEndian(buffer, offset + 16);
            Length = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 24);
            Flags = (RegionFlags)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 28);

            return 32;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(Guid, buffer, offset + 0);
            EndianUtilities.WriteBytesLittleEndian(FileOffset, buffer, offset + 16);
            EndianUtilities.WriteBytesLittleEndian(Length, buffer, offset + 24);
            EndianUtilities.WriteBytesLittleEndian((uint)Flags, buffer, offset + 28);
        }
    }
}
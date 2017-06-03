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
using System.Collections.Generic;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class RegionTable : IByteArraySerializable
    {
        public const uint RegionTableSignature = 0x69676572;
        public const int FixedSize = (int)(64 * Sizes.OneKiB);
        public static readonly Guid BatGuid = new Guid("2DC27766-F623-4200-9D64-115E9BFD4A08");
        public static readonly Guid MetadataRegionGuid = new Guid("8B7CA206-4790-4B9A-B8FE-575F050F886E");

        private readonly byte[] _data = new byte[FixedSize];
        public uint Checksum;
        public uint EntryCount;
        public IDictionary<Guid, RegionEntry> Regions = new Dictionary<Guid, RegionEntry>();
        public uint Reserved;

        public uint Signature = RegionTableSignature;

        public bool IsValid
        {
            get
            {
                if (Signature != RegionTableSignature)
                {
                    return false;
                }

                if (EntryCount > 2047)
                {
                    return false;
                }

                byte[] checkData = new byte[FixedSize];
                Array.Copy(_data, checkData, FixedSize);
                EndianUtilities.WriteBytesLittleEndian((uint)0, checkData, 4);
                return Checksum == Crc32LittleEndian.Compute(Crc32Algorithm.Castagnoli, checkData, 0, FixedSize);
            }
        }

        public int Size
        {
            get { return FixedSize; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Array.Copy(buffer, offset, _data, 0, FixedSize);

            Signature = EndianUtilities.ToUInt32LittleEndian(_data, 0);
            Checksum = EndianUtilities.ToUInt32LittleEndian(_data, 4);
            EntryCount = EndianUtilities.ToUInt32LittleEndian(_data, 8);
            Reserved = EndianUtilities.ToUInt32LittleEndian(_data, 12);
            Regions = new Dictionary<Guid, RegionEntry>();

            if (IsValid)
            {
                for (int i = 0; i < EntryCount; ++i)
                {
                    RegionEntry entry = EndianUtilities.ToStruct<RegionEntry>(_data, 16 + 32 * i);
                    Regions.Add(entry.Guid, entry);
                }
            }

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EntryCount = (uint)Regions.Count;
            Checksum = 0;

            EndianUtilities.WriteBytesLittleEndian(Signature, _data, 0);
            EndianUtilities.WriteBytesLittleEndian(Checksum, _data, 4);
            EndianUtilities.WriteBytesLittleEndian(EntryCount, _data, 8);

            int dataOffset = 16;
            foreach (KeyValuePair<Guid, RegionEntry> region in Regions)
            {
                region.Value.WriteTo(_data, dataOffset);
                dataOffset += 32;
            }

            Checksum = Crc32LittleEndian.Compute(Crc32Algorithm.Castagnoli, _data, 0, FixedSize);
            EndianUtilities.WriteBytesLittleEndian(Checksum, _data, 4);

            Array.Copy(_data, 0, buffer, offset, FixedSize);
        }
    }
}
//
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Lvm
{
    using DiscUtils.Streams;
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class VolumeGroupMetadata : IByteArraySerializable
    {
        public const string VgMetadataMagic = " LVM2 x[5A%r0N*>";
        public const uint VgMetadataVersion = 1;
        public uint Crc;
        public ulong CalculatedCrc;
        public string Magic;
        public uint Version;
        public ulong Start;
        public ulong Length;
        public RawLocation[] RawLocations;
        public string Metadata;
        public Metadata ParsedMetadata;

        /// <inheritdoc />
        public int Size { get { return (int) Length; } }

        /// <inheritdoc />
        public int ReadFrom(byte[] buffer, int offset)
        {
            Crc = EndianUtilities.ToUInt32LittleEndian(buffer, offset);
            CalculatedCrc = PhysicalVolume.CalcCrc(buffer, offset + 0x4, PhysicalVolume.SECTOR_SIZE - 0x4);
            Magic = EndianUtilities.BytesToString(buffer, offset + 0x4, 0x10);
            Version = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x14);
            Start = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x18);
            Length = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x20);

            var locations = new List<RawLocation>();
            var locationOffset = offset + 0x28;
            while (true)
            {
                var location = new RawLocation();
                locationOffset += location.ReadFrom(buffer, locationOffset);
                if (location.Offset == 0 && location.Length == 0 && location.Checksum == 0 && location.Flags == 0) break;
                locations.Add(location);
            }
            RawLocations = locations.ToArray();
            foreach (var location in RawLocations)
            {
                if ((location.Flags & RawLocationFlags.Ignored) != 0)
                    continue;
                var checksum = PhysicalVolume.CalcCrc(buffer, (int) location.Offset, (int) location.Length);
                if (location.Checksum != checksum)
                    throw new IOException("invalid metadata checksum");
                Metadata = EndianUtilities.BytesToString(buffer, (int)location.Offset, (int)location.Length);
                ParsedMetadata = Lvm.Metadata.Parse(Metadata);
                break;
            }
            return Size;
        }

        /// <inheritdoc />
        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

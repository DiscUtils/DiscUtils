// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    // ECMA-167, third edition, parts 3 and 4; UDF 2.01 mastered physical partition.
    // Offsets are relative to each descriptor, including its 16-byte tag.
    internal sealed class UdfImageWriter
    {
        private const int BlockSize = 2048;
        private const ushort Revision = 0x0201;
        private const long MaximumExtentLength = (1L << 30) - BlockSize;
        private readonly string _volume;
        private readonly string _volumeSet;
        private readonly DateTime _time;

        public UdfImageWriter(string volume, string volumeSet, DateTime time)
        {
            _volume = volume;
            _volumeSet = volumeSet;
            _time = time.ToUniversalTime();
        }

        public static byte[] VolumeRecognition()
        {
            var bytes = new byte[3 * BlockSize];
            string[] identifiers = { "BEA01", "NSR03", "TEA01" };
            for (int i = 0; i < identifiers.Length; i++)
            {
                Ascii(bytes, i * BlockSize + 1, identifiers[i]);
                bytes[i * BlockSize + 6] = 1;
            }

            return bytes;
        }

        public byte[] Anchor(uint location)
        {
            var bytes = new byte[512];
            U32(bytes, 16, 16 * BlockSize);
            U32(bytes, 20, 257);
            U32(bytes, 24, 16 * BlockSize);
            U32(bytes, 28, 273);
            return Tag(bytes, 2, location);
        }

        public byte[] PrimaryVolume(uint location)
        {
            var bytes = new byte[512];
            U32(bytes, 16, 1);
            DString(bytes, 24, 32, _volume);
            U16(bytes, 56, 1);
            U16(bytes, 58, 1);
            U16(bytes, 60, 2);
            U16(bytes, 62, 3);
            U32(bytes, 64, 1);
            U32(bytes, 68, 1);
            DString(bytes, 72, 128, _volumeSet);
            Charset(bytes, 200);
            Charset(bytes, 264);
            Entity(bytes, 344, "*DiscUtils");
            Timestamp(bytes, 376);
            Entity(bytes, 388, "*DiscUtils");
            U16(bytes, 488, 1);
            return Tag(bytes, 1, location);
        }

        public byte[] ImplementationVolume(uint location)
        {
            var bytes = new byte[512];
            U32(bytes, 16, 2);
            Entity(bytes, 20, "*UDF LV Info");
            U16(bytes, 44, Revision);
            Charset(bytes, 52);
            DString(bytes, 116, 128, _volume);
            Entity(bytes, 352, "*DiscUtils");
            return Tag(bytes, 4, location);
        }

        public byte[] Partition(uint location, uint start, uint length)
        {
            var bytes = new byte[512];
            U32(bytes, 16, 3);
            U16(bytes, 20, 1);
            Entity(bytes, 24, "+NSR03");
            U32(bytes, 184, 1); // Read-only.
            U32(bytes, 188, start);
            U32(bytes, 192, length);
            Entity(bytes, 196, "*DiscUtils");
            return Tag(bytes, 5, location);
        }

        public byte[] LogicalVolume(uint location)
        {
            var bytes = new byte[446];
            U32(bytes, 16, 4);
            Charset(bytes, 20);
            DString(bytes, 84, 128, _volume);
            U32(bytes, 212, BlockSize);
            Domain(bytes, 216);
            LongAd(bytes, 248, 2 * BlockSize, 0);
            U32(bytes, 264, 6);
            U32(bytes, 268, 1);
            Entity(bytes, 272, "*DiscUtils");
            U32(bytes, 432, 2 * BlockSize);
            U32(bytes, 436, 289);
            bytes[440] = 1; // Type-1 physical partition map.
            bytes[441] = 6;
            U16(bytes, 442, 1); // Volume sequence number.
            return Tag(bytes, 6, location);
        }

        public byte[] UnallocatedSpace(uint location)
        {
            var bytes = new byte[24];
            U32(bytes, 16, 5);
            return Tag(bytes, 7, location);
        }

        public static byte[] Terminator(uint location)
        {
            return Tag(new byte[512], 8, location);
        }

        public byte[] Integrity(uint partitionLength, ulong nextId, uint files, uint directories)
        {
            var bytes = new byte[134];
            Timestamp(bytes, 16);
            U32(bytes, 28, 1); // Closed.
            U64(bytes, 40, nextId);
            U32(bytes, 72, 1);
            U32(bytes, 76, 46);
            U32(bytes, 84, partitionLength);
            Entity(bytes, 88, "*DiscUtils");
            U32(bytes, 120, files);
            U32(bytes, 124, directories);
            U16(bytes, 128, Revision);
            U16(bytes, 130, Revision);
            U16(bytes, 132, Revision);
            return Tag(bytes, 9, 289);
        }

        public byte[] FileSet(uint rootIcb)
        {
            var bytes = new byte[512];
            Timestamp(bytes, 16);
            U16(bytes, 28, 3);
            U16(bytes, 30, 3);
            U32(bytes, 32, 1);
            U32(bytes, 36, 1);
            Charset(bytes, 48);
            DString(bytes, 112, 128, _volume);
            Charset(bytes, 240);
            DString(bytes, 304, 32, _volume);
            LongAd(bytes, 400, BlockSize, rootIcb);
            Domain(bytes, 416);
            return Tag(bytes, 256, 0);
        }

        public byte[] FileEntry(
            uint icb,
            uint dataBlock,
            long length,
            bool directory,
            ushort links,
            ulong uniqueId
        )
        {
            int allocationCount = checked(
                (int)((length + MaximumExtentLength - 1) / MaximumExtentLength)
            );
            var bytes = new byte[176 + allocationCount * 8];
            U16(bytes, 20, 4); // ICB strategy type.
            U16(bytes, 24, 1); // Maximum ICB entries.
            bytes[27] = directory ? (byte)4 : (byte)5;
            U32(bytes, 36, uint.MaxValue);
            U32(bytes, 40, uint.MaxValue);
            U32(bytes, 44, directory ? 0x14a5u : 0x1084u);
            U16(bytes, 48, links);
            U64(bytes, 56, checked((ulong)length));
            U64(bytes, 64, checked((ulong)((length + BlockSize - 1) / BlockSize)));
            Timestamp(bytes, 72);
            Timestamp(bytes, 84);
            Timestamp(bytes, 96);
            U32(bytes, 108, 1);
            Entity(bytes, 128, "*DiscUtils");
            U64(bytes, 160, uniqueId);
            U32(bytes, 172, checked((uint)(allocationCount * 8)));
            long remaining = length;
            for (int i = 0; i < allocationCount; i++)
            {
                uint part = (uint)Math.Min(remaining, MaximumExtentLength);
                U32(bytes, 176 + i * 8, part);
                U32(bytes, 180 + i * 8, dataBlock);
                remaining -= part;
                dataBlock = checked(dataBlock + (part + BlockSize - 1) / BlockSize);
            }

            return Tag(bytes, 261, icb);
        }

        public int FileIdentifier(
            byte[] output,
            int offset,
            uint location,
            string name,
            uint icb,
            ulong uniqueId,
            bool directory,
            bool parent
        )
        {
            byte[] encoded = parent ? new byte[0] : EncodeName(name);
            var bytes = new byte[(38 + encoded.Length + 3) / 4 * 4];
            U16(bytes, 16, 1);
            bytes[18] = (byte)((directory ? 2 : 0) | (parent ? 8 : 0));
            bytes[19] = checked((byte)encoded.Length);
            LongAd(bytes, 20, BlockSize, icb);
            U32(bytes, 32, (uint)uniqueId);
            Array.Copy(encoded, 0, bytes, 38, encoded.Length);
            Tag(bytes, 257, location);
            Array.Copy(bytes, 0, output, offset, bytes.Length);
            return bytes.Length;
        }

        public static byte[] EncodeName(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            bool wide = value.Any(character => character > 255);
            Encoding encoding = wide
                ? new UnicodeEncoding(true, false, true)
                : Encoding.GetEncoding("ISO-8859-1");
            byte[] name = encoding.GetBytes(value);
            if (name.Length > 254)
            {
                throw new ArgumentException(
                    "The UDF name exceeds 255 encoded bytes.",
                    nameof(value)
                );
            }

            var encoded = new byte[name.Length + 1];
            encoded[0] = wide ? (byte)16 : (byte)8;
            Array.Copy(name, 0, encoded, 1, name.Length);
            return encoded;
        }

        public static byte[] EncodeDString(string value, int size)
        {
            var bytes = new byte[size];
            if (string.IsNullOrEmpty(value))
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                return bytes;
            }

            byte[] encoded = EncodeName(value);
            if (encoded.Length >= size)
            {
                throw new ArgumentException(
                    "The identifier exceeds its UDF field length.",
                    nameof(value)
                );
            }

            Array.Copy(encoded, bytes, encoded.Length);
            bytes[size - 1] = checked((byte)encoded.Length);
            return bytes;
        }

        private static byte[] Tag(byte[] bytes, ushort id, uint location)
        {
            U16(bytes, 0, id);
            U16(bytes, 2, 3); // ECMA-167 third edition.
            U16(bytes, 6, 1);
            ushort crc = 0;
            for (int i = 16; i < bytes.Length; i++)
            {
                crc ^= (ushort)(bytes[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
                }
            }

            U16(bytes, 8, crc);
            U16(bytes, 10, checked((ushort)(bytes.Length - 16)));
            U32(bytes, 12, location);
            int checksum = 0;
            for (int i = 0; i < 16; i++)
            {
                if (i != 4)
                {
                    checksum += bytes[i];
                }
            }

            bytes[4] = (byte)checksum;
            return bytes;
        }

        private void Timestamp(byte[] bytes, int offset)
        {
            U16(bytes, offset, 0x1000); // Local-time type with UTC offset zero.
            U16(bytes, offset + 2, checked((ushort)_time.Year));
            bytes[offset + 4] = (byte)_time.Month;
            bytes[offset + 5] = (byte)_time.Day;
            bytes[offset + 6] = (byte)_time.Hour;
            bytes[offset + 7] = (byte)_time.Minute;
            bytes[offset + 8] = (byte)_time.Second;
            bytes[offset + 9] = (byte)(_time.Millisecond / 10);
        }

        private static void Charset(byte[] bytes, int offset)
        {
            Ascii(bytes, offset + 1, "OSTA Compressed Unicode");
        }

        private static void Entity(byte[] bytes, int offset, string id)
        {
            Ascii(bytes, offset + 1, id);
        }

        private static void Domain(byte[] bytes, int offset)
        {
            Entity(bytes, offset, "*OSTA UDF Compliant");
            U16(bytes, offset + 24, Revision);
            bytes[offset + 26] = 3; // Hard and soft write protection.
        }

        private static void LongAd(byte[] bytes, int offset, uint length, uint location)
        {
            U32(bytes, offset, length);
            U32(bytes, offset + 4, location);
        }

        private static void DString(byte[] bytes, int offset, int size, string value)
        {
            Array.Copy(EncodeDString(value, size), 0, bytes, offset, size);
        }

        private static void Ascii(byte[] bytes, int offset, string value)
        {
            Encoding.ASCII.GetBytes(value, 0, value.Length, bytes, offset);
        }

        private static void U16(byte[] bytes, int offset, ushort value)
        {
            EndianUtilities.WriteBytesLittleEndian(value, bytes, offset);
        }

        private static void U32(byte[] bytes, int offset, uint value)
        {
            EndianUtilities.WriteBytesLittleEndian(value, bytes, offset);
        }

        private static void U64(byte[] bytes, int offset, ulong value)
        {
            EndianUtilities.WriteBytesLittleEndian(value, bytes, offset);
        }
    }
}

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
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Vhd
{
    internal class DynamicHeader
    {
        public const string HeaderCookie = "cxsparse";
        public const uint Version1 = 0x00010000;
        public const uint DefaultBlockSize = 0x00200000;
        public uint BlockSize;
        public uint Checksum;

        public string Cookie;
        public long DataOffset;
        public uint HeaderVersion;
        public int MaxTableEntries;
        public ParentLocator[] ParentLocators;
        public DateTime ParentTimestamp;
        public string ParentUnicodeName;
        public Guid ParentUniqueId;
        public long TableOffset;

        public DynamicHeader() {}

        public DynamicHeader(long dataOffset, long tableOffset, uint blockSize, long diskSize)
        {
            Cookie = HeaderCookie;
            DataOffset = dataOffset;
            TableOffset = tableOffset;
            HeaderVersion = Version1;
            BlockSize = blockSize;
            MaxTableEntries = (int)((diskSize + blockSize - 1) / blockSize);
            ParentTimestamp = Footer.EpochUtc;
            ParentUnicodeName = string.Empty;
            ParentLocators = new ParentLocator[8];
            for (int i = 0; i < 8; ++i)
            {
                ParentLocators[i] = new ParentLocator();
            }
        }

        public DynamicHeader(DynamicHeader toCopy)
        {
            Cookie = toCopy.Cookie;
            DataOffset = toCopy.DataOffset;
            TableOffset = toCopy.TableOffset;
            HeaderVersion = toCopy.HeaderVersion;
            MaxTableEntries = toCopy.MaxTableEntries;
            BlockSize = toCopy.BlockSize;
            Checksum = toCopy.Checksum;
            ParentUniqueId = toCopy.ParentUniqueId;
            ParentTimestamp = toCopy.ParentTimestamp;
            ParentUnicodeName = toCopy.ParentUnicodeName;
            ParentLocators = new ParentLocator[toCopy.ParentLocators.Length];
            for (int i = 0; i < ParentLocators.Length; ++i)
            {
                ParentLocators[i] = new ParentLocator(toCopy.ParentLocators[i]);
            }
        }

        public static DynamicHeader FromBytes(byte[] data, int offset)
        {
            DynamicHeader result = new DynamicHeader();
            result.Cookie = EndianUtilities.BytesToString(data, offset, 8);
            result.DataOffset = EndianUtilities.ToInt64BigEndian(data, offset + 8);
            result.TableOffset = EndianUtilities.ToInt64BigEndian(data, offset + 16);
            result.HeaderVersion = EndianUtilities.ToUInt32BigEndian(data, offset + 24);
            result.MaxTableEntries = EndianUtilities.ToInt32BigEndian(data, offset + 28);
            result.BlockSize = EndianUtilities.ToUInt32BigEndian(data, offset + 32);
            result.Checksum = EndianUtilities.ToUInt32BigEndian(data, offset + 36);
            result.ParentUniqueId = EndianUtilities.ToGuidBigEndian(data, offset + 40);
            result.ParentTimestamp = Footer.EpochUtc.AddSeconds(EndianUtilities.ToUInt32BigEndian(data, offset + 56));
            result.ParentUnicodeName = Encoding.BigEndianUnicode.GetString(data, offset + 64, 512).TrimEnd('\0');

            result.ParentLocators = new ParentLocator[8];
            for (int i = 0; i < 8; ++i)
            {
                result.ParentLocators[i] = ParentLocator.FromBytes(data, offset + 576 + i * 24);
            }

            return result;
        }

        public void ToBytes(byte[] data, int offset)
        {
            EndianUtilities.StringToBytes(Cookie, data, offset, 8);
            EndianUtilities.WriteBytesBigEndian(DataOffset, data, offset + 8);
            EndianUtilities.WriteBytesBigEndian(TableOffset, data, offset + 16);
            EndianUtilities.WriteBytesBigEndian(HeaderVersion, data, offset + 24);
            EndianUtilities.WriteBytesBigEndian(MaxTableEntries, data, offset + 28);
            EndianUtilities.WriteBytesBigEndian(BlockSize, data, offset + 32);
            EndianUtilities.WriteBytesBigEndian(Checksum, data, offset + 36);
            EndianUtilities.WriteBytesBigEndian(ParentUniqueId, data, offset + 40);
            EndianUtilities.WriteBytesBigEndian((uint)(ParentTimestamp - Footer.EpochUtc).TotalSeconds, data, offset + 56);
            EndianUtilities.WriteBytesBigEndian((uint)0, data, offset + 60);
            Array.Clear(data, offset + 64, 512);
            Encoding.BigEndianUnicode.GetBytes(ParentUnicodeName, 0, ParentUnicodeName.Length, data, offset + 64);

            for (int i = 0; i < 8; ++i)
            {
                ParentLocators[i].ToBytes(data, offset + 576 + i * 24);
            }

            Array.Clear(data, offset + 1024 - 256, 256);
        }

        public bool IsValid()
        {
            return (Cookie == HeaderCookie)
                   && IsChecksumValid()
                   && HeaderVersion == Version1;
        }

        public bool IsChecksumValid()
        {
            return Checksum == CalculateChecksum();
        }

        public uint UpdateChecksum()
        {
            Checksum = CalculateChecksum();
            return Checksum;
        }

        internal static DynamicHeader FromStream(Stream stream)
        {
            return FromBytes(StreamUtilities.ReadExact(stream, 1024), 0);
        }

        private uint CalculateChecksum()
        {
            DynamicHeader copy = new DynamicHeader(this);
            copy.Checksum = 0;

            byte[] asBytes = new byte[1024];
            copy.ToBytes(asBytes, 0);
            uint checksum = 0;
            foreach (uint value in asBytes)
            {
                checksum += value;
            }

            checksum = ~checksum;

            return checksum;
        }
    }
}
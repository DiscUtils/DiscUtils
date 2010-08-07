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
using System.Text;

namespace DiscUtils.Vhd
{
    internal class DynamicHeader
    {
        public const string HeaderCookie = "cxsparse";
        public const uint Version1 = 0x00010000;
        public const uint DefaultBlockSize = 0x00200000;

        public string Cookie;
        public long DataOffset;
        public long TableOffset;
        public uint HeaderVersion;
        public int MaxTableEntries;
        public uint BlockSize;
        public uint Checksum;
        public Guid ParentUniqueId;
        public DateTime ParentTimestamp;
        public string ParentUnicodeName;
        public ParentLocator[] ParentLocators;

        public DynamicHeader() { }

        public DynamicHeader(long dataOffset, long tableOffset, uint blockSize, long diskSize)
        {
            Cookie = HeaderCookie;
            DataOffset = dataOffset;
            TableOffset = tableOffset;
            HeaderVersion = Version1;
            BlockSize = blockSize;
            MaxTableEntries = (int)((diskSize + blockSize - 1) / blockSize);
            ParentTimestamp = Footer.EpochUtc;
            ParentUnicodeName = "";
            ParentLocators = new ParentLocator[8];
            for(int i = 0; i < 8; ++i)
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

        internal static DynamicHeader FromStream(Stream stream)
        {
            return FromBytes(Utilities.ReadFully(stream, 1024), 0);
        }

        public static DynamicHeader FromBytes(byte[] data, int offset)
        {
            DynamicHeader result = new DynamicHeader();
            result.Cookie = Utilities.BytesToString(data, offset, 8);
            result.DataOffset = Utilities.ToInt64BigEndian(data, offset + 8);
            result.TableOffset = Utilities.ToInt64BigEndian(data, offset + 16);
            result.HeaderVersion = Utilities.ToUInt32BigEndian(data, offset + 24);
            result.MaxTableEntries = Utilities.ToInt32BigEndian(data, offset + 28);
            result.BlockSize = Utilities.ToUInt32BigEndian(data, offset + 32);
            result.Checksum = Utilities.ToUInt32BigEndian(data, offset + 36);
            result.ParentUniqueId = Utilities.ToGuidBigEndian(data, offset + 40);
            result.ParentTimestamp = Footer.EpochUtc.AddSeconds(Utilities.ToUInt32BigEndian(data, offset + 56));
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
            Utilities.StringToBytes(Cookie, data, offset, 8);
            Utilities.WriteBytesBigEndian(DataOffset, data, offset + 8);
            Utilities.WriteBytesBigEndian(TableOffset, data, offset + 16);
            Utilities.WriteBytesBigEndian(HeaderVersion, data, offset + 24);
            Utilities.WriteBytesBigEndian(MaxTableEntries, data, offset + 28);
            Utilities.WriteBytesBigEndian(BlockSize, data, offset + 32);
            Utilities.WriteBytesBigEndian(Checksum, data, offset + 36);
            Utilities.WriteBytesBigEndian(ParentUniqueId, data, offset + 40);
            Utilities.WriteBytesBigEndian((uint)(ParentTimestamp - Footer.EpochUtc).TotalSeconds, data, offset + 56);
            Utilities.WriteBytesBigEndian((uint)0, data, offset + 60);
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

        private uint CalculateChecksum()
        {
            DynamicHeader copy = new DynamicHeader(this);
            copy.Checksum = 0;

            byte[] asBytes = new byte[1024];
            copy.ToBytes(asBytes, 0);
            uint Checksum = 0;
            foreach (uint value in asBytes)
            {
                Checksum += value;
            }
            Checksum = ~Checksum;

            return Checksum;
        }
    }
}

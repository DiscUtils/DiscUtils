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

namespace DiscUtils.Registry
{
    internal sealed class HiveHeader : IByteArraySerializable
    {
        public const int HeaderSize = 512;

        private const uint Signature = 0x66676572;
        public uint Checksum;
        public Guid Guid1;
        public Guid Guid2;
        public int Length;
        public int MajorVersion;
        public int MinorVersion;
        public string Path;
        public int RootCell;

        public int Sequence1;
        public int Sequence2;
        public DateTime Timestamp;

        public HiveHeader()
        {
            Sequence1 = 1;
            Sequence2 = 1;
            Timestamp = DateTime.UtcNow;
            MajorVersion = 1;
            MinorVersion = 3;
            RootCell = -1;
            Path = string.Empty;
            Guid1 = Guid.NewGuid();
            Guid2 = Guid.NewGuid();
        }

        public int Size
        {
            get { return HeaderSize; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            uint sig = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
            if (sig != Signature)
            {
                throw new IOException("Invalid signature for registry hive");
            }

            Sequence1 = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x0004);
            Sequence2 = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x0008);

            Timestamp = DateTime.FromFileTimeUtc(EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x000C));

            MajorVersion = EndianUtilities.ToInt32LittleEndian(buffer, 0x0014);
            MinorVersion = EndianUtilities.ToInt32LittleEndian(buffer, 0x0018);

            int isLog = EndianUtilities.ToInt32LittleEndian(buffer, 0x001C);

            RootCell = EndianUtilities.ToInt32LittleEndian(buffer, 0x0024);
            Length = EndianUtilities.ToInt32LittleEndian(buffer, 0x0028);

            Path = Encoding.Unicode.GetString(buffer, 0x0030, 0x0040).Trim('\0');

            Guid1 = EndianUtilities.ToGuidLittleEndian(buffer, 0x0070);
            Guid2 = EndianUtilities.ToGuidLittleEndian(buffer, 0x0094);

            Checksum = EndianUtilities.ToUInt32LittleEndian(buffer, 0x01FC);

            if (Sequence1 != Sequence2)
            {
                throw new NotImplementedException("Support for replaying registry log file");
            }

            if (Checksum != CalcChecksum(buffer, offset))
            {
                throw new IOException("Invalid checksum on registry file");
            }

            return HeaderSize;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(Signature, buffer, offset);
            EndianUtilities.WriteBytesLittleEndian(Sequence1, buffer, offset + 0x0004);
            EndianUtilities.WriteBytesLittleEndian(Sequence2, buffer, offset + 0x0008);
            EndianUtilities.WriteBytesLittleEndian(Timestamp.ToFileTimeUtc(), buffer, offset + 0x000C);
            EndianUtilities.WriteBytesLittleEndian(MajorVersion, buffer, offset + 0x0014);
            EndianUtilities.WriteBytesLittleEndian(MinorVersion, buffer, offset + 0x0018);

            EndianUtilities.WriteBytesLittleEndian((uint)1, buffer, offset + 0x0020); // Unknown - seems to be '1'

            EndianUtilities.WriteBytesLittleEndian(RootCell, buffer, offset + 0x0024);
            EndianUtilities.WriteBytesLittleEndian(Length, buffer, offset + 0x0028);

            Encoding.Unicode.GetBytes(Path, 0, Path.Length, buffer, offset + 0x0030);
            EndianUtilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x0030 + Path.Length * 2);

            EndianUtilities.WriteBytesLittleEndian(Guid1, buffer, offset + 0x0070);
            EndianUtilities.WriteBytesLittleEndian(Guid2, buffer, offset + 0x0094);

            EndianUtilities.WriteBytesLittleEndian(CalcChecksum(buffer, offset), buffer, offset + 0x01FC);
        }

        private static uint CalcChecksum(byte[] buffer, int offset)
        {
            uint sum = 0;

            for (int i = 0; i < 0x01FC; i += 4)
            {
                sum = sum ^ EndianUtilities.ToUInt32LittleEndian(buffer, offset + i);
            }

            return sum;
        }
    }
}
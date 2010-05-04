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

namespace DiscUtils.Registry
{
    internal sealed class HiveHeader : IByteArraySerializable
    {
        public const int HeaderSize = 512;

        private const uint Signature = 0x66676572;

        public int Sequence1;
        public int Sequence2;
        public DateTime Timestamp;
        public int MajorVersion;
        public int MinorVersion;
        public int RootCell;
        public int Length;
        public uint Checksum;
        public string Path;
        public Guid Guid1;
        public Guid Guid2;

        public HiveHeader()
        {
            Sequence1 = 1;
            Sequence2 = 1;
            Timestamp = DateTime.UtcNow;
            MajorVersion = 1;
            MinorVersion = 3;
            RootCell = -1;
            Path = "";
            Guid1 = Guid.NewGuid();
            Guid2 = Guid.NewGuid();
        }

        #region IByteArraySerializable Members

        public int ReadFrom(byte[] buffer, int offset)
        {
            uint sig = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
            if (sig != Signature)
            {
                throw new IOException("Invalid signature for registry hive");
            }

            Sequence1 = Utilities.ToInt32LittleEndian(buffer, offset + 0x0004);
            Sequence2 = Utilities.ToInt32LittleEndian(buffer, offset + 0x0008);

            Timestamp = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x000C));

            MajorVersion = Utilities.ToInt32LittleEndian(buffer, 0x0014);
            MinorVersion = Utilities.ToInt32LittleEndian(buffer, 0x0018);

            int isLog = Utilities.ToInt32LittleEndian(buffer, 0x001C);

            RootCell = Utilities.ToInt32LittleEndian(buffer, 0x0024);
            Length = Utilities.ToInt32LittleEndian(buffer, 0x0028);

            Path = Encoding.Unicode.GetString(buffer, 0x0030, 0x0040).Trim('\0');

            Guid1 = Utilities.ToGuidLittleEndian(buffer, 0x0070);
            Guid2 = Utilities.ToGuidLittleEndian(buffer, 0x0094);

            Checksum = Utilities.ToUInt32LittleEndian(buffer, 0x01FC);

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
            Utilities.WriteBytesLittleEndian(Signature, buffer, offset);
            Utilities.WriteBytesLittleEndian(Sequence1, buffer, offset + 0x0004);
            Utilities.WriteBytesLittleEndian(Sequence2, buffer, offset + 0x0008);
            Utilities.WriteBytesLittleEndian(Timestamp.ToFileTimeUtc(), buffer, offset + 0x000C);
            Utilities.WriteBytesLittleEndian(MajorVersion, buffer, offset + 0x0014);
            Utilities.WriteBytesLittleEndian(MinorVersion, buffer, offset + 0x0018);

            Utilities.WriteBytesLittleEndian((uint)1, buffer, offset + 0x0020); // Unknown - seems to be '1'

            Utilities.WriteBytesLittleEndian(RootCell, buffer, offset + 0x0024);
            Utilities.WriteBytesLittleEndian(Length, buffer, offset + 0x0028);

            Encoding.Unicode.GetBytes(Path, 0, Path.Length, buffer, offset + 0x0030);
            Utilities.WriteBytesLittleEndian((ushort)0, buffer, offset + 0x0030 + Path.Length * 2);

            Utilities.WriteBytesLittleEndian(Guid1, buffer, offset + 0x0070);
            Utilities.WriteBytesLittleEndian(Guid2, buffer, offset + 0x0094);

            Utilities.WriteBytesLittleEndian(CalcChecksum(buffer, offset), buffer, offset + 0x01FC);
        }

        public int Size
        {
            get { return HeaderSize; }
        }

        #endregion

        private static uint CalcChecksum(byte[] buffer, int offset)
        {
            uint sum = 0;

            for (int i = 0; i < 0x01FC; i += 4)
            {
                sum = sum ^ Utilities.ToUInt32LittleEndian(buffer, offset + i);
            }

            return sum;
        }
    }
}

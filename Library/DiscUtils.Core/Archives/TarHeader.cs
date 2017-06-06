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
using DiscUtils.Streams;

namespace DiscUtils.Archives
{
    internal sealed class TarHeader
    {
        public const int Length = 512;
        public long FileLength;
        public UnixFilePermissions FileMode;

        public string FileName;
        public int GroupId;
        public DateTime ModificationTime;
        public int OwnerId;

        public void ReadFrom(byte[] buffer, int offset)
        {
            FileName = ReadNullTerminatedString(buffer, offset + 0, 100);
            FileMode = (UnixFilePermissions)OctalToLong(ReadNullTerminatedString(buffer, offset + 100, 8));
            OwnerId = (int)OctalToLong(ReadNullTerminatedString(buffer, offset + 108, 8));
            GroupId = (int)OctalToLong(ReadNullTerminatedString(buffer, offset + 116, 8));
            FileLength = OctalToLong(ReadNullTerminatedString(buffer, offset + 124, 12));
            ModificationTime = OctalToLong(ReadNullTerminatedString(buffer, offset + 136, 12)).FromUnixTimeSeconds().DateTime;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            Array.Clear(buffer, offset, Length);

            EndianUtilities.StringToBytes(FileName, buffer, offset, 99);
            EndianUtilities.StringToBytes(LongToOctal((long)FileMode, 7), buffer, offset + 100, 7);
            EndianUtilities.StringToBytes(LongToOctal(OwnerId, 7), buffer, offset + 108, 7);
            EndianUtilities.StringToBytes(LongToOctal(GroupId, 7), buffer, offset + 116, 7);
            EndianUtilities.StringToBytes(LongToOctal(FileLength, 11), buffer, offset + 124, 11);
            EndianUtilities.StringToBytes(LongToOctal(Convert.ToUInt32((new DateTimeOffset(ModificationTime)).ToUnixTimeSeconds()), 11), buffer, offset + 136, 11);

            // Checksum
            EndianUtilities.StringToBytes(new string(' ', 8), buffer, offset + 148, 8);
            long checkSum = 0;
            for (int i = 0; i < 512; ++i)
            {
                checkSum += buffer[offset + i];
            }

            EndianUtilities.StringToBytes(LongToOctal(checkSum, 7), buffer, offset + 148, 7);
            buffer[155] = 0;
        }

        private static string ReadNullTerminatedString(byte[] buffer, int offset, int length)
        {
            return EndianUtilities.BytesToString(buffer, offset, length).TrimEnd('\0');
        }

        private static long OctalToLong(string value)
        {
            long result = 0;

            for (int i = 0; i < value.Length; ++i)
            {
                result = result * 8 + (value[i] - '0');
            }

            return result;
        }

        private static string LongToOctal(long value, int length)
        {
            string result = string.Empty;

            while (value > 0)
            {
                result = (char)('0' + value % 8) + result;
                value = value / 8;
            }

            return new string('0', length - result.Length) + result;
        }
    }
}
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
using System.Globalization;
using System.IO;
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Iso9660
{
    internal static class IsoUtilities
    {
        public const int SectorSize = 2048;

        public static uint ToUInt32FromBoth(byte[] data, int offset)
        {
            return EndianUtilities.ToUInt32LittleEndian(data, offset);
        }

        public static ushort ToUInt16FromBoth(byte[] data, int offset)
        {
            return EndianUtilities.ToUInt16LittleEndian(data, offset);
        }

        internal static void ToBothFromUInt32(byte[] buffer, int offset, uint value)
        {
            EndianUtilities.WriteBytesLittleEndian(value, buffer, offset);
            EndianUtilities.WriteBytesBigEndian(value, buffer, offset + 4);
        }

        internal static void ToBothFromUInt16(byte[] buffer, int offset, ushort value)
        {
            EndianUtilities.WriteBytesLittleEndian(value, buffer, offset);
            EndianUtilities.WriteBytesBigEndian(value, buffer, offset + 2);
        }

        internal static void ToBytesFromUInt32(byte[] buffer, int offset, uint value)
        {
            EndianUtilities.WriteBytesLittleEndian(value, buffer, offset);
        }

        internal static void ToBytesFromUInt16(byte[] buffer, int offset, ushort value)
        {
            EndianUtilities.WriteBytesLittleEndian(value, buffer, offset);
        }

        internal static void WriteAChars(byte[] buffer, int offset, int numBytes, string str)
        {
            // Validate string
            if (!IsValidAString(str))
            {
                throw new IOException("Attempt to write string with invalid a-characters");
            }

            ////WriteASCII(buffer, offset, numBytes, true, str);
            WriteString(buffer, offset, numBytes, true, str, Encoding.ASCII);
        }

        internal static void WriteDChars(byte[] buffer, int offset, int numBytes, string str)
        {
            // Validate string
            if (!IsValidDString(str))
            {
                throw new IOException("Attempt to write string with invalid d-characters");
            }

            ////WriteASCII(buffer, offset, numBytes, true, str);
            WriteString(buffer, offset, numBytes, true, str, Encoding.ASCII);
        }

        internal static void WriteA1Chars(byte[] buffer, int offset, int numBytes, string str, Encoding enc)
        {
            // Validate string
            if (!IsValidAString(str))
            {
                throw new IOException("Attempt to write string with invalid a-characters");
            }

            WriteString(buffer, offset, numBytes, true, str, enc);
        }

        internal static void WriteD1Chars(byte[] buffer, int offset, int numBytes, string str, Encoding enc)
        {
            // Validate string
            if (!IsValidDString(str))
            {
                throw new IOException("Attempt to write string with invalid d-characters");
            }

            WriteString(buffer, offset, numBytes, true, str, enc);
        }

        internal static string ReadChars(byte[] buffer, int offset, int numBytes, Encoding enc)
        {
            char[] chars;

            // Special handling for 'magic' names '\x00' and '\x01', which indicate root and parent, respectively
            if (numBytes == 1)
            {
                chars = new char[1];
                chars[0] = (char)buffer[offset];
            }
            else
            {
                Decoder decoder = enc.GetDecoder();
                chars = new char[decoder.GetCharCount(buffer, offset, numBytes, false)];
                decoder.GetChars(buffer, offset, numBytes, chars, 0, false);
            }

            return new string(chars).TrimEnd(' ');
        }

#if false
        public static byte WriteFileName(byte[] buffer, int offset, int numBytes, string str, Encoding enc)
        {
            if (numBytes > 255 || numBytes < 0)
            {
                throw new ArgumentOutOfRangeException("numBytes", "Attempt to write overlength or underlength file name");
            }

            // Validate string
            if (!isValidFileName(str))
            {
                throw new IOException("Attempt to write string with invalid file name characters");
            }

            return (byte)WriteString(buffer, offset, numBytes, false, str, enc);
        }

        public static byte WriteDirectoryName(byte[] buffer, int offset, int numBytes, string str, Encoding enc)
        {
            if (numBytes > 255 || numBytes < 0)
            {
                throw new ArgumentOutOfRangeException("numBytes", "Attempt to write overlength or underlength directory name");
            }

            // Validate string
            if (!isValidDirectoryName(str))
            {
                throw new IOException("Attempt to write string with invalid directory name characters");
            }

            return (byte)WriteString(buffer, offset, numBytes, false, str, enc);
        }
#endif

        internal static int WriteString(byte[] buffer, int offset, int numBytes, bool pad, string str, Encoding enc)
        {
            return WriteString(buffer, offset, numBytes, pad, str, enc, false);
        }

        internal static int WriteString(byte[] buffer, int offset, int numBytes, bool pad, string str, Encoding enc,
                                        bool canTruncate)
        {
            Encoder encoder = enc.GetEncoder();

            string paddedString = pad ? str + new string(' ', numBytes) : str;

            // Assumption: never less than one byte per character

            int charsUsed;
            int bytesUsed;
            bool completed;
            encoder.Convert(paddedString.ToCharArray(), 0, paddedString.Length, buffer, offset, numBytes, false,
                out charsUsed, out bytesUsed, out completed);

            if (!canTruncate && charsUsed < str.Length)
            {
                throw new IOException("Failed to write entire string");
            }

            return bytesUsed;
        }

        internal static bool IsValidAString(string str)
        {
            for (int i = 0; i < str.Length; ++i)
            {
                if (!(
                    (str[i] >= ' ' && str[i] <= '\"')
                    || (str[i] >= '%' && str[i] <= '/')
                    || (str[i] >= ':' && str[i] <= '?')
                    || (str[i] >= '0' && str[i] <= '9')
                    || (str[i] >= 'A' && str[i] <= 'Z')
                    || (str[i] == '_')))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsValidDString(string str)
        {
            for (int i = 0; i < str.Length; ++i)
            {
                if (!IsValidDChar(str[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsValidDChar(char ch)
        {
            return (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z') || (ch == '_');
        }

        internal static bool IsValidFileName(string str)
        {
            for (int i = 0; i < str.Length; ++i)
            {
                if (
                    !((str[i] >= '0' && str[i] <= '9') || (str[i] >= 'A' && str[i] <= 'Z') || (str[i] == '_') ||
                      (str[i] == '.') || (str[i] == ';')))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsValidDirectoryName(string str)
        {
            if (str.Length == 1 && (str[0] == 0 || str[0] == 1))
            {
                return true;
            }
            return IsValidDString(str);
        }

        internal static string NormalizeFileName(string name)
        {
            string[] parts = SplitFileName(name);
            return parts[0] + '.' + parts[1] + ';' + parts[2];
        }

        internal static string[] SplitFileName(string name)
        {
            string[] parts = { name, string.Empty, "1" };

            if (name.Contains("."))
            {
                int endOfFilePart = name.IndexOf('.');
                parts[0] = name.Substring(0, endOfFilePart);
                if (name.Contains(";"))
                {
                    int verSep = name.IndexOf(';', endOfFilePart + 1);
                    parts[1] = name.Substring(endOfFilePart + 1, verSep - (endOfFilePart + 1));
                    parts[2] = name.Substring(verSep + 1);
                }
                else
                {
                    parts[1] = name.Substring(endOfFilePart + 1);
                }
            }
            else
            {
                if (name.Contains(";"))
                {
                    int verSep = name.IndexOf(';');
                    parts[0] = name.Substring(0, verSep);
                    parts[2] = name.Substring(verSep + 1);
                }
            }

            ushort ver;
            if (!ushort.TryParse(parts[2], out ver) || ver > 32767 || ver < 1)
            {
                ver = 1;
            }

            parts[2] = string.Format(CultureInfo.InvariantCulture, "{0}", ver);

            return parts;
        }

        /// <summary>
        /// Converts a DirectoryRecord time to UTC.
        /// </summary>
        /// <param name="data">Buffer containing the time data.</param>
        /// <param name="offset">Offset in buffer of the time data.</param>
        /// <returns>The time in UTC.</returns>
        internal static DateTime ToUTCDateTimeFromDirectoryTime(byte[] data, int offset)
        {
            try
            {
                DateTime relTime = new DateTime(
                    1900 + data[offset],
                    data[offset + 1],
                    data[offset + 2],
                    data[offset + 3],
                    data[offset + 4],
                    data[offset + 5],
                    DateTimeKind.Utc);
                return relTime - TimeSpan.FromMinutes(15 * (sbyte)data[offset + 6]);
            }
            catch (ArgumentOutOfRangeException)
            {
                // In case the ISO has a bad date encoded, we'll just fall back to using a fixed date
                return DateTime.MinValue;
            }
        }

        internal static void ToDirectoryTimeFromUTC(byte[] data, int offset, DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue)
            {
                Array.Clear(data, offset, 7);
            }
            else
            {
                if (dateTime.Year < 1900)
                {
                    throw new IOException("Year is out of range");
                }

                data[offset] = (byte)(dateTime.Year - 1900);
                data[offset + 1] = (byte)dateTime.Month;
                data[offset + 2] = (byte)dateTime.Day;
                data[offset + 3] = (byte)dateTime.Hour;
                data[offset + 4] = (byte)dateTime.Minute;
                data[offset + 5] = (byte)dateTime.Second;
                data[offset + 6] = 0;
            }
        }

        internal static DateTime ToDateTimeFromVolumeDescriptorTime(byte[] data, int offset)
        {
            bool allNull = true;
            for (int i = 0; i < 16; ++i)
            {
                if (data[offset + i] != (byte)'0' && data[offset + i] != 0)
                {
                    allNull = false;
                    break;
                }
            }

            if (allNull)
            {
                return DateTime.MinValue;
            }

            string strForm = Encoding.ASCII.GetString(data, offset, 16);

            // Work around bugs in burning software that may use zero bytes (rather than '0' characters)
            strForm = strForm.Replace('\0', '0');

            int year = SafeParseInt(1, 9999, strForm.Substring(0, 4));
            int month = SafeParseInt(1, 12, strForm.Substring(4, 2));
            int day = SafeParseInt(1, 31, strForm.Substring(6, 2));
            int hour = SafeParseInt(0, 23, strForm.Substring(8, 2));
            int min = SafeParseInt(0, 59, strForm.Substring(10, 2));
            int sec = SafeParseInt(0, 59, strForm.Substring(12, 2));
            int hundredths = SafeParseInt(0, 99, strForm.Substring(14, 2));

            try
            {
                DateTime time = new DateTime(year, month, day, hour, min, sec, hundredths * 10, DateTimeKind.Utc);
                return time - TimeSpan.FromMinutes(15 * (sbyte)data[offset + 16]);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.MinValue;
            }
        }

        internal static void ToVolumeDescriptorTimeFromUTC(byte[] buffer, int offset, DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue)
            {
                for (int i = offset; i < offset + 16; ++i)
                {
                    buffer[i] = (byte)'0';
                }

                buffer[offset + 16] = 0;
                return;
            }

            string strForm = dateTime.ToString("yyyyMMddHHmmssff", CultureInfo.InvariantCulture);
            EndianUtilities.StringToBytes(strForm, buffer, offset, 16);
            buffer[offset + 16] = 0;
        }

        internal static void EncodingToBytes(Encoding enc, byte[] data, int offset)
        {
            Array.Clear(data, offset, 32);
            if (enc == Encoding.ASCII)
            {
                // Nothing to do
            }
            else if (enc == Encoding.BigEndianUnicode)
            {
                data[offset + 0] = 0x25;
                data[offset + 1] = 0x2F;
                data[offset + 2] = 0x45;
            }
            else
            {
                throw new ArgumentException("Unrecognized character encoding");
            }
        }

        internal static Encoding EncodingFromBytes(byte[] data, int offset)
        {
            Encoding enc = Encoding.ASCII;
            if (data[offset + 0] == 0x25 && data[offset + 1] == 0x2F
                && (data[offset + 2] == 0x40 || data[offset + 2] == 0x43 || data[offset + 2] == 0x45))
            {
                // I.e. this is a joliet disc!
                enc = Encoding.BigEndianUnicode;
            }

            return enc;
        }

        internal static bool IsSpecialDirectory(DirectoryRecord r)
        {
            return r.FileIdentifier == "\0" || r.FileIdentifier == "\x01";
        }

        private static int SafeParseInt(int minVal, int maxVal, string str)
        {
            int val;
            if (!int.TryParse(str, out val))
            {
                return minVal;
            }

            if (val < minVal)
            {
                return minVal;
            }
            if (val > maxVal)
            {
                return maxVal;
            }
            return val;
        }
    }
}
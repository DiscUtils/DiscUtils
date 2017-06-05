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

namespace DiscUtils.Streams
{
    public static class EndianUtilities
    {
        #region Bit Twiddling
        
        public static void WriteBytesLittleEndian(ushort val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(val & 0xFF);
            buffer[offset + 1] = (byte)((val >> 8) & 0xFF);
        }

        public static void WriteBytesLittleEndian(uint val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(val & 0xFF);
            buffer[offset + 1] = (byte)((val >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((val >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((val >> 24) & 0xFF);
        }

        public static void WriteBytesLittleEndian(ulong val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(val & 0xFF);
            buffer[offset + 1] = (byte)((val >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((val >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((val >> 24) & 0xFF);
            buffer[offset + 4] = (byte)((val >> 32) & 0xFF);
            buffer[offset + 5] = (byte)((val >> 40) & 0xFF);
            buffer[offset + 6] = (byte)((val >> 48) & 0xFF);
            buffer[offset + 7] = (byte)((val >> 56) & 0xFF);
        }

        public static void WriteBytesLittleEndian(short val, byte[] buffer, int offset)
        {
            WriteBytesLittleEndian((ushort)val, buffer, offset);
        }

        public static void WriteBytesLittleEndian(int val, byte[] buffer, int offset)
        {
            WriteBytesLittleEndian((uint)val, buffer, offset);
        }

        public static void WriteBytesLittleEndian(long val, byte[] buffer, int offset)
        {
            WriteBytesLittleEndian((ulong)val, buffer, offset);
        }

        public static void WriteBytesLittleEndian(Guid val, byte[] buffer, int offset)
        {
            byte[] le = val.ToByteArray();
            Array.Copy(le, 0, buffer, offset, 16);
        }

        public static void WriteBytesBigEndian(ushort val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(val >> 8);
            buffer[offset + 1] = (byte)(val & 0xFF);
        }

        public static void WriteBytesBigEndian(uint val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)((val >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((val >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((val >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(val & 0xFF);
        }

        public static void WriteBytesBigEndian(ulong val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)((val >> 56) & 0xFF);
            buffer[offset + 1] = (byte)((val >> 48) & 0xFF);
            buffer[offset + 2] = (byte)((val >> 40) & 0xFF);
            buffer[offset + 3] = (byte)((val >> 32) & 0xFF);
            buffer[offset + 4] = (byte)((val >> 24) & 0xFF);
            buffer[offset + 5] = (byte)((val >> 16) & 0xFF);
            buffer[offset + 6] = (byte)((val >> 8) & 0xFF);
            buffer[offset + 7] = (byte)(val & 0xFF);
        }

        public static void WriteBytesBigEndian(short val, byte[] buffer, int offset)
        {
            WriteBytesBigEndian((ushort)val, buffer, offset);
        }

        public static void WriteBytesBigEndian(int val, byte[] buffer, int offset)
        {
            WriteBytesBigEndian((uint)val, buffer, offset);
        }

        public static void WriteBytesBigEndian(long val, byte[] buffer, int offset)
        {
            WriteBytesBigEndian((ulong)val, buffer, offset);
        }

        public static void WriteBytesBigEndian(Guid val, byte[] buffer, int offset)
        {
            byte[] le = val.ToByteArray();
            WriteBytesBigEndian(ToUInt32LittleEndian(le, 0), buffer, offset + 0);
            WriteBytesBigEndian(ToUInt16LittleEndian(le, 4), buffer, offset + 4);
            WriteBytesBigEndian(ToUInt16LittleEndian(le, 6), buffer, offset + 6);
            Array.Copy(le, 8, buffer, offset + 8, 8);
        }

        public static ushort ToUInt16LittleEndian(byte[] buffer, int offset)
        {
            return (ushort)(((buffer[offset + 1] << 8) & 0xFF00) | ((buffer[offset + 0] << 0) & 0x00FF));
        }

        public static uint ToUInt32LittleEndian(byte[] buffer, int offset)
        {
            return (uint)(((buffer[offset + 3] << 24) & 0xFF000000U) | ((buffer[offset + 2] << 16) & 0x00FF0000U)
                          | ((buffer[offset + 1] << 8) & 0x0000FF00U) | ((buffer[offset + 0] << 0) & 0x000000FFU));
        }

        public static ulong ToUInt64LittleEndian(byte[] buffer, int offset)
        {
            return ((ulong)ToUInt32LittleEndian(buffer, offset + 4) << 32) | ToUInt32LittleEndian(buffer, offset + 0);
        }

        public static short ToInt16LittleEndian(byte[] buffer, int offset)
        {
            return (short)ToUInt16LittleEndian(buffer, offset);
        }

        public static int ToInt32LittleEndian(byte[] buffer, int offset)
        {
            return (int)ToUInt32LittleEndian(buffer, offset);
        }

        public static long ToInt64LittleEndian(byte[] buffer, int offset)
        {
            return (long)ToUInt64LittleEndian(buffer, offset);
        }

        public static ushort ToUInt16BigEndian(byte[] buffer, int offset)
        {
            return (ushort)(((buffer[offset] << 8) & 0xFF00) | ((buffer[offset + 1] << 0) & 0x00FF));
        }

        public static uint ToUInt32BigEndian(byte[] buffer, int offset)
        {
            uint val = (uint)(((buffer[offset + 0] << 24) & 0xFF000000U) | ((buffer[offset + 1] << 16) & 0x00FF0000U)
                              | ((buffer[offset + 2] << 8) & 0x0000FF00U) | ((buffer[offset + 3] << 0) & 0x000000FFU));
            return val;
        }

        public static ulong ToUInt64BigEndian(byte[] buffer, int offset)
        {
            return ((ulong)ToUInt32BigEndian(buffer, offset + 0) << 32) | ToUInt32BigEndian(buffer, offset + 4);
        }

        public static short ToInt16BigEndian(byte[] buffer, int offset)
        {
            return (short)ToUInt16BigEndian(buffer, offset);
        }

        public static int ToInt32BigEndian(byte[] buffer, int offset)
        {
            return (int)ToUInt32BigEndian(buffer, offset);
        }

        public static long ToInt64BigEndian(byte[] buffer, int offset)
        {
            return (long)ToUInt64BigEndian(buffer, offset);
        }

        public static Guid ToGuidLittleEndian(byte[] buffer, int offset)
        {
            byte[] temp = new byte[16];
            Array.Copy(buffer, offset, temp, 0, 16);
            return new Guid(temp);
        }

        public static Guid ToGuidBigEndian(byte[] buffer, int offset)
        {
            return new Guid(
                ToUInt32BigEndian(buffer, offset + 0),
                ToUInt16BigEndian(buffer, offset + 4),
                ToUInt16BigEndian(buffer, offset + 6),
                buffer[offset + 8],
                buffer[offset + 9],
                buffer[offset + 10],
                buffer[offset + 11],
                buffer[offset + 12],
                buffer[offset + 13],
                buffer[offset + 14],
                buffer[offset + 15]);
        }

        public static byte[] ToByteArray(byte[] buffer, int offset, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(buffer, offset, result, 0, length);
            return result;
        }

        public static T ToStruct<T>(byte[] buffer, int offset)
            where T : IByteArraySerializable, new()
        {
            T result = new T();
            result.ReadFrom(buffer, offset);
            return result;
        }

        /// <summary>
        /// Primitive conversion from Unicode to ASCII that preserves special characters.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <param name="dest">The buffer to fill.</param>
        /// <param name="offset">The start of the string in the buffer.</param>
        /// <param name="count">The number of characters to convert.</param>
        /// <remarks>The built-in ASCIIEncoding converts characters of codepoint > 127 to ?,
        /// this preserves those code points by removing the top 16 bits of each character.</remarks>
        public static void StringToBytes(string value, byte[] dest, int offset, int count)
        {
            char[] chars = value.ToCharArray(0, Math.Min(value.Length, count));

            int i = 0;
            while (i < chars.Length && i < count)
            {
                dest[i + offset] = (byte)chars[i];
                ++i;
            }

            while (i < count)
            {
                dest[i + offset] = 0;
                ++i;
            }
        }

        /// <summary>
        /// Primitive conversion from ASCII to Unicode that preserves special characters.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <param name="offset">The first byte to convert.</param>
        /// <param name="count">The number of bytes to convert.</param>
        /// <returns>The string.</returns>
        /// <remarks>The built-in ASCIIEncoding converts characters of codepoint > 127 to ?,
        /// this preserves those code points.</remarks>
        public static string BytesToString(byte[] data, int offset, int count)
        {
            char[] result = new char[count];

            for (int i = 0; i < count; ++i)
            {
                result[i] = (char)data[i + offset];
            }

            return new string(result);
        }

        /// <summary>
        /// Primitive conversion from ASCII to Unicode that stops at a null-terminator.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <param name="offset">The first byte to convert.</param>
        /// <param name="count">The number of bytes to convert.</param>
        /// <returns>The string.</returns>
        /// <remarks>The built-in ASCIIEncoding converts characters of codepoint > 127 to ?,
        /// this preserves those code points.</remarks>
        public static string BytesToZString(byte[] data, int offset, int count)
        {
            char[] result = new char[count];

            for (int i = 0; i < count; ++i)
            {
                byte ch = data[i + offset];
                if (ch == 0)
                {
                    return new string(result, 0, i);
                }

                result[i] = (char)ch;
            }

            return new string(result);
        }

        #endregion
    }
}

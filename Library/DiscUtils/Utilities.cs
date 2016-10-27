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

namespace DiscUtils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    internal delegate TResult Func<T, TResult>(T arg);
    
    internal static class Utilities
    {
        /// <summary>
        /// The number of bytes in a standard disk sector (512).
        /// </summary>
        internal const int SectorSize = Sizes.Sector;

        /// <summary>
        /// The Epoch common to most (all?) Unix systems.
        /// </summary>
        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);

        /// <summary>
        /// Round up a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round up.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-up value.</returns>
        public static long RoundUp(long value, long unit)
        {
            return ((value + (unit - 1)) / unit) * unit;
        }

        /// <summary>
        /// Round up a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round up.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-up value.</returns>
        public static int RoundUp(int value, int unit)
        {
            return ((value + (unit - 1)) / unit) * unit;
        }

        /// <summary>
        /// Round down a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round down.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-down value.</returns>
        public static long RoundDown(long value, long unit)
        {
            return (value / unit) * unit;
        }

        /// <summary>
        /// Calculates the CEIL function.
        /// </summary>
        /// <param name="numerator">The value to divide.</param>
        /// <param name="denominator">The value to divide by.</param>
        /// <returns>The value of CEIL(numerator/denominator).</returns>
        public static int Ceil(int numerator, int denominator)
        {
            return (numerator + (denominator - 1)) / denominator;
        }

        /// <summary>
        /// Calculates the CEIL function.
        /// </summary>
        /// <param name="numerator">The value to divide.</param>
        /// <param name="denominator">The value to divide by.</param>
        /// <returns>The value of CEIL(numerator/denominator).</returns>
        public static uint Ceil(uint numerator, uint denominator)
        {
            return (numerator + (denominator - 1)) / denominator;
        }

        /// <summary>
        /// Calculates the CEIL function.
        /// </summary>
        /// <param name="numerator">The value to divide.</param>
        /// <param name="denominator">The value to divide by.</param>
        /// <returns>The value of CEIL(numerator/denominator).</returns>
        public static long Ceil(long numerator, long denominator)
        {
            return (numerator + (denominator - 1)) / denominator;
        }

        /// <summary>
        /// Converts between two arrays.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the source array.</typeparam>
        /// <typeparam name="U">The type of the elements of the destination array.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="func">The function to map from source type to destination type.</param>
        /// <returns>The resultant array.</returns>
        public static U[] Map<T, U>(ICollection<T> source, Func<T, U> func)
        {
            U[] result = new U[source.Count];
            int i = 0;

            foreach (T sVal in source)
            {
                result[i++] = func(sVal);
            }

            return result;
        }

        /// <summary>
        /// Converts between two arrays.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the source array.</typeparam>
        /// <typeparam name="U">The type of the elements of the destination array.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="func">The function to map from source type to destination type.</param>
        /// <returns>The resultant array.</returns>
        public static U[] Map<T, U>(IEnumerable<T> source, Func<T, U> func)
        {
            List<U> result = new List<U>();

            foreach (T sVal in source)
            {
                result.Add(func(sVal));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Filters a collection into a new collection.
        /// </summary>
        /// <typeparam name="C">The type of the new collection.</typeparam>
        /// <typeparam name="T">The type of the collection entries.</typeparam>
        /// <param name="source">The collection to filter.</param>
        /// <param name="predicate">The predicate to select which entries are carried over.</param>
        /// <returns>The new collection, containing all entries where the predicate returns <c>true</c>.</returns>
        public static C Filter<C, T>(ICollection<T> source, Func<T, bool> predicate) where C : ICollection<T>, new()
        {
            C result = new C();
            foreach (T val in source)
            {
                if (predicate(val))
                {
                    result.Add(val);
                }
            }

            return result;
        }

        /// <summary>
        /// Indicates if two ranges overlap.
        /// </summary>
        /// <typeparam name="T">The type of the ordinals.</typeparam>
        /// <param name="xFirst">The lowest ordinal of the first range (inclusive).</param>
        /// <param name="xLast">The highest ordinal of the first range (exclusive).</param>
        /// <param name="yFirst">The lowest ordinal of the second range (inclusive).</param>
        /// <param name="yLast">The highest ordinal of the second range (exclusive).</param>
        /// <returns><c>true</c> if the ranges overlap, else <c>false</c>.</returns>
        public static bool RangesOverlap<T>(T xFirst, T xLast, T yFirst, T yLast) where T : IComparable<T>
        {
            return !((xLast.CompareTo(yFirst) <= 0) || (xFirst.CompareTo(yLast) >= 0));
        }

        /// <summary>
        /// Validates standard buffer, offset, count parameters to a method.
        /// </summary>
        /// <param name="buffer">The byte array to read from / write to.</param>
        /// <param name="offset">The starting offset in <c>buffer</c>.</param>
        /// <param name="count">The number of bytes to read / write.</param>
        public static void AssertBufferParameters(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset", offset, "Offset is negative");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count", count, "Count is negative");
            }

            if (buffer.Length < offset + count)
            {
                throw new ArgumentException("buffer is too small", "buffer");
            }
        }

        #region Bit Twiddling
        public static bool IsAllZeros(byte[] buffer, int offset, int count)
        {
            int end = offset + count;
            for (int i = offset; i < end; ++i)
            {
                if (buffer[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsPowerOfTwo(uint val)
        {
            if (val == 0)
            {
                return false;
            }

            while ((val & 1) != 1)
            {
                val >>= 1;
            }

            return val == 1;
        }

        public static bool IsPowerOfTwo(long val)
        {
            if (val == 0)
            {
                return false;
            }

            while ((val & 1) != 1)
            {
                val >>= 1;
            }

            return val == 1;
        }

        public static int Log2(uint val)
        {
            if (val == 0)
            {
                throw new ArgumentException("Cannot calculate log of Zero", "val");
            }

            int result = 0;
            while ((val & 1) != 1)
            {
                val >>= 1;
                ++result;
            }

            if (val == 1)
            {
                return result;
            }
            else
            {
                throw new ArgumentException("Input is not a power of Two", "val");
            }
        }

        public static int Log2(int val)
        {
            if (val == 0)
            {
                throw new ArgumentException("Cannot calculate log of Zero", "val");
            }

            int result = 0;
            while ((val & 1) != 1)
            {
                val >>= 1;
                ++result;
            }

            if (val == 1)
            {
                return result;
            }
            else
            {
                throw new ArgumentException("Input is not a power of Two", "val");
            }
        }

        public static bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; ++i)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static ushort BitSwap(ushort value)
        {
            return (ushort)(((value & 0x00FF) << 8) | ((value & 0xFF00) >> 8));
        }

        public static uint BitSwap(uint value)
        {
            return ((value & 0xFF) << 24) | ((value & 0xFF00) << 8) | ((value & 0x00FF0000) >> 8) | ((value & 0xFF000000) >> 24);
        }

        public static ulong BitSwap(ulong value)
        {
            return (((ulong)BitSwap((uint)(value & 0xFFFFFFFF))) << 32) | BitSwap((uint)(value >> 32));
        }

        public static short BitSwap(short value)
        {
            return (short)BitSwap((ushort)value);
        }

        public static int BitSwap(int value)
        {
            return (int)BitSwap((uint)value);
        }

        public static long BitSwap(long value)
        {
            return (long)BitSwap((ulong)value);
        }

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
            return (((ulong)ToUInt32LittleEndian(buffer, offset + 4)) << 32) | ToUInt32LittleEndian(buffer, offset + 0);
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
            return (((ulong)ToUInt32BigEndian(buffer, offset + 0)) << 32) | ToUInt32BigEndian(buffer, offset + 4);
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
            char[] chars = value.ToCharArray();

            int i = 0;
            while (i < chars.Length)
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

        #region Path Manipulation
        /// <summary>
        /// Extracts the directory part of a path.
        /// </summary>
        /// <param name="path">The path to process.</param>
        /// <returns>The directory part.</returns>
        public static string GetDirectoryFromPath(string path)
        {
            string trimmed = path.TrimEnd('\\');

            int index = trimmed.LastIndexOf('\\');
            if (index < 0)
            {
                return string.Empty; // No directory, just a file name
            }

            return trimmed.Substring(0, index);
        }

        /// <summary>
        /// Extracts the file part of a path.
        /// </summary>
        /// <param name="path">The path to process.</param>
        /// <returns>The file part of the path.</returns>
        public static string GetFileFromPath(string path)
        {
            string trimmed = path.Trim('\\');

            int index = trimmed.LastIndexOf('\\');
            if (index < 0)
            {
                return trimmed; // No directory, just a file name
            }

            return trimmed.Substring(index + 1);
        }

        /// <summary>
        /// Combines two paths.
        /// </summary>
        /// <param name="a">The first part of the path.</param>
        /// <param name="b">The second part of the path.</param>
        /// <returns>The combined path.</returns>
        public static string CombinePaths(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || (b.Length > 0 && b[0] == '\\'))
            {
                return b;
            }
            else if (string.IsNullOrEmpty(b))
            {
                return a;
            }
            else
            {
                return a.TrimEnd('\\') + '\\' + b.TrimStart('\\');
            }
        }

        /// <summary>
        /// Resolves a relative path into an absolute one.
        /// </summary>
        /// <param name="basePath">The base path to resolve from.</param>
        /// <param name="relativePath">The relative path.</param>
        /// <returns>The absolute path, so far as it can be resolved.  If the
        /// <paramref name="relativePath"/> contains more '..' characters than the
        /// base path contains levels of directory, the resultant string will be relative.
        /// For example: (TEMP\Foo.txt, ..\..\Bar.txt) gives (..\Bar.txt).</returns>
        public static string ResolveRelativePath(string basePath, string relativePath)
        {
            List<string> pathElements = new List<string>(basePath.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries));
            if (!basePath.EndsWith(@"\", StringComparison.Ordinal) && pathElements.Count > 0)
            {
                pathElements.RemoveAt(pathElements.Count - 1);
            }

            pathElements.AddRange(relativePath.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries));

            int pos = 1;
            while (pos < pathElements.Count)
            {
                if (pathElements[pos] == ".")
                {
                    pathElements.RemoveAt(pos);
                }
                else if (pathElements[pos] == ".." && pos > 0 && pathElements[pos - 1][0] != '.')
                {
                    pathElements.RemoveAt(pos);
                    pathElements.RemoveAt(pos - 1);
                    pos--;
                }
                else
                {
                    pos++;
                }
            }

            string merged = string.Join(@"\", pathElements.ToArray());
            if (relativePath.EndsWith(@"\", StringComparison.Ordinal))
            {
                merged += @"\";
            }

            if (basePath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                merged = @"\\" + merged;
            }
            else if (basePath.StartsWith(@"\", StringComparison.Ordinal))
            {
                merged = @"\" + merged;
            }

            return merged;
        }

        public static string ResolvePath(string basePath, string path)
        {
            if (!path.StartsWith("\\", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveRelativePath(basePath, path);
            }
            else
            {
                return path;
            }
        }

        public static string MakeRelativePath(string path, string basePath)
        {
            List<string> pathElements = new List<string>(path.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries));
            List<string> basePathElements = new List<string>(basePath.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries));

            if (!basePath.EndsWith("\\", StringComparison.Ordinal) && basePathElements.Count > 0)
            {
                basePathElements.RemoveAt(basePathElements.Count - 1);
            }

            // Find first part of paths that don't match
            int i = 0;
            while (i < Math.Min(pathElements.Count - 1, basePathElements.Count))
            {
                if (pathElements[i].ToUpperInvariant() != basePathElements[i].ToUpperInvariant())
                {
                    break;
                }

                ++i;
            }

            // For each remaining part of the base path, insert '..'
            StringBuilder result = new StringBuilder();
            if (i == basePathElements.Count)
            {
                result.Append(@".\");
            }
            else if (i < basePathElements.Count)
            {
                for (int j = 0; j < basePathElements.Count - i; ++j)
                {
                    result.Append(@"..\");
                }
            }

            // For each remaining part of the path, add the path element
            for (int j = i; j < pathElements.Count - 1; ++j)
            {
                result.Append(pathElements[j]);
                result.Append(@"\");
            }

            result.Append(pathElements[pathElements.Count - 1]);

            // If the target was a directory, put the terminator back
            if (path.EndsWith(@"\", StringComparison.Ordinal))
            {
                result.Append(@"\");
            }

            return result.ToString();
        }
        #endregion

        #region Stream Manipulation
        /// <summary>
        /// Read bytes until buffer filled or EOF.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="buffer">The buffer to populate.</param>
        /// <param name="offset">Offset in the buffer to start.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The number of bytes actually read.</returns>
        public static int ReadFully(Stream stream, byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            int numRead = stream.Read(buffer, offset, length);
            while (numRead > 0)
            {
                totalRead += numRead;
                if (totalRead == length)
                {
                    break;
                }

                numRead = stream.Read(buffer, offset + totalRead, length - totalRead);
            }

            return totalRead;
        }

        /// <summary>
        /// Read bytes until buffer filled or throw IOException.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The data read from the stream.</returns>
        public static byte[] ReadFully(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            if (ReadFully(stream, buffer, 0, count) == count)
            {
                return buffer;
            }
            else
            {
                throw new IOException("Unable to complete read of " + count + " bytes");
            }
        }

        /// <summary>
        /// Read bytes until buffer filled or EOF.
        /// </summary>
        /// <param name="buffer">The stream to read.</param>
        /// <param name="pos">The position in buffer to read from.</param>
        /// <param name="data">The buffer to populate.</param>
        /// <param name="offset">Offset in the buffer to start.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The number of bytes actually read.</returns>
        public static int ReadFully(IBuffer buffer, long pos, byte[] data, int offset, int length)
        {
            int totalRead = 0;
            int numRead = buffer.Read(pos, data, offset, length);
            while (numRead > 0)
            {
                totalRead += numRead;
                if (totalRead == length)
                {
                    break;
                }

                numRead = buffer.Read(pos, data, offset + totalRead, length - totalRead);
            }

            return totalRead;
        }

        /// <summary>
        /// Read bytes until buffer filled or throw IOException.
        /// </summary>
        /// <param name="buffer">The buffer to read.</param>
        /// <param name="pos">The position in buffer to read from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The data read from the stream.</returns>
        public static byte[] ReadFully(IBuffer buffer, long pos, int count)
        {
            byte[] result = new byte[count];
            if (ReadFully(buffer, pos, result, 0, count) == count)
            {
                return result;
            }
            else
            {
                throw new IOException("Unable to complete read of " + count + " bytes");
            }
        }

        /// <summary>
        /// Read bytes until buffer filled or throw IOException.
        /// </summary>
        /// <param name="buffer">The buffer to read.</param>
        /// <returns>The data read from the stream.</returns>
        public static byte[] ReadAll(IBuffer buffer)
        {
            return ReadFully(buffer, 0, (int)buffer.Capacity);
        }

        /// <summary>
        /// Reads a disk sector (512 bytes).
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <returns>The sector data as a byte array.</returns>
        public static byte[] ReadSector(Stream stream)
        {
            return ReadFully(stream, SectorSize);
        }

        /// <summary>
        /// Reads a structure from a stream.
        /// </summary>
        /// <typeparam name="T">The type of the structure.</typeparam>
        /// <param name="stream">The stream to read.</param>
        /// <returns>The structure.</returns>
        public static T ReadStruct<T>(Stream stream)
            where T : IByteArraySerializable, new()
        {
            T result = new T();
            byte[] buffer = Utilities.ReadFully(stream, result.Size);
            result.ReadFrom(buffer, 0);
            return result;
        }

        /// <summary>
        /// Reads a structure from a stream.
        /// </summary>
        /// <typeparam name="T">The type of the structure.</typeparam>
        /// <param name="stream">The stream to read.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The structure.</returns>
        public static T ReadStruct<T>(Stream stream, int length)
            where T : IByteArraySerializable, new()
        {
            T result = new T();
            byte[] buffer = Utilities.ReadFully(stream, length);
            result.ReadFrom(buffer, 0);
            return result;
        }

        /// <summary>
        /// Writes a structure to a stream.
        /// </summary>
        /// <typeparam name="T">The type of the structure.</typeparam>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="obj">The structure to write.</param>
        public static void WriteStruct<T>(Stream stream, T obj)
            where T : IByteArraySerializable
        {
            byte[] buffer = new byte[obj.Size];
            obj.WriteTo(buffer, 0);
            stream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Copies the contents of one stream to another.
        /// </summary>
        /// <param name="source">The stream to copy from.</param>
        /// <param name="dest">The destination stream.</param>
        /// <remarks>Copying starts at the current stream positions.</remarks>
        public static void PumpStreams(Stream source, Stream dest)
        {
            byte[] buffer = new byte[64 * 1024];

            int numRead = source.Read(buffer, 0, buffer.Length);
            while (numRead != 0)
            {
                dest.Write(buffer, 0, numRead);
                numRead = source.Read(buffer, 0, buffer.Length);
            }
        }

        #endregion

        #region Filesystem Support

        /// <summary>
        /// Indicates if a file name matches the 8.3 pattern.
        /// </summary>
        /// <param name="name">The name to test.</param>
        /// <returns><c>true</c> if the name is 8.3, otherwise <c>false</c>.</returns>
        public static bool Is8Dot3(string name)
        {
            if (name.Length > 12)
            {
                return false;
            }

            string[] split = name.Split(new char[] { '.' });

            if (split.Length > 2 || split.Length < 1)
            {
                return false;
            }

            if (split[0].Length > 8)
            {
                return false;
            }

            foreach (char ch in split[0])
            {
                if (!Is8Dot3Char(ch))
                {
                    return false;
                }
            }

            if (split.Length > 1)
            {
                if (split[1].Length > 3)
                {
                    return false;
                }

                foreach (char ch in split[1])
                {
                    if (!Is8Dot3Char(ch))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool Is8Dot3Char(char ch)
        {
            return (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || "_^$~!#%£-{}()@'`&".IndexOf(ch) != -1;
        }

        /// <summary>
        /// Converts a 'standard' wildcard file/path specification into a regular expression.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to convert.</param>
        /// <returns>The resultant regular expression.</returns>
        /// <remarks>
        /// The wildcard * (star) matches zero or more characters (including '.'), and ?
        /// (question mark) matches precisely one character (except '.').
        /// </remarks>
        public static Regex ConvertWildcardsToRegEx(string pattern)
        {
            if (!pattern.Contains("."))
            {
                pattern += ".";
            }

            string query = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", "[^.]") + "$";
            return new Regex(query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public static DateTime DateTimeFromUnix(uint fileTime)
        {
            long ticks = fileTime * (long)10 * 1000 * 1000;
            return new DateTime(ticks + UnixEpoch.Ticks);
        }

        public static uint DateTimeToUnix(DateTime time)
        {
            return (uint)((time.Ticks - UnixEpoch.Ticks) / (10 * 1000 * 1000));
        }

        public static FileAttributes FileAttributesFromUnixFileType(UnixFileType fileType)
        {
            switch (fileType)
            {
                case UnixFileType.Fifo:
                    return FileAttributes.Device | FileAttributes.System;
                case UnixFileType.Character:
                    return FileAttributes.Device | FileAttributes.System;
                case UnixFileType.Directory:
                    return FileAttributes.Directory;
                case UnixFileType.Block:
                    return FileAttributes.Device | FileAttributes.System;
                case UnixFileType.Regular:
                    return FileAttributes.Normal;
                case UnixFileType.Link:
                    return FileAttributes.ReparsePoint;
                case UnixFileType.Socket:
                    return FileAttributes.Device | FileAttributes.System;
                default:
                    return (FileAttributes)0;
            }
        }
        #endregion
    }
}

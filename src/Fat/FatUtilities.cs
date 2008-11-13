//
// Copyright (c) 2008, Kenneth Bell
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
using System.Text;

namespace DiscUtils.Fat
{
    internal class FatUtilities
    {
        private const string SpecialPrivateChars = "$%'-_@~`!(){}^#&";

        /// <summary>
        /// Prevent instantiation.
        /// </summary>
        private FatUtilities() { }

        /// <summary>
        /// Primitive conversion from Unicode to ASCII that preserves special characters.
        /// </summary>
        /// <param name="value">The string to convert</param>
        /// <param name="dest">The buffer to fill</param>
        /// <param name="offset">The start of the string in the buffer</param>
        /// <param name="count">The number of characters to convert</param>
        /// <remarks>The built-in ASCIIEncoding converts characters of codepoint > 127 to ?,
        /// we need to preserve them.  Instead we'll just truncate the top-half of each character.</remarks>
        public static void StringToBytes(string value, byte[] dest, int offset, int count)
        {
            char[] chars = value.ToCharArray();

            for (int i = 0; i < count; ++i)
            {
                dest[i + offset] = (byte)chars[i];
            }
        }

        /// <summary>
        /// Primitive conversion from ASCII to Unicode that preserves special characters.
        /// </summary>
        /// <param name="data">The data to convert</param>
        /// <param name="offset">The first byte to convert</param>
        /// <param name="count">The number of bytes to convert</param>
        /// <returns>The string</returns>
        /// <remarks>The built-in ASCIIEncoding converts characters of codepoint > 127 to ?,
        /// we need to preserve them.</remarks>
        public static string BytesToString(byte[] data, int offset, int count)
        {
            char[] result = new char[count];

            for (int i = 0; i < count; ++i)
            {
                result[i] = (char)data[i + offset];
            }

            return new String(result);
        }

        /// <summary>
        /// Converts between two arrays.
        /// </summary>
        /// <typeparam name="T">The type of the elements of the source array</typeparam>
        /// <typeparam name="U">The type of the elements of the destination array</typeparam>
        /// <param name="source">The source array</param>
        /// <param name="func">The function to map from source type to destination type</param>
        /// <returns>The resultant array</returns>
        public static U[] Map<T, U>(T[] source, Func<T, U> func)
        {
            U[] result = new U[source.Length];
            for (int i = 0; i < source.Length; ++i)
            {
                result[i] = func(source[i]);
            }
            return result;
        }

        public static string NormalizedFileNameFromPath(string path)
        {
            string[] elems = path.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
            return NormalizeFileName(elems[elems.Length - 1]);
        }

        public static string NormalizeFileName(string name)
        {
            // Put it through a conversion round-trip, to catch invalid characters
            string roundTripped = Encoding.Default.GetString(Encoding.Default.GetBytes(name));

            // Divide the name from extension
            string[] parts = roundTripped.Split('.');
            if (parts.Length < 1 || parts.Length > 2)
            {
                throw new ArgumentException("Invalid file name", "name");
            }
            string namePart = parts[0];
            string extPart = (parts.Length == 2 ? parts[1] : "");

            // Check lengths
            if (namePart.Length > 8 || extPart.Length > 3)
            {
                throw new ArgumentException("Invalid file name", "name");
            }

            foreach (char ch in namePart)
            {
                if (!(Char.IsLetterOrDigit(ch) || ch > 127 || SpecialPrivateChars.IndexOf(ch) >= 0))
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid character '{0}' not allowed in file names", ch), "name");
                }
            }

            foreach (char ch in extPart)
            {
                if (!(Char.IsLetterOrDigit(ch) || ch > 127 || SpecialPrivateChars.IndexOf(ch) >= 0))
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Invalid character '{0}' not allowed in file extensions", ch), "name");
                }
            }

            return String.Format(CultureInfo.InvariantCulture, "{0,-8}{1,-3}", namePart.ToUpperInvariant(), extPart.ToUpperInvariant());
        }
    }
}

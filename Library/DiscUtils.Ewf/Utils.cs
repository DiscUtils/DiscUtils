//
// Copyright (c) 2013, Adam Bridge
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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Ewf
{
    /// <summary>
    /// Collection of helper functions for processing the EWF files.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Helper method to see if two byte arrays (or parts thereof) are the same.
        /// </summary>
        /// <param name="one">The first byte array.</param>
        /// <param name="oneStartIndex">The offset in the first array from where comparison will start.</param>
        /// <param name="two">The second byte array.</param>
        /// <param name="twoStartIndex">The offset in the second array from where comparison will start.</param>
        /// <param name="length">The number of bytes to compare.</param>
        /// <returns>trur if the arrays (or specified parts) contain the same values.</returns>
        public static bool CompareByteArrays(byte[] one, int oneStartIndex, byte[] two, int twoStartIndex, int length)
        {
            for (int i = oneStartIndex; i < length; i++)
            {
                if (one[i] != two[i + twoStartIndex])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates what the next segment file name after current.
        /// </summary>
        /// <param name="current">The path from which to calculate the next segment file.</param>
        /// <returns>The path of the next segment file.</returns>
        public static string CalcNextFilename(string current)
        {
            string stub = current.Substring(0, current.Length - 3);
            string ext = current.Substring(stub.Length);

            if (ext[2] >= '1' && ext[2] <= '8')
            {
                ext = ext.Substring(0,2) + (char)(ext[2] + 1);
            }
            else if (ext[2] == '9')
            {
                if (ext[1] == '9')
                {
                    ext = "EAA";
                }
                else
                {
                    ext = "" + (char)ext[0] + (char)(ext[1] + 1) + '0';
                }
            }
            else
            {
                if (ext[2] == 'Z')
                {
                    if (ext[1] == 'Z')
                    {
                        if (ext[0] == 'Z')
                        {
                            throw new ArgumentException("cannot calculate filename after .ZZZ");
                        }
                        else
                        {
                            ext = "" + (char)(ext[0] + 1) + "AA";
                        }
                    }
                    else
                    {
                        ext = "" + (char)ext[0] + (char)(ext[1] + 1) + 'A';
                    }
                }
                else
                {
                    ext = ext.Substring(0, 2) + (char)(ext[2] + 1);
                }
            }

            return stub + ext;
        }

        /// <summary>
        /// Translates an array of bytes into a human-friendly format.
        /// </summary>
        /// <param name="bytes">The bytes to translate.</param>
        /// <param name="offset">From where in the array the translation starts.</param>
        /// <param name="length">The number of bytes to translate.</param>        
        /// <returns>A string of the byte values.</returns>
        public static string ByteArrayToByteString(byte[] bytes, int offset, int length)
        {
            return ByteArrayToByteString(bytes, offset, length, false);
        }

        /// <summary>
        /// Translates an array of bytes into a human-friendly format.
        /// </summary>
        /// <param name="bytes">The bytes to translate.</param>
        /// <param name="offset">From where in the array the translation starts.</param>
        /// <param name="length">The number of bytes to translate.</param>
        /// <param name="incSpace">Should a space be placed between each byte.</param>
        /// <returns>A string of the byte values, optionally seperated by spaces.</returns>
        public static string ByteArrayToByteString(byte[] bytes, int offset, int length, bool incSpace)
        {
            StringBuilder sb = new StringBuilder();
            if (incSpace)
            {
                for (int i = 0; i < length; i++)
                    sb.AppendFormat("{0:X2} ", bytes[offset + i]);
                sb.Remove(sb.Length - 1, 1);
            }
            else
            {
                for (int i = 0; i < length; i++)
                    sb.AppendFormat("{0:X2}", bytes[offset + i]);
            }
            return sb.ToString();
        }
    }
}

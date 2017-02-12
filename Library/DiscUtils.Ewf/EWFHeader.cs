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

namespace DiscUtils.Ewf
{
    /// <summary>
    /// Class to represent the header/signature/magic of an EWF file.
    /// </summary>
    public class EWFHeader
    {
        /// <summary>
        /// The number of bytes that make up the EWF header.
        /// </summary>
        public const int HEADER_SIZE = 13;

        /// <summary>
        /// First 8-byte 'magic' of an EWF file.
        /// </summary>
        public static byte[] COOKIE = { (byte)'E', (byte)'V', (byte)'F', 0x09, 0x0D, 0x0A, 0xFF, 0x00 };

        /// <summary>
        /// Start of Fields: 1
        /// </summary>
        public const int SOF = 1; // Start of Fields

        /// <summary>
        /// End of Fields: 0
        /// </summary>
        public const int EOF = 0; // End of Fields

        /// <summary>
        /// Whether or not the Cookie is valid.
        /// </summary>
        public bool ValidCookie { get; private set; }

        /// <summary>
        /// Whether or not the Start of Fields is valid.
        /// </summary>
        public bool ValidSOF { get; private set; }

        /// <summary>
        /// The 1-based segment number of this segment file.
        /// </summary>
        public int SegmentNumber { get; private set; }

        /// <summary>
        /// Whether or not the End of Fields is valid
        /// </summary>
        public bool ValidEOF { get; private set; }

        /// <summary>
        /// Constructs an EWFHeader from the provided bytes.
        /// </summary>
        /// <param name="bytes"></param>
        public EWFHeader(byte[] bytes)
        {
            if (bytes.Length != HEADER_SIZE)
                throw new ArgumentException("number of bytes in header must be " + HEADER_SIZE, "byte");

            ValidCookie = Utils.CompareByteArrays(bytes, 0, COOKIE, 0, 8);
            if (!ValidCookie)
                throw new ArgumentException("invalid EWF signature");

            ValidSOF = (bytes[8] == SOF);
            if (!ValidSOF)
                throw new ArgumentException("invalid StartOfFields marker");

            SegmentNumber = BitConverter.ToInt16(bytes, 9);
            if (SegmentNumber < 1)
                throw new ArgumentException("invalid segment number, cannot be less that 1");

            ValidEOF = (BitConverter.ToInt16(bytes, 11) == EOF);
            if (!ValidEOF)
                throw new ArgumentException("invalid EndOfFields marker");
        }
    }
}

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

namespace DiscUtils.Ewf.Section
{
    /// <summary>
    /// The Error2 section from the EWF file.
    /// </summary>
    public class Error2
    {
        /// <summary>
        /// A list of the errors stored in the Error2 section.
        /// </summary>
        public List<Error2Entry> Entries { get; set; }

        /// <summary>
        /// Creates a new Error2 object from bytes.
        /// </summary>
        /// <param name="bytes">The bytes that make up the Error2 section.</param>
        public Error2(byte[] bytes)
        {
            int entryCount = BitConverter.ToInt32(bytes, 0);
            Entries = new List<Error2Entry>(entryCount);

            for (int i = 0; i < entryCount; i++)
            {
                byte[] entryBytes = new byte[8];
                Array.Copy(bytes, 520 + (i * 8), entryBytes, 0, 8);
                Entries.Add(new Error2Entry(entryBytes));
            }
        }

        /// <summary>
        /// Class to represent an Entry in the Error2 section.
        /// </summary>
        public class Error2Entry
        {
            /// <summary>
            /// The first sector that caused the error.
            /// </summary>
            public int FirstSector { get; private set; }

            /// <summary>
            /// The number of sectors that were ignored/blanked by the error.
            /// </summary>
            public int SectorCount { get; private set; }

            /// <summary>
            /// Creates a new Error2Entry object from bytes.
            /// </summary>
            /// <param name="bytes">The bytes that make up one entry in the Error2 section.</param>
            public Error2Entry(byte[] bytes)
            {
                FirstSector = BitConverter.ToInt32(bytes, 0);
                SectorCount = BitConverter.ToInt32(bytes, 4);

                // TODO: Adler32 check
            }
        }
    }
}

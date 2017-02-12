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
using System.Collections;
using System.Collections.Generic;

namespace DiscUtils.Ewf.Section
{
    /// <summary>
    /// The Table section from the EWF file.
    /// A Table provides a lookup for where in the E01 file the data for particular sectors is stored.
    /// </summary>
    public class Table
    {
        /// <summary>
        /// List of entries; each data chunk is stored in an entry.
        /// </summary>
        public List<TableEntry> Entries { get; set; }

        /// <summary>
        /// Creates an object to hold the Table section.
        /// </summary>
        /// <param name="bytes">The bytes from which to create Table section.</param>
        public Table(byte[] bytes)
        {
            int entryCount = BitConverter.ToInt32(bytes, 0);
            Entries = new List<TableEntry>(entryCount);

            for (int i = 0; i < entryCount; i++)
            {
                byte[] entryBytes = new byte[4];
                Array.Copy(bytes, 24 + (i * 4), entryBytes, 0, 4);
                Entries.Add(new TableEntry(entryBytes));
            }
        }

        /// <summary>
        /// Represents an Entry within the Table section.
        /// </summary>
        public class TableEntry
        {
            /// <summary>
            /// Indicates whether or not this chunk is compressed.
            /// </summary>
            public bool Compressed { get; private set; }

            /// <summary>
            /// Value, relative to the start of the EWF, where the data resides.
            /// </summary>
            public int Offset { get; private set; }

            /// <summary>
            /// Creates an object to hold a particular TableEntry.
            /// </summary>
            /// <param name="bytes">The bytes from which to make the TableEntry.</param>
            public TableEntry(byte[] bytes)
            {
                BitArray ba = new BitArray(bytes);
                Compressed = ba.Get(31);
                ba.Set(31, false);

                byte[] temp = new byte[4];
                ((ICollection)ba).CopyTo(temp, 0);                

                Offset = BitConverter.ToInt32(temp, 0);
            }
        }
    }
}

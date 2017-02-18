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
    /// The Session section from the EWF file.
    /// Represents an optical disc session.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// A list of Entry objects as read from the Session section.
        /// </summary>
        public List<SessionEntry> Entries { get; set; }

        /// <summary>
        /// An object to hold the Session section.
        /// </summary>
        /// <param name="bytes">The bytes that make up the Session section form the EWF file.</param>
        public Session(byte[] bytes)
        {
            int entryCount = BitConverter.ToInt32(bytes, 0);
            Entries = new List<SessionEntry>(entryCount);

            for (int i = 0; i < entryCount; i++)
            {
                byte[] entryBytes = new byte[32];
                Array.Copy(bytes, 36 + (i * 32), entryBytes, 0, 32);
                Entries.Add(new SessionEntry(entryBytes));
            }
        }

        /// <summary>
        /// An Entry in the in the Session section.
        /// </summary>
        public class SessionEntry
        {
            /// <summary>
            /// 0x01 for an audio track, else a data track.
            /// </summary>
            public int Flags { get; private set; }

            /// <summary>
            /// The first sector for the data. Seems to be 16, even though it's really 0.
            /// </summary>
            public int FirstSector { get; private set; }

            /// <summary>
            /// Creates an object to hold a particular session entry.
            /// </summary>
            /// <param name="bytes">The bytes from which to create this particular session entry.</param>
            public SessionEntry(byte[] bytes)
            {
                Flags = BitConverter.ToInt32(bytes, 0);
                FirstSector = BitConverter.ToInt32(bytes, 4);

                // TODO: Adler32 check
            }
        }
    }
}

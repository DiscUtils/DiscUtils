//
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Xfs
{
    using DiscUtils.Streams;
    using System;
    using System.IO;

    internal class ShortformDirectory : IByteArraySerializable
    {
        private bool _useShortInode;

        public byte Count4Bytes { get; private set; }

        public byte Count8Bytes { get; private set; }

        public ulong Parent { get; set; }

        public ShortformDirectoryEntry[] Entries { get; private set; }

        public int Size
        {
            get
            {
                var result = 0x6;
                foreach (var entry in Entries)
                {
                    result += entry.Size;
                }
                return result;
            }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Count4Bytes = buffer[offset];
            Count8Bytes = buffer[offset+0x1];
            byte count;
            _useShortInode = false;
            Parent = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x2);
            offset = offset + 0x6;
            if (Count4Bytes != 0)
            {
                _useShortInode = true;
                count = Count4Bytes;
            }
            else if (Count8Bytes != 0)
            {
                count = Count8Bytes;
            }
            else
            {
                count = 0;
            }
            Entries = new ShortformDirectoryEntry[count];
            for (int i = 0; i < count; i++)
            {
                var entry = new ShortformDirectoryEntry(_useShortInode);
                entry.ReadFrom(buffer, offset);
                offset += entry.Size;
                Entries[i] = entry;
            }
            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

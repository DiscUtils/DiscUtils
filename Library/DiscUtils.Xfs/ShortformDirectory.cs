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
        public SuperBlock superblock { get; private set; }

        public ShortformDirectory(SuperBlock sb)
        {
            superblock = sb;
            if ((((sb.SbVersion == 5) || ((sb.Version & 0x8000) != 0)) && ((sb.Features2 & 0x00000200) != 0)) ||
                ((sb.SbVersion == 5) && (sb.IncompatibleFeatures & 0x0001) != 0))
            {//has ftype in dir inode

                has_ftype = true;
            }
        }

        private bool _useShortInode;

        private bool has_ftype = false;

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
            _useShortInode = true;
            Parent = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x2);
            
            if (Count8Bytes != 0)
            {
                _useShortInode = false;
                count = Count8Bytes;
            }
            else if (Count4Bytes != 0)
            {
                count = Count4Bytes;
            }
            else
            {
                count = 0;
            }

            offset = offset + 10 - (_useShortInode?4:0);//sizeof(struct xfs_dir2_sf_hdr) - (i8count == 0) * (XFS_INO64_SIZE - XFS_INO32_SIZE);
            Entries = new ShortformDirectoryEntry[count];
            for (int i = 0; i < count; i++)
            {
                var entry = new ShortformDirectoryEntry(_useShortInode,has_ftype);
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

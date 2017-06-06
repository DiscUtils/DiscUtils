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

using System.Collections;

namespace DiscUtils.Xfs
{
    using DiscUtils.Streams;
    using System;

    internal class BTreeInodeRecord: IByteArraySerializable
    {
        /// <summary>
        /// specifies the starting inode number for the chunk
        /// </summary>
        public uint StartInode { get; private set; }

        /// <summary>
        /// specifies the number of free entries in the chuck
        /// </summary>
        public uint FreeCount { get; private set; }

        /// <summary>
        /// 64 element bit array specifying which entries are free in the chunk
        /// </summary>
        public BitArray Free { get; private set; }
        public int Size
        {
            get { return 0x10; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            StartInode = EndianUtilities.ToUInt32BigEndian(buffer, offset);
            FreeCount = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x4);
            Free = new BitArray(EndianUtilities.ToByteArray(buffer, offset + 0x8, 0x8));
            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

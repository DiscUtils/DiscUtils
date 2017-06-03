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

using System;
using DiscUtils.Streams;

namespace DiscUtils.SquashFs
{
    internal class ExtendedDirectoryInode : Inode, IDirectoryInode
    {
        private uint _extendedAttributes;
        private uint _fileSize;
        private ushort _indexCount;

        public override int Size
        {
            get { return 40; }
        }

        public override long FileSize
        {
            get { return _fileSize; }

            set
            {
                if (value > uint.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "File size greater than " + uint.MaxValue);
                }

                _fileSize = (uint)value;
            }
        }

        public uint StartBlock { get; private set; }

        public uint ParentInode { get; private set; }

        public ushort Offset { get; private set; }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            base.ReadFrom(buffer, offset);

            NumLinks = EndianUtilities.ToInt32LittleEndian(buffer, offset + 16);
            _fileSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 20);
            StartBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 24);
            ParentInode = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 28);
            _indexCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 32);
            Offset = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 34);
            _extendedAttributes = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 36);

            return 40;
        }
    }
}
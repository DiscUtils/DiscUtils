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
    internal class DirectoryInode : Inode, IDirectoryInode
    {
        private ushort _fileSize;

        public override int Size
        {
            get { return 32; }
        }

        public override long FileSize
        {
            get { return _fileSize; }

            set
            {
                if (value > ushort.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "File size greater than " + ushort.MaxValue);
                }

                _fileSize = (ushort)value;
            }
        }

        public uint StartBlock { get; set; }

        public uint ParentInode { get; set; }

        public ushort Offset { get; set; }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            base.ReadFrom(buffer, offset);

            StartBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 16);
            NumLinks = EndianUtilities.ToInt32LittleEndian(buffer, offset + 20);
            _fileSize = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 24);
            Offset = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 26);
            ParentInode = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 28);

            return 32;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            base.WriteTo(buffer, offset);

            EndianUtilities.WriteBytesLittleEndian(StartBlock, buffer, offset + 16);
            EndianUtilities.WriteBytesLittleEndian(NumLinks, buffer, offset + 20);
            EndianUtilities.WriteBytesLittleEndian(_fileSize, buffer, offset + 24);
            EndianUtilities.WriteBytesLittleEndian(Offset, buffer, offset + 26);
            EndianUtilities.WriteBytesLittleEndian(ParentInode, buffer, offset + 28);
        }
    }
}
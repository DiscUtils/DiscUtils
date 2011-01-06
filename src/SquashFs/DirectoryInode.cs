//
// Copyright (c) 2008-2010, Kenneth Bell
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

namespace DiscUtils.SquashFs
{
    internal class DirectoryInode : Inode, IDirectoryInode
    {
        private uint _startBlock;
        private uint _numLinks;
        private ushort _fileSize;
        private ushort _offset;
        private uint _parentInode;

        public override int Size
        {
            get { return 32; }
        }

        public uint NumLinks
        {
            get { return _numLinks; }
        }

        public uint FileSize
        {
            get { return _fileSize; }
        }

        public uint StartBlock
        {
            get { return _startBlock; }
        }

        public uint ParentInode
        {
            get { return _parentInode; }
        }

        public ushort Offset
        {
            get { return _offset; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            base.ReadFrom(buffer, offset);

            _startBlock = Utilities.ToUInt32LittleEndian(buffer, offset + 16);
            _numLinks = Utilities.ToUInt32LittleEndian(buffer, offset + 20);
            _fileSize = Utilities.ToUInt16LittleEndian(buffer, offset + 24);
            _offset = Utilities.ToUInt16LittleEndian(buffer, offset + 26);
            _parentInode = Utilities.ToUInt32LittleEndian(buffer, offset + 28);

            return 32;
        }
    }
}

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

using DiscUtils.Streams;

namespace DiscUtils.Xfs
{
    internal class BlockDirectoryDataEntry : BlockDirectoryData, IDirectoryEntry
    {
        private readonly bool _ftype;

        public ulong Inode { get; private set; }

        public byte NameLength { get; private set; }

        public byte[] Name { get; private set; }

        public ushort Tag { get; private set; }

        public DirectoryFType FType { get; private set; }

        public override int Size
        {
            get
            {
                var size = 0xb + NameLength + (_ftype?1:0);
                var padding = size%8;
                if (padding != 0)
                    return size + (8 - padding);
                return size;
            }
        }

        public BlockDirectoryDataEntry(Context context)
        {
            _ftype = context.SuperBlock.HasFType;
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Inode = EndianUtilities.ToUInt64BigEndian(buffer, offset);
            NameLength = buffer[offset + 0x8];
            Name = EndianUtilities.ToByteArray(buffer, offset + 0x9, NameLength);
            offset += 0x9 + NameLength;
            if (_ftype)
            {
                FType = (DirectoryFType)buffer[offset];
                offset++;
            }

            var padding = 6 - ((NameLength + (_ftype ? 2 : 1)) % 8);
            if (padding < 0)
                padding += 8;
            offset += padding;
            Tag = EndianUtilities.ToUInt16BigEndian(buffer, offset);
            return Size;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Inode}: {EndianUtilities.BytesToString(Name, 0, NameLength)}";
        }
    }
}

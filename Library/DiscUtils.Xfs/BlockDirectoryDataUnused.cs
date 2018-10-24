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
    internal class BlockDirectoryDataUnused : BlockDirectoryData
    {
        public ushort Freetag { get; private set; }

        public ushort Length { get; private set; }

        public ushort Tag { get; private set; }

        public SuperBlock SuperBlock { get; private set; }

        public bool sb_ok = false;

        public BlockDirectoryDataUnused(SuperBlock sb)
        {
            SuperBlock = sb;
            sb_ok = true;
        }

        public override int Size { get { return Length; } }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Freetag = EndianUtilities.ToUInt16BigEndian(buffer, offset);
            Length = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x2);
            Tag = EndianUtilities.ToUInt16BigEndian(buffer, offset + Length - 0x2);
            return Size;
        }
    }
}

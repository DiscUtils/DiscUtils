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

using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Registry
{
    internal sealed class BinHeader : IByteArraySerializable
    {
        public const int HeaderSize = 0x20;

        private const uint Signature = 0x6E696268;
        public int BinSize;

        public int FileOffset;

        public int Size
        {
            get { return HeaderSize; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            uint sig = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
            if (sig != Signature)
            {
                throw new IOException("Invalid signature for registry bin");
            }

            FileOffset = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x04);
            BinSize = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x08);
            long unknown = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x0C);
            long unknown1 = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x14);
            int unknown2 = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x1C);
            return HeaderSize;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(Signature, buffer, offset + 0x00);
            EndianUtilities.WriteBytesLittleEndian(FileOffset, buffer, offset + 0x04);
            EndianUtilities.WriteBytesLittleEndian(BinSize, buffer, offset + 0x08);
        }
    }
}
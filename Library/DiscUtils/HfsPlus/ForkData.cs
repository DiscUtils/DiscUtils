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

namespace DiscUtils.HfsPlus
{
    internal sealed class ForkData : IByteArraySerializable
    {
        public const int StructSize = 80;

        public ulong LogicalSize;
        public uint ClumpSize;
        public uint TotalBlocks;
        public ExtentDescriptor[] Extents;

        public int Size
        {
            get { return StructSize; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            LogicalSize = Utilities.ToUInt64BigEndian(buffer, offset + 0);
            ClumpSize = Utilities.ToUInt32BigEndian(buffer, offset + 8);
            TotalBlocks = Utilities.ToUInt32BigEndian(buffer, offset + 12);

            Extents = new ExtentDescriptor[8];
            for (int i = 0; i < 8; ++i)
            {
                Extents[i] = Utilities.ToStruct<ExtentDescriptor>(buffer, offset + 16 + (i * 8));
            }

            return StructSize;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new System.NotImplementedException();
        }
    }
}

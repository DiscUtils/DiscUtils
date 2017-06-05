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

namespace DiscUtils.Udf
{
    internal sealed class ShortAllocationDescriptor : IByteArraySerializable
    {
        public uint ExtentLength;
        public uint ExtentLocation;
        public ShortAllocationFlags Flags;

        public int Size
        {
            get { return 8; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            uint len = EndianUtilities.ToUInt32LittleEndian(buffer, offset);
            ExtentLocation = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);

            ExtentLength = len & 0x3FFFFFFF;
            Flags = (ShortAllocationFlags)((len >> 30) & 0x3);

            return 8;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return ExtentLocation + ":+" + ExtentLength + " [" + Flags + "]";
        }
    }
}
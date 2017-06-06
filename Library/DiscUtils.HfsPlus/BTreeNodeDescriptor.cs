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

namespace DiscUtils.HfsPlus
{
    internal sealed class BTreeNodeDescriptor : IByteArraySerializable
    {
        public uint BackwardLink;
        public uint ForwardLink;
        public byte Height;
        public BTreeNodeKind Kind;
        public ushort NumRecords;
        public ushort Reserved;

        public int Size
        {
            get { return 14; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            ForwardLink = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0);
            BackwardLink = EndianUtilities.ToUInt32BigEndian(buffer, offset + 4);
            Kind = (BTreeNodeKind)buffer[offset + 8];
            Height = buffer[offset + 9];
            NumRecords = EndianUtilities.ToUInt16BigEndian(buffer, offset + 10);
            Reserved = EndianUtilities.ToUInt16BigEndian(buffer, offset + 12);

            return 14;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
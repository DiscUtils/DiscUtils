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
    using System;

    internal class BTreeHeaderRecord : BTreeNodeRecord
    {
        public ushort TreeDepth;
        public uint RootNode;
        public uint NumLeafRecords;
        public uint FirstLeafNode;
        public uint LastLeafNode;
        public ushort NodeSize;
        public ushort MaxKeyLength;
        public uint TotalNodes;
        public uint FreeNodes;
        public ushort Res1;
        public uint ClumpSize;
        public byte TreeType;
        public byte KeyCompareType;
        public uint Attributes;

        public override int Size
        {
            get { return 104; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            TreeDepth = Utilities.ToUInt16BigEndian(buffer, offset + 0);
            RootNode = Utilities.ToUInt32BigEndian(buffer, offset + 2);
            NumLeafRecords = Utilities.ToUInt32BigEndian(buffer, offset + 6);
            FirstLeafNode = Utilities.ToUInt32BigEndian(buffer, offset + 10);
            LastLeafNode = Utilities.ToUInt32BigEndian(buffer, offset + 14);
            NodeSize = Utilities.ToUInt16BigEndian(buffer, offset + 18);
            MaxKeyLength = Utilities.ToUInt16BigEndian(buffer, offset + 20);
            TotalNodes = Utilities.ToUInt16BigEndian(buffer, offset + 22);
            FreeNodes = Utilities.ToUInt32BigEndian(buffer, offset + 24);
            Res1 = Utilities.ToUInt16BigEndian(buffer, offset + 28);
            ClumpSize = Utilities.ToUInt32BigEndian(buffer, offset + 30);
            TreeType = buffer[offset + 34];
            KeyCompareType = buffer[offset + 35];
            Attributes = Utilities.ToUInt32BigEndian(buffer, offset + 36);

            return 104;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

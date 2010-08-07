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


namespace DiscUtils.Iscsi
{
    internal class ScsiReadCapacityResponse : ScsiResponse
    {
        private bool _truncated;

        private uint _numLogicalBlocks;
        private uint _logicalBlockSize;

        public uint NumLogicalBlocks
        {
            get { return _numLogicalBlocks; }
        }

        public uint LogicalBlockSize
        {
            get { return _logicalBlockSize; }
        }

        public override void ReadFrom(byte[] buffer, int offset, int count)
        {
            if (count < 8)
            {
                _truncated = true;
                return;
            }

            _numLogicalBlocks = Utilities.ToUInt32BigEndian(buffer, offset);
            _logicalBlockSize = Utilities.ToUInt32BigEndian(buffer, offset + 4);
        }

        public override bool Truncated
        {
            get { return _truncated; }
        }

        public override uint NeededDataLength
        {
            get { return 8; }
        }
    }
}

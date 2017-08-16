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

using System.Collections.Generic;
using DiscUtils.Streams;

namespace DiscUtils.Iscsi
{
    internal class ScsiReportLunsResponse : ScsiResponse
    {
        private uint _availableLuns;

        public List<ulong> Luns { get; private set; }

        public override uint NeededDataLength
        {
            get { return _availableLuns * 8 + 8; }
        }

        public override bool Truncated
        {
            get { return _availableLuns != Luns.Count; }
        }

        public override void ReadFrom(byte[] buffer, int offset, int count)
        {
            Luns = new List<ulong>();

            if (count == 0)
            {
                return;
            }

            if (count < 8)
            {
                throw new InvalidProtocolException("Data truncated too far");
            }

            _availableLuns = EndianUtilities.ToUInt32BigEndian(buffer, offset) / 8;
            int pos = 8;
            while (pos <= count - 8 && Luns.Count < _availableLuns)
            {
                Luns.Add(EndianUtilities.ToUInt64BigEndian(buffer, offset + pos));
                pos += 8;
            }
        }
    }
}
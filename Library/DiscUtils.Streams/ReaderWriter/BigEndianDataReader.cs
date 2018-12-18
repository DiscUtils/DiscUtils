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
using System.IO;

namespace DiscUtils.Streams
{
    public class BigEndianDataReader : DataReader
    {
        public BigEndianDataReader(Stream stream)
            : base(stream) {}

        public override ushort ReadUInt16()
        {
            ReadToBuffer(sizeof(UInt16));
            return EndianUtilities.ToUInt16BigEndian(_buffer, 0);
        }

        public override int ReadInt32()
        {
            ReadToBuffer(sizeof(Int32));
            return EndianUtilities.ToInt32BigEndian(_buffer, 0);
        }

        public override uint ReadUInt32()
        {
            ReadToBuffer(sizeof(UInt32));
            return EndianUtilities.ToUInt32BigEndian(_buffer, 0);
        }

        public override long ReadInt64()
        {
            ReadToBuffer(sizeof(Int64));
            return EndianUtilities.ToInt64BigEndian(_buffer, 0);
        }

        public override ulong ReadUInt64()
        {
            ReadToBuffer(sizeof(UInt64));
            return EndianUtilities.ToUInt64BigEndian(_buffer, 0);
        }
    }
}
//
// Copyright (c) 2014, Quamotion
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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.HfsPlus
{
    internal class CompressionResourceHeader
    {
        private uint _headerSize;
        private uint _totalSize;
        private uint _dataSize;
        private uint _flags;

        public int ReadFrom(byte[] buffer, int offset)
        {
            _headerSize = Utilities.ToUInt32BigEndian(buffer, offset);
            _totalSize = Utilities.ToUInt32BigEndian(buffer, offset + 4);
            _dataSize = Utilities.ToUInt32BigEndian(buffer, offset + 8);
            _flags = Utilities.ToUInt32BigEndian(buffer, offset + 12);

            return Size;
        }

        public static int Size
        {
            get
            {
                return 16;
            }
        }

        public uint HeaderSize
        {
            get { return _headerSize; }
        }

        public uint TotalSize
        {
            get { return _totalSize; }
        }

        public uint DataSize
        {
            get { return _dataSize; }
        }

        public uint Flags
        {
            get { return _flags; }
        }
    }
}

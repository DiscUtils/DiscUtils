//
// Copyright (c) 2008-2012, Kenneth Bell
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

namespace DiscUtils.Vhdx
{
    internal struct BatEntry : IByteArraySerializable
    {
        private ulong _value;

        public BatEntry(byte[] buffer, int offset)
        {
            _value = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
        }

        public PayloadBlockStatus PayloadBlockStatus
        {
            get { return (PayloadBlockStatus)(_value & 0x7); }
            set { _value = (_value & ~0x7ul) | (ulong)value; }
        }

        public bool BitmapBlockPresent
        {
            get { return (_value & 0x7) == 6; }
            set { _value = (_value & ~0x7ul) | (value ? 6u : 0u); }
        }

        public long FileOffsetMB
        {
            get { return (long)(_value >> 20) & 0xFFFFFFFFFFFL; }
            set { _value = (_value & 0xFFFFF) | (ulong)value << 20; }
        }

        public int Size
        {
            get { return 8; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            _value = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            return 8;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(_value, buffer, offset);
        }
    }
}
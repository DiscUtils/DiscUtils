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
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Nfs
{
    public sealed class XdrDataReader : BigEndianDataReader
    {
        public XdrDataReader(Stream stream)
            : base(stream) {}

        public bool ReadBool()
        {
            return ReadUInt32() != 0;
        }

        public override byte[] ReadBytes(int count)
        {
            byte[] buffer = StreamUtilities.ReadExact(_stream, count);

            if ((count & 0x3) != 0)
            {
                StreamUtilities.ReadExact(_stream, 4 - (count & 0x3));
            }

            return buffer;
        }

        public byte[] ReadBuffer()
        {
            uint length = ReadUInt32();
            return ReadBytes((int)length);
        }

        public byte[] ReadBuffer(uint maxLength)
        {
            uint length = ReadUInt32();
            if (length <= maxLength)
            {
                return ReadBytes((int)length);
            }

            throw new IOException("Attempt to read buffer that is too long");
        }

        public string ReadString()
        {
            byte[] data = ReadBuffer();
            return Encoding.ASCII.GetString(data);
        }

        public string ReadString(uint maxLength)
        {
            byte[] data = ReadBuffer(maxLength);
            return Encoding.ASCII.GetString(data);
        }
    }
}
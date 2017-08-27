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
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal class DescriptorTag : IByteArraySerializable
    {
        public ushort DescriptorCrc;
        public ushort DescriptorCrcLength;
        public ushort DescriptorVersion;
        public byte TagChecksum;
        public TagIdentifier TagIdentifier;
        public uint TagLocation;
        public ushort TagSerialNumber;

        public int Size
        {
            get { return 16; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            TagIdentifier = (TagIdentifier)EndianUtilities.ToUInt16LittleEndian(buffer, offset);
            DescriptorVersion = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 2);
            TagChecksum = buffer[offset + 4];
            TagSerialNumber = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 6);
            DescriptorCrc = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 8);
            DescriptorCrcLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 10);
            TagLocation = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 12);

            return 16;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public static bool IsValid(byte[] buffer, int offset)
        {
            byte checkSum = 0;

            if (EndianUtilities.ToUInt16LittleEndian(buffer, offset) == 0)
            {
                return false;
            }

            for (int i = 0; i < 4; ++i)
            {
                checkSum += buffer[offset + i];
            }

            for (int i = 5; i < 16; ++i)
            {
                checkSum += buffer[offset + i];
            }

            return checkSum == buffer[offset + 4];
        }

        public static bool TryFromStream(Stream stream, out DescriptorTag result)
        {
            byte[] next = StreamUtilities.ReadExact(stream, 512);
            if (!IsValid(next, 0))
            {
                result = null;
                return false;
            }

            DescriptorTag dt = new DescriptorTag();
            dt.ReadFrom(next, 0);

            result = dt;
            return true;
        }
    }
}
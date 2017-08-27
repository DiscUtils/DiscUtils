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
using DiscUtils.Streams;

namespace DiscUtils.Iscsi
{
    internal class ProtocolDataUnit
    {
        public ProtocolDataUnit(byte[] headerData, byte[] contentData)
        {
            HeaderData = headerData;
            ContentData = contentData;
        }

        public byte[] ContentData { get; }

        public byte[] HeaderData { get; }

        public OpCode OpCode
        {
            get { return (OpCode)(HeaderData[0] & 0x3F); }
        }

        public static ProtocolDataUnit ReadFrom(Stream stream, bool headerDigestEnabled, bool dataDigestEnabled)
        {
            int numRead = 0;

            byte[] headerData = StreamUtilities.ReadExact(stream, 48);
            numRead += 48;

            byte[] contentData = null;

            if (headerDigestEnabled)
            {
                uint digest = ReadDigest(stream);
                numRead += 4;
            }

            BasicHeaderSegment bhs = new BasicHeaderSegment();
            bhs.ReadFrom(headerData, 0);

            if (bhs.DataSegmentLength > 0)
            {
                contentData = StreamUtilities.ReadExact(stream, bhs.DataSegmentLength);
                numRead += bhs.DataSegmentLength;

                if (dataDigestEnabled)
                {
                    uint digest = ReadDigest(stream);
                    numRead += 4;
                }
            }

            int rem = 4 - numRead % 4;
            if (rem != 4)
            {
                StreamUtilities.ReadExact(stream, rem);
            }

            return new ProtocolDataUnit(headerData, contentData);
        }

        private static uint ReadDigest(Stream stream)
        {
            byte[] data = StreamUtilities.ReadExact(stream, 4);
            return EndianUtilities.ToUInt32BigEndian(data, 0);
        }
    }
}
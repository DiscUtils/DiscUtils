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

using System.IO;

namespace DiscUtils.Iscsi
{
    internal class ProtocolDataUnit
    {
        private byte[] _headerData;
        private byte[] _contentData;

        public ProtocolDataUnit(byte[] headerData, byte[] contentData)
        {
            _headerData = headerData;
            _contentData = contentData;
        }

        public OpCode OpCode
        {
            get { return (OpCode)(_headerData[0] & 0x3F); }
        }

        public byte[] HeaderData
        {
            get { return _headerData; }
        }

        public byte[] ContentData
        {
            get { return _contentData; }
        }

        public static ProtocolDataUnit ReadFrom(Stream stream, bool headerDigestEnabled, bool dataDigestEnabled)
        {
            int numRead = 0;

            byte[] headerData = Utilities.ReadFully(stream, 48);
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
                contentData = Utilities.ReadFully(stream, bhs.DataSegmentLength);
                numRead += bhs.DataSegmentLength;

                if (dataDigestEnabled)
                {
                    uint digest = ReadDigest(stream);
                    numRead += 4;
                }
            }

            int rem = 4 - (numRead % 4);
            if (rem != 4)
            {
                Utilities.ReadFully(stream, rem);
            }

            return new ProtocolDataUnit(headerData, contentData);
        }

        private static uint ReadDigest(Stream stream)
        {
            byte[] data = Utilities.ReadFully(stream, 4);
            return Utilities.ToUInt32BigEndian(data, 0);
        }
    }
}

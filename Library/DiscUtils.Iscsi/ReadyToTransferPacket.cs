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

using DiscUtils.Streams;

namespace DiscUtils.Iscsi
{
    internal class ReadyToTransferPacket : BaseResponse
    {
        public uint BufferOffset;
        public uint DesiredTransferLength;
        public BasicHeaderSegment Header;
        public ulong Lun;
        public uint ReadyToTransferSequenceNumber;
        public uint TargetTransferTag;

        public override void Parse(ProtocolDataUnit pdu)
        {
            Parse(pdu.HeaderData, 0);
        }

        public void Parse(byte[] headerData, int headerOffset)
        {
            StatusPresent = false;

            Header = new BasicHeaderSegment();
            Header.ReadFrom(headerData, headerOffset);

            if (Header.OpCode != OpCode.ReadyToTransfer)
            {
                throw new InvalidProtocolException("Invalid opcode in response, expected " + OpCode.ReadyToTransfer +
                                                   " was " + Header.OpCode);
            }

            Lun = EndianUtilities.ToUInt64BigEndian(headerData, headerOffset + 8);
            TargetTransferTag = EndianUtilities.ToUInt32BigEndian(headerData, headerOffset + 20);
            StatusSequenceNumber = EndianUtilities.ToUInt32BigEndian(headerData, headerOffset + 24);
            ExpectedCommandSequenceNumber = EndianUtilities.ToUInt32BigEndian(headerData, headerOffset + 28);
            MaxCommandSequenceNumber = EndianUtilities.ToUInt32BigEndian(headerData, headerOffset + 32);
            ReadyToTransferSequenceNumber = EndianUtilities.ToUInt32BigEndian(headerData, headerOffset + 36);
            BufferOffset = EndianUtilities.ToUInt32BigEndian(headerData, headerOffset + 40);
            DesiredTransferLength = EndianUtilities.ToUInt32BigEndian(headerData, headerOffset + 44);
        }
    }
}
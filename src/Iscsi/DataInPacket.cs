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
    internal class DataInPacket : BaseResponse
    {
        public BasicHeaderSegment Header;
        public bool Acknowledge;
        public bool O;
        public bool U;

        public ScsiStatus Status;
        public ulong Lun;
        public uint TargetTransferTag;

        public uint DataSequenceNumber;
        public uint BufferOffset;
        public uint ResidualCount;
        public byte[] ReadData;

        public override void Parse(ProtocolDataUnit pdu)
        {
            Parse(pdu.HeaderData, 0, pdu.ContentData);
        }

        public void Parse(byte[] headerData, int headerOffset, byte[] bodyData)
        {
            Header = new BasicHeaderSegment();
            Header.ReadFrom(headerData, headerOffset);

            if (Header.OpCode != OpCode.ScsiDataIn)
            {
                throw new InvalidProtocolException("Invalid opcode in response, expected " + OpCode.ScsiDataIn + " was " + Header.OpCode);
            }

            UnpackFlags(headerData[headerOffset + 1]);
            if (StatusPresent)
            {
                Status = (ScsiStatus)headerData[headerOffset + 3];
            }
            Lun = Utilities.ToUInt64BigEndian(headerData, headerOffset + 8);
            TargetTransferTag = Utilities.ToUInt32BigEndian(headerData, headerOffset + 20);
            StatusSequenceNumber = Utilities.ToUInt32BigEndian(headerData, headerOffset + 24);
            ExpectedCommandSequenceNumber = Utilities.ToUInt32BigEndian(headerData, headerOffset + 28);
            MaxCommandSequenceNumber = Utilities.ToUInt32BigEndian(headerData, headerOffset + 32);
            DataSequenceNumber = Utilities.ToUInt32BigEndian(headerData, headerOffset + 36);
            BufferOffset = Utilities.ToUInt32BigEndian(headerData, headerOffset + 40);
            ResidualCount = Utilities.ToUInt32BigEndian(headerData, headerOffset + 44);

            ReadData = bodyData;
        }

        private void UnpackFlags(byte value)
        {
            Acknowledge = (value & 0x40) != 0;
            O = (value & 0x04) != 0;
            U = (value & 0x02) != 0;
            StatusPresent = (value & 0x01) != 0;
        }
    }
}

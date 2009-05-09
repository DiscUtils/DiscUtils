//
// Copyright (c) 2009, Kenneth Bell
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

namespace DiscUtils.Iscsi
{
    internal enum TaskAttributes
    {
        Untagged = 0,
        Simple = 1,
        Ordered = 2,
        HeadOfQueue = 3,
        Aca = 4
    }

    internal class ScsiCommandRequest
    {
        private Connection _connection;

        private ulong _lun;

        public ScsiCommandRequest(Connection connection, ulong lun)
        {
            _connection = connection;
            _lun = lun;
        }

        public byte[] GetBytes(ScsiCommand cmd, byte[] data, int offset, int count, bool isFinalData, bool expectInput, bool expectOutput)
        {
            BasicHeaderSegment _basicHeader = new BasicHeaderSegment();
            _basicHeader.Immediate = cmd.ImmediateDelivery;
            _basicHeader.OpCode = OpCode.ScsiCommand;
            _basicHeader.FinalPdu = isFinalData;
            _basicHeader.TotalAhsLength = 0;
            _basicHeader.DataSegmentLength = count;
            _basicHeader.InitiatorTaskTag = _connection.Session.NextTaskTag();

            byte[] buffer = new byte[48 + Utilities.RoundUp(count, 4)];
            _basicHeader.WriteTo(buffer, 0);
            buffer[1] = PackAttrByte(isFinalData, expectInput, expectOutput, cmd.TaskAttributes);
            Utilities.WriteBytesBigEndian(_lun, buffer, 8);
            Utilities.WriteBytesBigEndian(cmd.ExpectedResponseDataLength, buffer, 20);
            Utilities.WriteBytesBigEndian(_connection.Session.NextCommandSequenceNumber(), buffer, 24);
            Utilities.WriteBytesBigEndian(_connection.ExpectedStatusSequenceNumber, buffer, 28);
            cmd.WriteTo(buffer, 32);

            if (data != null && count != 0)
            {
                Array.Copy(data, offset, buffer, 48, count);
            }

            return buffer;
        }

        public byte PackAttrByte(bool isFinalData, bool expectInput, bool expectOutput, TaskAttributes taskAttr)
        {
            byte value = 0;

            if (isFinalData) value |= 0x80;
            if (expectInput) value |= 0x40;
            if (expectOutput) value |= 0x20;
            value |= (byte)((int)taskAttr & 0x3);

            return value;
        }
    }
}

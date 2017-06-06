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
using DiscUtils.Streams;

namespace DiscUtils.Iscsi
{
    internal class TextRequest
    {
        private uint _commandSequenceNumber; // Per-session
        private readonly Connection _connection;

        private bool _continue;
        private uint _expectedStatusSequenceNumber; // Per-connection (ack)
        private ulong _lun;
        private readonly uint _targetTransferTag = 0xFFFFFFFF;

        public TextRequest(Connection connection)
        {
            _connection = connection;
        }

        public byte[] GetBytes(ulong lun, byte[] data, int offset, int count, bool isFinalData)
        {
            BasicHeaderSegment _basicHeader = new BasicHeaderSegment();
            _basicHeader.Immediate = true;
            _basicHeader.OpCode = OpCode.TextRequest;
            _basicHeader.FinalPdu = isFinalData;
            _basicHeader.TotalAhsLength = 0;
            _basicHeader.DataSegmentLength = count;
            _basicHeader.InitiatorTaskTag = _connection.Session.CurrentTaskTag;

            _continue = !isFinalData;
            _lun = lun;
            _commandSequenceNumber = _connection.Session.CommandSequenceNumber;
            _expectedStatusSequenceNumber = _connection.ExpectedStatusSequenceNumber;

            byte[] buffer = new byte[MathUtilities.RoundUp(48 + count, 4)];
            _basicHeader.WriteTo(buffer, 0);
            buffer[1] |= (byte)(_continue ? 0x40 : 0x00);
            EndianUtilities.WriteBytesBigEndian(lun, buffer, 8);
            EndianUtilities.WriteBytesBigEndian(_targetTransferTag, buffer, 20);
            EndianUtilities.WriteBytesBigEndian(_commandSequenceNumber, buffer, 24);
            EndianUtilities.WriteBytesBigEndian(_expectedStatusSequenceNumber, buffer, 28);
            Array.Copy(data, offset, buffer, 48, count);
            return buffer;
        }
    }
}
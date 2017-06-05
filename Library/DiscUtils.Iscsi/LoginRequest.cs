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
    internal class LoginRequest
    {
        private const ushort IsidQualifier = 0x0000;

        private BasicHeaderSegment _basicHeader;
        private uint _commandSequenceNumber; // Per-session

        private readonly Connection _connection;
        private ushort _connectionId;
        private bool _continue;
        private LoginStages _currentStage;
        private uint _expectedStatusSequenceNumber; // Per-connection (ack)
        private LoginStages _nextStage;

        private bool _transit;

        public LoginRequest(Connection connection)
        {
            _connection = connection;
        }

        public byte[] GetBytes(byte[] data, int offset, int count, bool isFinalData)
        {
            _basicHeader = new BasicHeaderSegment();
            _basicHeader.Immediate = true;
            _basicHeader.OpCode = OpCode.LoginRequest;
            _basicHeader.FinalPdu = isFinalData;
            _basicHeader.TotalAhsLength = 0;
            _basicHeader.DataSegmentLength = count;
            _basicHeader.InitiatorTaskTag = _connection.Session.CurrentTaskTag;

            _transit = isFinalData;
            _continue = !isFinalData;
            _currentStage = _connection.CurrentLoginStage;
            if (_transit)
            {
                _nextStage = _connection.NextLoginStage;
            }

            _connectionId = _connection.Id;
            _commandSequenceNumber = _connection.Session.CommandSequenceNumber;
            _expectedStatusSequenceNumber = _connection.ExpectedStatusSequenceNumber;

            byte[] buffer = new byte[MathUtilities.RoundUp(48 + count, 4)];
            _basicHeader.WriteTo(buffer, 0);
            buffer[1] = PackState();
            buffer[2] = 0; // Max Version
            buffer[3] = 0; // Min Version
            EndianUtilities.WriteBytesBigEndian(_connection.Session.InitiatorSessionId, buffer, 8);
            EndianUtilities.WriteBytesBigEndian(IsidQualifier, buffer, 12);
            EndianUtilities.WriteBytesBigEndian(_connection.Session.TargetSessionId, buffer, 14);
            EndianUtilities.WriteBytesBigEndian(_connectionId, buffer, 20);
            EndianUtilities.WriteBytesBigEndian(_commandSequenceNumber, buffer, 24);
            EndianUtilities.WriteBytesBigEndian(_expectedStatusSequenceNumber, buffer, 28);
            Array.Copy(data, offset, buffer, 48, count);
            return buffer;
        }

        private byte PackState()
        {
            byte val = 0;

            if (_transit)
            {
                val |= 0x80;
            }

            if (_continue)
            {
                val |= 0x40;
            }

            val |= (byte)((int)_currentStage << 2);
            val |= (byte)_nextStage;

            return val;
        }
    }
}
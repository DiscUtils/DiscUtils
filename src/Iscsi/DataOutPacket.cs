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

using System;

namespace DiscUtils.Iscsi
{
    internal class DataOutPacket
    {
        private Connection _connection;

        private ulong _lun;

        public DataOutPacket(Connection connection, ulong lun)
        {
            _connection = connection;
            _lun = lun;
        }

        public byte[] GetBytes(byte[] data, int offset, int count, bool isFinalData, int dataSeqNumber, uint bufferOffset, uint targetTransferTag)
        {
            BasicHeaderSegment _basicHeader = new BasicHeaderSegment();
            _basicHeader.Immediate = false;
            _basicHeader.OpCode = OpCode.ScsiDataOut;
            _basicHeader.FinalPdu = isFinalData;
            _basicHeader.TotalAhsLength = 0;
            _basicHeader.DataSegmentLength = count;
            _basicHeader.InitiatorTaskTag = _connection.Session.CurrentTaskTag;

            byte[] buffer = new byte[48 + Utilities.RoundUp(count, 4)];
            _basicHeader.WriteTo(buffer, 0);
            buffer[1] = (byte)(isFinalData ? 0x80 : 0x00);
            Utilities.WriteBytesBigEndian(_lun, buffer, 8);
            Utilities.WriteBytesBigEndian(targetTransferTag, buffer, 20);
            Utilities.WriteBytesBigEndian(_connection.ExpectedStatusSequenceNumber, buffer, 28);
            Utilities.WriteBytesBigEndian(dataSeqNumber, buffer, 36);
            Utilities.WriteBytesBigEndian(bufferOffset, buffer, 40);

            Array.Copy(data, offset, buffer, 48, count);

            return buffer;
        }

    }
}

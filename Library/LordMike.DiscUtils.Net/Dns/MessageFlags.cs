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

namespace DiscUtils.Net.Dns
{
    internal struct MessageFlags
    {
        public MessageFlags(ushort value)
        {
            Value = value;
        }

        public MessageFlags(
            bool isResponse,
            OpCode opCode,
            bool isAuthorative,
            bool isTruncated,
            bool recursionDesired,
            bool recursionAvailable,
            ResponseCode responseCode)
        {
            int val = 0;
            val |= isResponse ? 0x8000 : 0x0000;
            val |= ((int)opCode & 0xF) << 11;
            val |= isAuthorative ? 0x0400 : 0x0000;
            val |= isTruncated ? 0x0200 : 0x0000;
            val |= recursionDesired ? 0x0100 : 0x0000;
            val |= recursionAvailable ? 0x0080 : 0x0000;
            val |= (int)responseCode & 0xF;

            Value = (ushort)val;
        }

        public ushort Value { get; }

        public bool IsResponse
        {
            get { return (Value & 0x8000) != 0; }
        }

        public OpCode OpCode
        {
            get { return (OpCode)((Value >> 11) & 0xF); }
        }

        public bool IsAuthorative
        {
            get { return (Value & 0x0400) != 0; }
        }

        public bool IsTruncated
        {
            get { return (Value & 0x0200) != 0; }
        }

        public bool RecursionDesired
        {
            get { return (Value & 0x0100) != 0; }
        }

        public bool RecursionAvailable
        {
            get { return (Value & 0x0080) != 0; }
        }

        public ResponseCode ResponseCode
        {
            get { return (ResponseCode)(Value & 0x000F); }
        }
    }
}
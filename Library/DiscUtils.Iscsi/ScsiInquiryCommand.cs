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
    internal class ScsiInquiryCommand : ScsiCommand
    {
        public const int InitialResponseDataLength = 36;

        private readonly bool _askForPage = false;
        private readonly uint _expected;
        private readonly byte _pageCode = 0;

        public ScsiInquiryCommand(ulong targetLun, uint expected)
            : base(targetLun)
        {
            _expected = expected;
        }

        public override int Size
        {
            get { return 6; }
        }

        ////public ScsiInquiryCommand(ulong targetLun, byte pageCode, uint expected)
        ////    : base(targetLun)
        ////{
        ////    _askForPage = true;
        ////    _pageCode = pageCode;
        ////    _expected = expected;
        ////}

        public override TaskAttributes TaskAttributes
        {
            get { return TaskAttributes.Untagged; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            Array.Clear(buffer, offset, 10);
            buffer[offset] = 0x12; // OpCode
            buffer[offset + 1] = (byte)(_askForPage ? 0x01 : 0x00);
            buffer[offset + 2] = _pageCode;
            EndianUtilities.WriteBytesBigEndian((ushort)_expected, buffer, offset + 3);
            buffer[offset + 5] = 0;
        }
    }
}
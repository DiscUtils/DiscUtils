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
    internal class ScsiRawCommand : ScsiCommand
    {
        private byte[] _buffer;
        private int _offset;
        private int _length;

        public ScsiRawCommand(ulong targetLun, byte[] buffer, int offset, int length)
            : base(targetLun)
        {
            _buffer = buffer;
            _offset = offset;
            _length = length;
        }

        public override TaskAttributes TaskAttributes
        {
            get { return TaskAttributes.Simple; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            Array.Copy(_buffer, _offset, buffer, offset, _length);
        }

        public override int Size
        {
            get { return _length; }
        }
    }
}

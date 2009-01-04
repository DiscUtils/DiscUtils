//
// Copyright (c) 2008-2009, Kenneth Bell
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

using System.Globalization;
using System.IO;

namespace DiscUtils.Ntfs
{
    internal class DataRun
    {
        private long _runLength;
        private long _runOffset;
        private bool _isSparse;

        public long RunLength
        {
            get { return _runLength; }
        }

        public long RunOffset
        {
            get { return _runOffset; }
        }

        public bool IsSparse
        {
            get { return _isSparse; }
        }

        public int Read(byte[] buffer, int offset)
        {
            int runOffsetSize = (buffer[offset] >> 4) & 0x0F;
            int runLengthSize = buffer[offset] & 0x0F;

            _runLength = (long)ReadVarULong(buffer, offset + 1, runLengthSize);
            _runOffset = ReadVarLong(buffer, offset + 1 + runLengthSize, runOffsetSize);
            _isSparse = (runOffsetSize == 0);

            return 1 + runLengthSize + runOffsetSize;
        }

        private static ulong ReadVarULong(byte[] buffer, int offset, int size)
        {
            ulong val = 0;
            for (int i = 0; i < size; ++i)
            {
                val = val | (((ulong)buffer[offset + i]) << (i * 8));
            }
            return val;
        }

        private static long ReadVarLong(byte[] buffer, int offset, int size)
        {
            ulong val = 0;
            bool signExtend = false;

            for (int i = 0; i < size; ++i)
            {
                byte b = buffer[offset + i];
                val = val | (((ulong)b) << (i * 8));
                signExtend = (b & 0x80) != 0;
            }

            if (signExtend)
            {
                for (int i = size; i < 8; ++i)
                {
                    val = val | (((ulong)0xFF) << (i * 8));
                }
            }

            return (long)val;
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + ">" + _runOffset + " [+" + _runLength + "]");
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:+##;-##;0}[+{1}]", _runOffset, _runLength);
        }
    }
}

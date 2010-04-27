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

namespace DiscUtils.Wim
{
    [Flags]
    internal enum ResourceFlags : byte
    {
        None = 0x00,
        Free = 0x01,
        MetaData = 0x02,
        Compressed = 0x04,
        Spanned = 0x08
    }

    internal class ShortResourceHeader
    {
        public const int Size = 24;

        public ResourceFlags Flags;
        public long CompressedSize;
        public long FileOffset;
        public long OriginalSize;

        public void Read(byte[] buffer, int offset)
        {
            CompressedSize = Utilities.ToInt64LittleEndian(buffer, offset);
            Flags = (ResourceFlags)((CompressedSize >> 56) & 0xFF);
            CompressedSize = CompressedSize & 0x00FFFFFFFFFFFFFF;
            FileOffset = Utilities.ToInt64LittleEndian(buffer, offset + 8);
            OriginalSize = Utilities.ToInt64LittleEndian(buffer, offset + 16);
        }
    }
}

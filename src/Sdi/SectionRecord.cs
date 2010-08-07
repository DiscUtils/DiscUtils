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


namespace DiscUtils.Sdi
{
    internal class SectionRecord
    {
        public const int RecordSize = 64;

        public string SectionType;
        public ulong Attr;
        public long Offset;
        public long Size;
        public ulong PartitionType;

        public void ReadFrom(byte[] buffer, int offset)
        {
            SectionType = Utilities.BytesToString(buffer, offset, 8).TrimEnd('\0');
            Attr = Utilities.ToUInt64LittleEndian(buffer, offset + 8);
            Offset = Utilities.ToInt64LittleEndian(buffer, offset + 16);
            Size = Utilities.ToInt64LittleEndian(buffer, offset + 24);
            PartitionType = Utilities.ToUInt64LittleEndian(buffer, offset + 32);
        }
    }
}

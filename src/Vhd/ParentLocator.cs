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


namespace DiscUtils.Vhd
{
    internal class ParentLocator
    {
        public const string PlatformCodeWindowsRelativeUnicode = "W2ru";
        public const string PlatformCodeWindowsAbsoluteUnicode = "W2ku";

        public string PlatformCode;
        public int PlatformDataSpace;
        public int PlatformDataLength;
        public long PlatformDataOffset;

        public ParentLocator()
        {
            PlatformCode = "";
        }

        public ParentLocator(ParentLocator toCopy)
        {
            PlatformCode = toCopy.PlatformCode;
            PlatformDataSpace = toCopy.PlatformDataSpace;
            PlatformDataLength = toCopy.PlatformDataLength;
            PlatformDataOffset = toCopy.PlatformDataOffset;
        }

        public static ParentLocator FromBytes(byte[] data, int offset)
        {
            ParentLocator result = new ParentLocator();
            result.PlatformCode = Utilities.BytesToString(data, offset, 4);
            result.PlatformDataSpace = Utilities.ToInt32BigEndian(data, offset + 4);
            result.PlatformDataLength = Utilities.ToInt32BigEndian(data, offset + 8);
            result.PlatformDataOffset = Utilities.ToInt64BigEndian(data, offset + 16);
            return result;
        }

        internal void ToBytes(byte[] data, int offset)
        {
            Utilities.StringToBytes(PlatformCode, data, offset, 4);
            Utilities.WriteBytesBigEndian(PlatformDataSpace, data, offset + 4);
            Utilities.WriteBytesBigEndian(PlatformDataLength, data, offset + 8);
            Utilities.WriteBytesBigEndian((uint)0, data, offset + 12);
            Utilities.WriteBytesBigEndian(PlatformDataOffset, data, offset + 16);
        }
    }
}

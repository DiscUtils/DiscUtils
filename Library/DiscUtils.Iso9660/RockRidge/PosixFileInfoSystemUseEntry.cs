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

namespace DiscUtils.Iso9660
{
    internal sealed class PosixFileInfoSystemUseEntry : SystemUseEntry
    {
        public uint FileMode;
        public uint GroupId;
        public uint Inode;
        public uint NumLinks;
        public uint UserId;

        public PosixFileInfoSystemUseEntry(string name, byte length, byte version, byte[] data, int offset)
        {
            CheckAndSetCommonProperties(name, length, version, 36, 1);

            FileMode = IsoUtilities.ToUInt32FromBoth(data, offset + 4);
            NumLinks = IsoUtilities.ToUInt32FromBoth(data, offset + 12);
            UserId = IsoUtilities.ToUInt32FromBoth(data, offset + 20);
            GroupId = IsoUtilities.ToUInt32FromBoth(data, offset + 28);
            Inode = 0;
            if (length >= 44)
            {
                Inode = IsoUtilities.ToUInt32FromBoth(data, offset + 36);
            }
        }
    }
}
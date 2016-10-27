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

namespace DiscUtils.Udf
{
    using System;
    using System.Text;

    internal enum OSClass : byte
    {
        None = 0,
        Dos = 1,
        OS2 = 2,
        Macintosh = 3,
        Unix = 4,
        Windows9x = 5,
        WindowsNt = 6,
        Os400 = 7,
        BeOS = 8,
        WindowsCe = 9
    }

    internal enum OSIdentifier : ushort
    {
        DosOrWindows3 = 0x0100,
        Os2 = 0x0200,
        MacintoshOs9 = 0x0300,
        MacintoshOsX = 0x0301,
        UnixGeneric = 0x0400,
        UnixAix = 0x0401,
        UnixSunOS = 0x0402,
        UnixHPUX = 0x0403,
        UnixIrix = 0x0404,
        UnixLinux = 0x0405,
        UnixMkLinux = 0x0406,
        UnixFreeBsd = 0x0407,
        UnixNetBsd = 0x0408,
        Windows9x = 0x0500,
        WindowsNt = 0x0600,
        Os400 = 0x0700,
        BeOS = 0x0800,
        WindowsCe = 0x0900
    }

    internal abstract class EntityIdentifier : IByteArraySerializable
    {
        public byte Flags;
        public string Identifier;
        public byte[] Suffix;

        public int Size
        {
            get { return 32; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Flags = buffer[offset];
            Identifier = Encoding.ASCII.GetString(buffer, offset + 1, 23).TrimEnd('\0');
            Suffix = Utilities.ToByteArray(buffer, offset + 24, 8);

            return 32;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

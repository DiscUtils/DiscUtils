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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Udf
{
    internal enum CharacterSetType : byte
    {
        CharacterSet0 = 0,
        CharacterSet1 = 1,
        CharacterSet2 = 2,
        CharacterSet3 = 3,
        CharacterSet4 = 4,
        CharacterSet5 = 5,
        CharacterSet6 = 6,
        CharacterSet7 = 7,
        CharacterSet8 = 8,
    }

    internal class CharacterSetSpecification : IByteArraySerializable
    {
        public CharacterSetType Type;
        public byte[] Information;

        public int ReadFrom(byte[] buffer, int offset)
        {
            Type = (CharacterSetType)buffer[offset];
            Information = Utilities.ToByteArray(buffer, offset + 1, 63);
            return 64;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public int Size
        {
            get { return 64; }
        }
    }
}

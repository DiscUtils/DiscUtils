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

namespace DiscUtils.Registry
{
    [Flags]
    internal enum ValueFlags : ushort
    {
        Named = 0x0001,
        Unknown0002 = 0x0002,
        Unknown0004 = 0x0004,
        Unknown0008 = 0x0008,
        Unknown0010 = 0x0010,
        Unknown0020 = 0x0020,
        Unknown0040 = 0x0040,
        Unknown0080 = 0x0080,
        Unknown0100 = 0x0100,
        Unknown0200 = 0x0200,
        Unknown0400 = 0x0400,
        Unknown0800 = 0x0800,
        Unknown1000 = 0x1000,
        Unknown2000 = 0x2000,
        Unknown4000 = 0x4000,
        Unknown8000 = 0x8000
    }
}
//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Btrfs.Base
{
    internal enum ItemType:byte
    {
        InodeItem = 0x01,
        InodeRef = 0x0c,
        InodeExtRef = 0x0d,
        XattrItem = 0x18,
        OrphanItem = 0x30,
        DirLogItem = 0x3c,
        DirLogIndex = 0x48,
        DirItem = 0x54,
        DirIndex = 0x60,
        ExtentData = 0x6c,
        ExtentCsum = 0x80,
        RootItem = 0x84,
        RootBackref = 0x90,
        RootRef = 0x9c,
        ExtentItem = 0xa8,
        MetadataItem = 0xa9,
        TreeBlockRef = 0xb0,
        ExtentDataRef = 0xb2,
        ExtentRefV0 = 0xb4,
        SharedBlockRef = 0xb6,
        SharedDataRef = 0xb8,
        BlockGroupItem = 0xc0,
        DevExtent = 0xcc,
        DevItem = 0xd8,
        ChunkItem = 0xe4,
        StringItem = 0xfd
    }
}

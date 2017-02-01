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
    internal enum FileType : byte
    {
        None = 0,
        UnallocatedSpaceEntry = 1,
        PartitionIntegrityEntry = 2,
        IndirectEntry = 3,
        Directory = 4,
        RandomBytes = 5,
        SpecialBlockDevice = 6,
        SpecialCharacterDevice = 7,
        ExtendedAttributes = 8,
        Fifo = 9,
        Socket = 10,
        TerminalEntry = 11,
        SymbolicLink = 12,
        StreamDirectory = 13,

        UdfVirtualAllocationTable = 248,
        UdfRealTimeFile = 249,
        UdfMetadataFile = 250,
        UdfMetadataMirrorFile = 251,
        UdfMetadataBitmapFile = 252
    }
}
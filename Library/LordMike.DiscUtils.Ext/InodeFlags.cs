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

namespace DiscUtils.Ext
{
    /// <summary>
    /// Inode flags.
    /// </summary>
    [Flags]
    internal enum InodeFlags : uint
    {
        SecureDelete = 0x00000001,
        Undelete = 0x00000002,
        CompressFile = 0x00000004,
        SynchronousUpdates = 0x00000008,
        ImmutableFile = 0x00000010,
        AppendOnly = 0x00000020,
        NoDump = 0x00000040,
        NoAccessTime = 0x00000080,
        CompressionDirty = 0x00000100,
        CompressionCompressed = 0x00000200,
        CompressionDintCompress = 0x00000400,
        CompressionError = 0x00000800,
        IndexedDirectory = 0x00001000,
        AfsDirectory = 0x00002000,
        JournalFileData = 0x00004000,
        NoTailMerge = 0x00008000,
        DirSync = 0x00010000,
        TopDir = 0x00020000,
        HugeFile = 0x00040000,
        ExtentsUsed = 0x00080000,
        Migrating = 0x00100000
    }
}
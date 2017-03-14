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
    /// Feature flags for features backwards compatible with read-only mounting.
    /// </summary>
    [Flags]
    internal enum IncompatibleFeatures : ushort
    {
        /// <summary>
        /// File compression used (not used in mainline?).
        /// </summary>
        Compression = 0x0001,

        /// <summary>
        /// Indicates that directory entries contain a file type field (uses byte of file name length field).
        /// </summary>
        FileType = 0x0002,

        /// <summary>
        /// Ext3 feature - indicates a dirty journal, that needs to be replayed (safe for read-only access, not for read-write).
        /// </summary>
        NeedsRecovery = 0x0004,

        /// <summary>
        /// Ext3 feature - indicates the file system is a dedicated EXT3 journal, not an actual file system.
        /// </summary>
        IsJournalDevice = 0x0008,

        /// <summary>
        /// Indicates the file system saves space by only allocating backup space for the superblock in groups storing it (used with SparseSuperBlock).
        /// </summary>
        MetaBlockGroup = 0x0010,

        /// <summary>
        /// Ext4 feature to store files as extents.
        /// </summary>
        Extents = 0x0040,

        /// <summary>
        /// Ext4 feature to support some 64-bit fields.
        /// </summary>
        SixtyFourBit = 0x080,

        /// <summary>
        /// Ext4 feature for storage of block groups.
        /// </summary>
        FlexBlockGroups = 0x0200
    }
}
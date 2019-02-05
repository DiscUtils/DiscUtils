//
// Copyright (c) 2019, Bianco Veigel
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

namespace DiscUtils.Xfs
{
    using System;

    /// <summary>
    /// Filesystem version number. This is a bitmask specifying the features enabled when creating the filesystem.
    /// Any disk checking tools or drivers that do not recognize any set bits must not operate upon the filesystem.
    /// Most of the flags indicate features introduced over time.
    /// </summary>
    [Flags]
    internal enum VersionFlags : ushort
    {
        None = 0,
        Version1 = 1, /* 5.3, 6.0.1, 6.1 */
        Version2 = 2, /* 6.2 - attributes */
        Version3 = 3, /* 6.2 - new inode version */
        Version4 = 4, /* 6.2+ - bitmask version */
        Version5 = 5, /* CRC enabled filesystem */
        NumberFlag = 0x000f,

        /// <summary>
        /// Set if any inode have extended attributes. If this bit is
        /// set; the XFS_SB_VERSION2_ATTR2BIT is not
        /// set; and the attr2 mount flag is not specified, the
        /// di_forkoff inode field will not be dynamically
        /// adjusted.
        /// </summary>
        ExtendedAttributes = 0x0010,

        /// <summary>
        /// Set if any inodes use 32-bit di_nlink values.
        /// </summary>
        NLink = 0x0020,

        /// <summary>
        /// Quotas are enabled on the filesystem. This also
        /// brings in the various quota fields in the superblock.
        /// </summary>
        Quota = 0x0040,

        /// <summary>
        /// Set if sb_inoalignmt is used.
        /// </summary>
        Alignment = 0x0080,

        /// <summary>
        /// Set if sb_unit and sb_width are used.
        /// </summary>
        DAlignment = 0x0100,

        /// <summary>
        /// Set if sb_shared_vn is used.
        /// </summary>
        Shared = 0x0200,

        /// <summary>
        /// Version 2 journaling logs are used.
        /// </summary>
        LogV2 = 0x0400,

        /// <summary>
        /// Set if sb_sectsize is not 512.
        /// </summary>
        Sector = 0x0800,

        /// <summary>
        /// Unwritten extents are used. This is always set.
        /// </summary>
        ExtentFlag = 0x1000,

        /// <summary>
        /// Version 2 directories are used. This is always set.
        /// </summary>
        DirV2 = 0x2000,

        /// <summary>
        /// ASCII only case-insens.
        /// </summary>
        Borg = 0x4000,

        /// <summary>
        /// Set if the sb_features2 field in the superblock
        /// contains more flags.
        /// </summary>
        Features2 = 0x8000,
    }
}

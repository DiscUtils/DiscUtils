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
    /// Additional version flags if <see cref="VersionFlags.MOREBITSBIT"/> is set in <see cref="SuperBlock.Version"/>.
    /// </summary>
    [Flags]
    internal enum Version2Features : uint
    {
        Reserved1 = 0x001,

        /// <summary>
        /// Lazy global counters. Making a filesystem with this
        /// bit set can improve performance. The global free
        /// space and inode counts are only updated in the
        /// primary superblock when the filesystem is cleanly
        /// unmounted.
        /// </summary>
        LazySbBitCount = 0x002,
        Reserved4 = 0x004,

        /// <summary>
        /// Extended attributes version 2. Making a filesystem
        /// with this optimises the inode layout of extended
        /// attributes. If this bit is set and the noattr2 mount
        /// flag is not specified, the di_forkoff inode field
        /// will be dynamically adjusted.
        /// </summary>
        ExtendedAttributeVersion2 = 0x008,

        /// <summary>
        /// Parent pointers. All inodes must have an extended
        /// attribute that points back to its parent inode. The
        /// primary purpose for this information is in backup
        /// systems.
        /// </summary>
        Parent = 0x010,

        /// <summary>
        /// 32-bit Project ID. Inodes can be associated with a
        /// project ID number, which can be used to enforce disk
        /// space usage quotas for a particular group of
        /// directories. This flag indicates that project IDs can be
        /// 32 bits in size.
        /// </summary>
        ProjectId32Bit = 0x0080,

        /// <summary>
        /// Metadata checksumming. All metadata blocks have
        /// an extended header containing the block checksum,
        /// a copy of the metadata UUID, the log sequence
        /// number of the last update to prevent stale replays,
        /// and a back pointer to the owner of the block. This
        /// feature must be and can only be set if the lowest
        /// nibble of sb_versionnum is set to 5.
        /// </summary>
        Crc = 0x0100,

        /// <summary>
        /// Directory file type. Each directory entry records the
        /// type of the inode to which the entry points. This
        /// speeds up directory iteration by removing the need
        /// to load every inode into memory.
        /// </summary>
        FType = 0x0200,
    }
}
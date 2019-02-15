//
// Copyright (c) 2016, Bianco Veigel
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
    
    [Flags]
    internal enum InodeFlags : ushort
    {
        None = 0,

        /// <summary>
        /// The inode's data is located on the real-time device.
        /// </summary>
        Realtime = (1 << 0),

        /// <summary>
        /// The inode's extents have been preallocated.
        /// </summary>
        Prealloc = (1 << 1),

        /// <summary>
        /// Specifies the sb_rbmino uses the new real-time bitmap format
        /// </summary>
        NewRtBitmap = (1 << 2),

        /// <summary>
        /// Specifies the inode cannot be modified.
        /// </summary>
        Immutable = (1 << 3),

        /// <summary>
        /// The inode is in append only mode.
        /// </summary>
        Append = (1 << 4),

        /// <summary>
        /// The inode is written synchronously.
        /// </summary>
        Sync = (1 << 5),

        /// <summary>
        /// The inode's di_atime is not updated.
        /// </summary>
        NoAtime = (1 << 6),

        /// <summary>
        /// Specifies the inode is to be ignored by xfsdump.
        /// </summary>
        NoDump = (1 << 7),

        /// <summary>
        /// For directory inodes, new inodes inherit the XFS_DIFLAG_REALTIME bit.
        ///  </summary>
        RtInherit = (1 << 8),

        /// <summary>
        /// For directory inodes, new inodes inherit the <see cref="Inode.ProjectId"/> value.
        /// </summary>
        ProjInherit = (1 << 9),

        /// <summary>
        /// For directory inodes, symlinks cannot be created.
        /// </summary>
        NoSymlinks = (1 << 10),

        /// <summary>
        /// Specifies the extent size for real-time files or a and extent size hint for regular files.
        /// </summary>
        ExtentSize = (1 << 11),

        /// <summary>
        /// For directory inodes, new inodes inherit the <see cref="Inode.ExtentSize"/> value.
        /// </summary>
        ExtentSizeInherit = (1 << 12),

        /// <summary>
        /// Specifies the inode is to be ignored when defragmenting the filesystem.
        /// </summary>
        NoDefrag = (1 << 13),

        /// <summary>
        /// use filestream allocator
        /// </summary>
        Filestream = (1 << 14)
    }
}

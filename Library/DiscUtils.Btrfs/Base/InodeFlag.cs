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

using System;

namespace DiscUtils.Btrfs.Base
{
    [Flags]
    internal enum InodeFlag : ulong
    {
        /// <summary>
        /// Do not perform checksum operations on this inode.
        /// </summary>
        NoDataSum = 0x1,
        /// <summary>
        /// Do not perform CoW for data extents on this inode when the reference count is 1.
        /// </summary>
        NoDataCow = 0x2,
        /// <summary>
        /// Inode is read-only regardless of UNIX permissions or ownership.
        /// </summary>
        /// <remarks>
        /// This bit is still checked and returns EACCES but there is no way to set it. That suggests that it has been superseded by IMMUTABLE.
        /// </remarks>
        Readonly = 0x4,
        /// <summary>
        /// Do not compress this inode.
        /// </summary>
        /// <remarks>
        /// This flag may be changed by the kernel as compression ratios change.
        /// If the compression ratio for data associated with an inode becomes undesirable,
        /// this flag will be set. It may be cleared if the data changes and the compression ratio is favorable again.
        /// </remarks>
        NoCompress = 0x8,
        /// <summary>
        /// Inode contains preallocated extents. This instructs the kernel to attempt to avoid CoWing those extents.
        /// </summary>
        Prealloc = 0x10,
        /// <summary>
        /// Operations on this inode will be performed synchronously.
        /// This flag is converted to a VFS-level inode flag but is not handled anywhere.
        /// </summary>
        Sync = 0x20,
        /// <summary>
        /// Inode is read-only regardless of UNIX permissions or ownership. Attempts to modify this inode will result in EPERM being returned to the user.
        /// </summary>
        Immutable = 0x40,
        /// <summary>
        /// This inode is append-only.
        /// </summary>
        Append = 0x80,
        /// <summary>
        /// This inode is not a candidate for dumping using the dump(8) program.
        /// </summary>
        /// <remarks>
        /// This flag will be accepted on all kernels but is not implemented
        /// </remarks>
        NoDump = 0x100,
        /// <summary>
        /// Do not update atime,when this inode is accessed.
        /// </summary>
        NoATime = 0x200,
        /// <summary>
        /// Operations on directory operations will be performed synchronously.
        /// </summary>
        /// <remarks>
        /// This flag is converted to a VFS-level inode flag but is not handled anywhere.
        /// </remarks>
        DirSync = 0x400,
        /// Compression is enabled on this inode.
        Compress = 0x800,
    }
}

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

    /// <summary>
    /// Feature flags for features backwards compatible with read-only mounting.
    /// </summary>
    [Flags]
    internal enum ReadOnlyCompatibleFeatures : uint
    {
        /// <summary>
        /// Free inode B+tree. Each allocation group contains a
        /// B+tree to track inode chunks containing free inodes.
        /// This is a performance optimization to reduce the
        /// time required to allocate inodes.
        /// </summary>
        FINOBT = (1 << 0),

        /// <summary>
        /// Reverse mapping B+tree. Each allocation group
        /// contains a B+tree containing records mapping AG
        /// blocks to their owners.
        /// </summary>
        RMAPBT = (1 << 1),

        /// <summary>
        /// Reference count B+tree. Each allocation group
        /// contains a B+tree to track the reference counts of AG
        /// blocks. This enables files to share data blocks safely.
        /// </summary>
        REFLINK = (1 << 2),

        ALL = FINOBT | RMAPBT | REFLINK,
    }
}

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
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Feature flags for features backwards compatible with read-only mounting.
    /// </summary>
    [Flags]
    internal enum ReadOnlyCompatibleFeatures : uint
    {
        /// <summary>
        /// free inode btree
        /// </summary>
        FINOBT = (1 << 0),

        /// <summary>
        /// reverse map btree
        /// </summary>
        RMAPBT = (1 << 1),

        /// <summary>
        /// reflinked files
        /// </summary>
        REFLINK = (1 << 2),

        ALL = FINOBT | RMAPBT | REFLINK,
    }
}

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

namespace DiscUtils.Ntfs.Internals
{
    /// <summary>
    /// Flags indicating the nature of a Master File Table entry.
    /// </summary>
    [Flags]
    public enum MasterFileTableEntryFlags
    {
        /// <summary>
        /// Default value.
        /// </summary>
        None = 0x0000,

        /// <summary>
        /// The entry is currently in use.
        /// </summary>
        InUse = 0x0001,

        /// <summary>
        /// The entry is for a directory (rather than a file).
        /// </summary>
        IsDirectory = 0x0002,

        /// <summary>
        /// The entry is for a file that forms parts of the NTFS meta-data.
        /// </summary>
        IsMetaFile = 0x0004,

        /// <summary>
        /// The entry contains index attributes.
        /// </summary>
        HasViewIndex = 0x0008
    }
}
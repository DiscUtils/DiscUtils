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
    /// Feature flags for backwards compatible features.
    /// </summary>
    [Flags]
    internal enum CompatibleFeatures : ushort
    {
        /// <summary>
        /// Indicates pre-allocation hints are present.
        /// </summary>
        DirectoryPreallocation = 0x0001,

        /// <summary>
        /// AFS support in inodex.
        /// </summary>
        IMagicInodes = 0x0002,

        /// <summary>
        /// Indicates an EXT3-style journal is present.
        /// </summary>
        HasJournal = 0x0004,

        /// <summary>
        /// Indicates extended attributes (e.g. FileACLs) are present.
        /// </summary>
        ExtendedAttributes = 0x0008,

        /// <summary>
        /// Indicates space is reserved through a special inode to enable the file system to be resized dynamically.
        /// </summary>
        ResizeInode = 0x0010,

        /// <summary>
        /// Indicates that directory indexes are present (not used in mainline?).
        /// </summary>
        DirectoryIndex = 0x0020
    }
}
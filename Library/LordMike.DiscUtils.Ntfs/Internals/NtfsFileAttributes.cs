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
    /// File attributes as stored natively by NTFS.
    /// </summary>
    [Flags]
    public enum NtfsFileAttributes
    {
        /// <summary>
        /// No attributes.
        /// </summary>
        None = 0x00000000,

        /// <summary>
        /// The file is read-only.
        /// </summary>
        ReadOnly = 0x00000001,

        /// <summary>
        /// The file is hidden.
        /// </summary>
        Hidden = 0x00000002,

        /// <summary>
        /// The file is part of the Operating System.
        /// </summary>
        System = 0x00000004,

        /// <summary>
        /// The file should be archived.
        /// </summary>
        Archive = 0x00000020,

        /// <summary>
        /// The file is actually a device.
        /// </summary>
        Device = 0x00000040,

        /// <summary>
        /// The file is a 'normal' file.
        /// </summary>
        Normal = 0x00000080,

        /// <summary>
        /// The file is a temporary file.
        /// </summary>
        Temporary = 0x00000100,

        /// <summary>
        /// The file content is stored in sparse form.
        /// </summary>
        Sparse = 0x00000200,

        /// <summary>
        /// The file has a reparse point attached.
        /// </summary>
        ReparsePoint = 0x00000400,

        /// <summary>
        /// The file content is stored compressed.
        /// </summary>
        Compressed = 0x00000800,

        /// <summary>
        /// The file is an 'offline' file.
        /// </summary>
        Offline = 0x00001000,

        /// <summary>
        /// The file is not indexed.
        /// </summary>
        NotIndexed = 0x00002000,

        /// <summary>
        /// The file content is encrypted.
        /// </summary>
        Encrypted = 0x00004000,

        /// <summary>
        /// The file is actually a directory.
        /// </summary>
        Directory = 0x10000000,

        /// <summary>
        /// The file has an index attribute.
        /// </summary>
        IndexView = 0x20000000
    }
}
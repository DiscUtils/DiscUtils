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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Vfs
{
    /// <summary>
    /// Interface implemented by a class representing a file.
    /// </summary>
    /// <remarks>
    /// File system implementations should have a class that implements this
    /// interface.  If the file system implementation is read-only, it is
    /// acceptable to throw <c>NotImplementedException</c> from setters.
    /// </remarks>
    public interface IVfsFile
    {
        /// <summary>
        /// Gets or sets the last creation time in UTC.
        /// </summary>
        DateTime CreationTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the file's attributes.
        /// </summary>
        FileAttributes FileAttributes { get; set; }

        /// <summary>
        /// Gets a buffer to access the file's contents.
        /// </summary>
        IBuffer FileContent { get; }

        /// <summary>
        /// Gets the length of the file.
        /// </summary>
        long FileLength { get; }

        /// <summary>
        /// Gets or sets the last access time in UTC.
        /// </summary>
        DateTime LastAccessTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the last write time in UTC.
        /// </summary>
        DateTime LastWriteTimeUtc { get; set; }
    }
}
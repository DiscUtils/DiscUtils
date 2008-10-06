//
// Copyright (c) 2008, Kenneth Bell
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

using System.IO;

namespace DiscUtils
{
    /// <summary>
    /// Provides information about a file on a disc.
    /// </summary>
    public abstract class DiscFileInfo : DiscFileSystemInfo
    {
        /// <summary>
        /// Gets the length of the current file in bytes.
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// Opens the current file.
        /// </summary>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <returns>The newly created stream</returns>
        /// <remarks>Read-only file systems only support <c>FileMode.Open</c>.</remarks>
        public abstract Stream Open(FileMode mode);

        /// <summary>
        /// Opens the current file.
        /// </summary>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <param name="access">The access permissions for the created stream.</param>
        /// <returns>The newly created stream</returns>
        /// <remarks>Read-only file systems only support <c>FileMode.Open</c> and <c>FileAccess.Read</c>.</remarks>
        public abstract Stream Open(FileMode mode, FileAccess access);
    }
}

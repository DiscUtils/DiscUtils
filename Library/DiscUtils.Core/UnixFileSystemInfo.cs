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

namespace DiscUtils
{
    /// <summary>
    /// Information about a file or directory common to most Unix systems.
    /// </summary>
    public sealed class UnixFileSystemInfo
    {
        /// <summary>
        /// Gets or sets the device id of the referenced device (for character and block devices).
        /// </summary>
        public long DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the file's type.
        /// </summary>
        public UnixFileType FileType { get; set; }

        /// <summary>
        /// Gets or sets the group that owns this file or directory.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Gets or sets the file's serial number (unique within file system).
        /// </summary>
        public long Inode { get; set; }

        /// <summary>
        /// Gets or sets the number of hard links to this file.
        /// </summary>
        public int LinkCount { get; set; }

        /// <summary>
        /// Gets or sets the file permissions (aka flags) for this file or directory.
        /// </summary>
        public UnixFilePermissions Permissions { get; set; }

        /// <summary>
        /// Gets or sets the user that owns this file or directory.
        /// </summary>
        public int UserId { get; set; }
    }
}
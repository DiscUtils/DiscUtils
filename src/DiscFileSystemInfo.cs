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

using System;
using System.IO;

namespace DiscUtils
{
    /// <summary>
    /// Provides the base class for both <see cref="DiscFileInfo"/> and <see cref="DiscDirectoryInfo"/> objects.
    /// </summary>
    public abstract class DiscFileSystemInfo
    {
        /// <summary>
        /// Gets the name of the file or directory.
        /// </summary>
        public abstract string Name
        {
            get;
        }

        /// <summary>
        /// Gets the full path of the file or directory.
        /// </summary>
        public abstract string FullName
        {
            get;
        }

        /// <summary>
        /// Gets the <see cref="System.IO.FileAttributes"/> of the current <see cref="DiscFileSystemInfo"/> object.
        /// </summary>
        public abstract FileAttributes Attributes
        {
            get;
        }

        /// <summary>
        /// Gets the <see cref="DiscDirectoryInfo"/> of the directory containing the current <see cref="DiscFileSystemInfo"/> object.
        /// </summary>
        public abstract DiscDirectoryInfo Parent
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the file system object exists.
        /// </summary>
        public abstract bool Exists
        {
            get;
        }

        /// <summary>
        /// Gets the creation time (in local time) of the current <see cref="DiscFileSystemInfo"/> object.
        /// </summary>
        public abstract DateTime CreationTime
        {
            get;
        }

        /// <summary>
        /// Gets the creation time (in UTC) of the current <see cref="DiscFileSystemInfo"/> object.
        /// </summary>
        public abstract DateTime CreationTimeUtc
        {
            get;
        }

        /// <summary>
        /// Gets the last time (in local time) the file or directory was accessed.
        /// </summary>
        /// <remarks>Read-only file systems will never update this value, it will remain at a fixed value.</remarks>
        public abstract DateTime LastAccessTime
        {
            get;
        }

        /// <summary>
        /// Gets the last time (in UTC) the file or directory was accessed.
        /// </summary>
        /// <remarks>Read-only file systems will never update this value, it will remain at a fixed value.</remarks>
        public abstract DateTime LastAccessTimeUtc
        {
            get;
        }

        /// <summary>
        /// Gets the last time (in local time) the file or directory was written to.
        /// </summary>
        public abstract DateTime LastWriteTime
        {
            get;
        }

        /// <summary>
        /// Gets the last time (in UTC) the file or directory was written to.
        /// </summary>
        public abstract DateTime LastWriteTimeUtc
        {
            get;
        }
    }
}

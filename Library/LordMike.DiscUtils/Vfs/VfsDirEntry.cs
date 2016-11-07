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

namespace DiscUtils.Vfs
{
    /// <summary>
    /// Base class for directory entries in a file system.
    /// </summary>
    /// <remarks>
    /// File system implementations should have a class that derives from
    /// this abstract class.  If the file system implementation is read-only,
    /// it is acceptable to throw <c>NotImplementedException</c> from methods
    /// that attempt to modify the file system.
    /// </remarks>
    public abstract class VfsDirEntry
    {
        /// <summary>
        /// Gets the creation time of the file or directory.
        /// </summary>
        /// <remarks>
        /// May throw <c>NotSupportedException</c> if <c>HasVfsTimeInfo</c> is <c>false</c>.
        /// </remarks>
        public abstract DateTime CreationTimeUtc { get; }

        /// <summary>
        /// Gets the file attributes from the directory entry.
        /// </summary>
        /// <remarks>
        /// May throw <c>NotSupportedException</c> if <c>HasVfsFileAttributes</c> is <c>false</c>.
        /// </remarks>
        public abstract FileAttributes FileAttributes { get; }

        /// <summary>
        /// Gets the name of this directory entry.
        /// </summary>
        public abstract string FileName { get; }

        /// <summary>
        /// Gets a value indicating whether this directory entry contains file attribute information.
        /// </summary>
        /// <remarks>
        /// <para>Typically either always returns <c>true</c> or <c>false</c>.</para>
        /// </remarks>
        public abstract bool HasVfsFileAttributes { get; }

        /// <summary>
        /// Gets a value indicating whether this directory entry contains time information.
        /// </summary>
        /// <remarks>
        /// <para>Typically either always returns <c>true</c> or <c>false</c>.</para>
        /// </remarks>
        public abstract bool HasVfsTimeInfo { get; }

        /// <summary>
        /// Gets a value indicating whether this directory entry represents a directory (rather than a file).
        /// </summary>
        public abstract bool IsDirectory { get; }

        /// <summary>
        /// Gets a value indicating whether this directory entry represents a symlink (rather than a file or directory).
        /// </summary>
        public abstract bool IsSymlink { get; }

        /// <summary>
        /// Gets the last access time of the file or directory.
        /// </summary>
        /// <remarks>
        /// May throw <c>NotSupportedException</c> if <c>HasVfsTimeInfo</c> is <c>false</c>.
        /// </remarks>
        public abstract DateTime LastAccessTimeUtc { get; }

        /// <summary>
        /// Gets the last write time of the file or directory.
        /// </summary>
        /// <remarks>
        /// May throw <c>NotSupportedException</c> if <c>HasVfsTimeInfo</c> is <c>false</c>.
        /// </remarks>
        public abstract DateTime LastWriteTimeUtc { get; }

        /// <summary>
        /// Gets a version of FileName that can be used in wildcard matches.
        /// </summary>
        /// <remarks>
        /// The returned name, must have an extension separator '.', and not have any optional version
        /// information found in some files.  The returned name is matched against a wildcard patterns
        /// such as "*.*".
        /// </remarks>
        public virtual string SearchName
        {
            get
            {
                string fileName = FileName;
                if (fileName.IndexOf('.') == -1)
                {
                    return fileName + ".";
                }
                return fileName;
            }
        }

        /// <summary>
        /// Gets a unique id for the file or directory represented by this directory entry.
        /// </summary>
        public abstract long UniqueCacheId { get; }
    }
}
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
    /// Provides the base class for all file systems.
    /// </summary>
    public abstract class DiscFileSystem : IDisposable
    {
        /// <summary>
        /// Destructor.
        /// </summary>
        ~DiscFileSystem()
        {
            Dispose(false);
        }

        /// <summary>
        /// Provides a friendly description of the file system type.
        /// </summary>
        public abstract string FriendlyName
        {
            get;
        }

        /// <summary>
        /// Indicates whether the file system is read-only or read-write.
        /// </summary>
        /// <returns>true if the file system is read-write.</returns>
        public abstract bool CanWrite { get; }

        /// <summary>
        /// Gets the root directory of the file system.
        /// </summary>
        public abstract DiscDirectoryInfo Root
        {
            get;
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <returns>The new stream.</returns>
        public virtual Stream Open(string path, FileMode mode)
        {
            return Open(path, mode, (mode == FileMode.Open) ? FileAccess.Read : FileAccess.ReadWrite);
        }

        /// <summary>
        /// Opens the specified file.
        /// </summary>
        /// <param name="path">The full path of the file to open.</param>
        /// <param name="mode">The file mode for the created stream.</param>
        /// <param name="access">The access permissions for the created stream.</param>
        /// <returns>The new stream.</returns>
        public abstract Stream Open(string path, FileMode mode, FileAccess access);

        /// <summary>
        /// Gets an object representing a possible file.
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file does not need to exist</remarks>
        public abstract DiscFileInfo GetFileInfo(string path);

        /// <summary>
        /// Gets an object representing a possible directory.
        /// </summary>
        /// <param name="path">The directory path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file does not need to exist</remarks>
        public abstract DiscDirectoryInfo GetDirectoryInfo(string path);

        /// <summary>
        /// Gets an object representing a possible file system object (file or directory).
        /// </summary>
        /// <param name="path">The file system path</param>
        /// <returns>The representing object</returns>
        /// <remarks>The file does not need to exist</remarks>
        public abstract DiscFileSystemInfo GetFileSystemInfo(string path);

        #region IDisposable Members

        /// <summary>
        /// Disposes of this instance, releasing all resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        /// <param name="disposing">true if Disposing</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        #endregion
    }
}

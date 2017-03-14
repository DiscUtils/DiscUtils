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

using System.IO;

namespace DiscUtils.Vfs
{
    /// <summary>
    /// Class holding information about a file system.
    /// </summary>
    public sealed class VfsFileSystemInfo : FileSystemInfo
    {
        private readonly VfsFileSystemOpener _openDelegate;

        /// <summary>
        /// Initializes a new instance of the VfsFileSystemInfo class.
        /// </summary>
        /// <param name="name">The name of the file system.</param>
        /// <param name="description">A one-line description of the file system.</param>
        /// <param name="openDelegate">A delegate that can open streams as the indicated file system.</param>
        public VfsFileSystemInfo(string name, string description, VfsFileSystemOpener openDelegate)
        {
            Name = name;
            Description = description;
            _openDelegate = openDelegate;
        }

        /// <summary>
        /// Gets a one-line description of the file system.
        /// </summary>
        public override string Description { get; }

        /// <summary>
        /// Gets the name of the file system.
        /// </summary>
        public override string Name { get; }

        /// <summary>
        /// Opens a volume using the file system.
        /// </summary>
        /// <param name="volume">The volume to access.</param>
        /// <param name="parameters">Parameters for the file system.</param>
        /// <returns>A file system instance.</returns>
        public override DiscFileSystem Open(VolumeInfo volume, FileSystemParameters parameters)
        {
            return _openDelegate(volume.Open(), volume, parameters);
        }

        /// <summary>
        /// Opens a stream using the file system.
        /// </summary>
        /// <param name="stream">The stream to access.</param>
        /// <param name="parameters">Parameters for the file system.</param>
        /// <returns>A file system instance.</returns>
        public override DiscFileSystem Open(Stream stream, FileSystemParameters parameters)
        {
            return _openDelegate(stream, null, parameters);
        }
    }
}
//
// Copyright (c) 2008-2010, Kenneth Bell
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
    /// Delegate for instantiating a file system.
    /// </summary>
    /// <param name="stream">The stream containing the file system</param>
    /// <param name="volumeInfo">Optional, information about the volume the file system is on</param>
    /// <returns>A file system implementation</returns>
    public delegate DiscFileSystem VfsFileSystemOpener(Stream stream, VolumeInfo volumeInfo);

    /// <summary>
    /// Class holding information about a file system.
    /// </summary>
    public sealed class VfsFileSystemInfo : FileSystemInfo
    {
        private string _name;
        private string _description;
        private VfsFileSystemOpener _openDelegate;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="name">The name of the file system</param>
        /// <param name="description">A one-line description of the file system</param>
        /// <param name="openDelegate">A delegate that can open streams as the indicated file system</param>
        public VfsFileSystemInfo(string name, string description, VfsFileSystemOpener openDelegate)
        {
            _name = name;
            _description = description;
            _openDelegate = openDelegate;
        }

        /// <summary>
        /// Gets the name of the file system.
        /// </summary>
        public override string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets a one-line description of the file system.
        /// </summary>
        public override string Description
        {
            get { return _description; }
        }

        /// <summary>
        /// Opens a volume using the file system.
        /// </summary>
        /// <param name="volume">The volume to access</param>
        /// <returns>A file system instance</returns>
        public override DiscFileSystem Open(VolumeInfo volume)
        {
            return _openDelegate(volume.Open(), volume);
        }

        /// <summary>
        /// Opens a stream using the file system.
        /// </summary>
        /// <param name="stream">The stream to access</param>
        /// <returns>A file system instance</returns>
        public override DiscFileSystem Open(Stream stream)
        {
            return _openDelegate(stream, null);
        }
    }
}

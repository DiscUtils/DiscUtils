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
using DiscUtils.Streams;
using DiscUtils.Vfs;

namespace DiscUtils.Ext
{
    /// <summary>
    /// Read-only access to ext file system.
    /// </summary>
    public sealed class ExtFileSystem : VfsFileSystemFacade, IUnixFileSystem
    {
        /// <summary>
        /// Initializes a new instance of the ExtFileSystem class.
        /// </summary>
        /// <param name="stream">The stream containing the ext file system.</param>
        public ExtFileSystem(Stream stream)
            : base(new VfsExtFileSystem(stream, null)) {}

        /// <summary>
        /// Initializes a new instance of the ExtFileSystem class.
        /// </summary>
        /// <param name="stream">The stream containing the ext file system.</param>
        /// <param name="parameters">The generic file system parameters (only file name encoding is honoured).</param>
        public ExtFileSystem(Stream stream, FileSystemParameters parameters)
            : base(new VfsExtFileSystem(stream, parameters)) {}

        /// <summary>
        /// Retrieves Unix-specific information about a file or directory.
        /// </summary>
        /// <param name="path">Path to the file or directory.</param>
        /// <returns>Information about the owner, group, permissions and type of the
        /// file or directory.</returns>
        public UnixFileSystemInfo GetUnixFileInfo(string path)
        {
            return GetRealFileSystem<VfsExtFileSystem>().GetUnixFileInfo(path);
        }

        internal static bool Detect(Stream stream)
        {
            if (stream.Length < 2048)
            {
                return false;
            }

            stream.Position = 1024;
            byte[] superblockData = StreamUtilities.ReadExact(stream, 1024);

            SuperBlock superblock = new SuperBlock();
            superblock.ReadFrom(superblockData, 0);

            return superblock.Magic == SuperBlock.Ext2Magic;
        }
    }
}
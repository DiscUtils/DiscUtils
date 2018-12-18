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

namespace DiscUtils.SquashFs
{
    /// <summary>
    /// Implementation of SquashFs file system reader.
    /// </summary>
    /// <remarks>
    /// SquashFs is a read-only file system, it is not designed to be modified
    /// after it is created.
    /// </remarks>
    public class SquashFileSystemReader : VfsFileSystemFacade, IUnixFileSystem
    {
        /// <summary>
        /// Initializes a new instance of the SquashFileSystemReader class.
        /// </summary>
        /// <param name="data">The stream to read the file system image from.</param>
        public SquashFileSystemReader(Stream data)
            : base(new VfsSquashFileSystemReader(data)) {}

        /// <summary>
        /// Gets Unix file information about a file or directory.
        /// </summary>
        /// <param name="path">Path to the file or directory.</param>
        /// <returns>Unix information about the file or directory.</returns>
        public UnixFileSystemInfo GetUnixFileInfo(string path)
        {
            return GetRealFileSystem<VfsSquashFileSystemReader>().GetUnixFileInfo(path);
        }

        /// <summary>
        /// Detects if the stream contains a SquashFs file system.
        /// </summary>
        /// <param name="stream">The stream to inspect.</param>
        /// <returns><c>true</c> if stream appears to be a SquashFs file system.</returns>
        public static bool Detect(Stream stream)
        {
            stream.Position = 0;

            SuperBlock superBlock = new SuperBlock();

            if (stream.Length < superBlock.Size)
            {
                return false;
            }

            byte[] buffer = StreamUtilities.ReadExact(stream, superBlock.Size);
            superBlock.ReadFrom(buffer, 0);

            return superBlock.Magic == SuperBlock.SquashFsMagic;
        }
    }
}
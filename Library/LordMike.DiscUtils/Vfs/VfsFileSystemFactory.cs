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
    /// Base class for logic to detect file systems.
    /// </summary>
    public abstract class VfsFileSystemFactory
    {
        /// <summary>
        /// Detects if a stream contains any known file systems.
        /// </summary>
        /// <param name="stream">The stream to inspect.</param>
        /// <returns>A list of file systems (may be empty).</returns>
        public FileSystemInfo[] Detect(Stream stream)
        {
            return Detect(stream, null);
        }

        /// <summary>
        /// Detects if a volume contains any known file systems.
        /// </summary>
        /// <param name="volume">The volume to inspect.</param>
        /// <returns>A list of file systems (may be empty).</returns>
        public FileSystemInfo[] Detect(VolumeInfo volume)
        {
            using (Stream stream = volume.Open())
            {
                return Detect(stream, volume);
            }
        }

        /// <summary>
        /// The logic for detecting file systems.
        /// </summary>
        /// <param name="stream">The stream to inspect.</param>
        /// <param name="volumeInfo">Optionally, information about the volume.</param>
        /// <returns>A list of file systems detected (may be empty).</returns>
        public abstract FileSystemInfo[] Detect(Stream stream, VolumeInfo volumeInfo);
    }
}
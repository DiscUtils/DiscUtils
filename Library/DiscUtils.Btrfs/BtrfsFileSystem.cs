//
// Copyright (c) 2017, Bianco Veigel
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

namespace DiscUtils.Btrfs
{
    /// <summary>
    /// Read-only access to btrfs file system.
    /// </summary>
    public sealed class BtrfsFileSystem : VfsFileSystemFacade
    {
        internal static readonly long[] SuperblockOffsets = {0x10000L, 0x4000000L, 0x4000000000L, 0x4000000000000L};

        /// <summary>
        /// Initializes a new instance of the BtrfsFileSystem class.
        /// </summary>
        /// <param name="stream">The stream containing the btrfs file system.</param>
        public BtrfsFileSystem(Stream stream)
            : base(new VfsBtrfsFileSystem(stream))
        {
        }

        /// <summary>
        /// Initializes a new instance of the BtrfsFileSystem class.
        /// </summary>
        /// <param name="stream">The stream containing the btrfs file system.</param>
        /// <param name="options">Options for opening the file system</param>
        public BtrfsFileSystem(Stream stream, BtrfsFileSystemOptions options)
            :base(new VfsBtrfsFileSystem(stream, options))
        {
            
        }

        internal static bool Detect(Stream stream)
        {
            if (stream.Length < SuperBlock.Length + SuperblockOffsets[0])
            {
                return false;
            }

            stream.Position = SuperblockOffsets[0];
            byte[] superblockData = StreamUtilities.ReadExact(stream, SuperBlock.Length);

            SuperBlock superblock = new SuperBlock();
            superblock.ReadFrom(superblockData, 0);

            return superblock.Magic == SuperBlock.BtrfsMagic;
        }

        /// <summary>
        /// retrieve all subvolumes
        /// </summary>
        /// <returns>a list of subvolumes with id and name</returns>
        public Subvolume[] GetSubvolumes()
        {
            return GetRealFileSystem<VfsBtrfsFileSystem>().GetSubvolumes();
        }
    }
}
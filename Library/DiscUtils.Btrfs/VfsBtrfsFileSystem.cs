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

using System;
using System.IO;
using DiscUtils.Btrfs.Base;
using DiscUtils.Internal;
using DiscUtils.Vfs;

namespace DiscUtils.Btrfs
{
    internal sealed class VfsBtrfsFileSystem :VfsReadOnlyFileSystem<VfsDirEntry, IVfsFile, IVfsDirectory<VfsDirEntry,IVfsFile>, Context>
    {
        public VfsBtrfsFileSystem(Stream stream)
            :base(new DiscFileSystemOptions())
        {
            Context = new Context
            {
                RawStream = stream,
            };
            foreach (var offset in BtrfsFileSystem.SuperblockOffsets)
            {
                if (offset + SuperBlock.Length > stream.Length) break;

                stream.Position = offset;
                var superblockData = Utilities.ReadFully(stream, SuperBlock.Length);
                var superblock = new SuperBlock();
                superblock.ReadFrom(superblockData, 0);

                if (superblock.Magic != SuperBlock.BtrfsMagic)
                    throw new IOException("Invalid Superblock Magic");

                if (Context.SuperBlock == null)
                    Context.SuperBlock = superblock;
                else if (Context.SuperBlock.Generation < superblock.Generation)
                    Context.SuperBlock = superblock;
            }
            if (Context.SuperBlock == null)
                throw new IOException("No Superblock detected");
            Context.ChunkTreeRoot = ReadTree(Context.SuperBlock.ChunkRoot, Context.SuperBlock.ChunkRootLevel);
            Context.RootTreeRoot = ReadTree(Context.SuperBlock.Root, Context.SuperBlock.RootLevel);
        }
        
        public override string FriendlyName
        {
            get { return "Btrfs"; }
        }

        /// <inheritdoc />
        public override string VolumeLabel { get { return Context.SuperBlock.Label; } }
        
        /// <summary>
        /// Size of the Filesystem in bytes
        /// </summary>
        public override long Size
        {
            get { return (long) Context.SuperBlock.TotalBytes; }
        }

        /// <summary>
        /// Used space of the Filesystem in bytes
        /// </summary>
        public override long UsedSpace
        {
            get { return (long)Context.SuperBlock.BytesUsed; }
        }

        /// <summary>
        /// Available space of the Filesystem in bytes
        /// </summary>
        public override long AvailableSpace
        {
            get { return Size - UsedSpace; }
        }

        protected override IVfsFile ConvertDirEntryToFile(VfsDirEntry dirEntry)
        {
            throw new NotImplementedException();
        }

        private NodeHeader ReadTree(ulong logical, byte level)
        {
            var physical = Context.MapToPhysical(logical);
            Context.RawStream.Seek((long)physical, SeekOrigin.Begin);
            var dataSize = level > 0 ? Context.SuperBlock.NodeSize : Context.SuperBlock.LeafSize;
            var buffer = new byte[dataSize];
            Context.RawStream.Read(buffer, 0, buffer.Length);
            return NodeHeader.Create(buffer, 0);
        }
    }
}

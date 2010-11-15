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

namespace DiscUtils.Ext2
{
    using System.IO;
    using DiscUtils.Vfs;

    internal class VfsExt2FileSystem : VfsReadOnlyFileSystem<DirEntry, File, Directory, Ext2Context>, IUnixFileSystem
    {
        private BlockGroup[] _blockGroups;

        public VfsExt2FileSystem(Stream stream)
            : base(new DiscFileSystemOptions())
        {
            stream.Position = 1024;
            byte[] superblockData = Utilities.ReadFully(stream, 1024);

            SuperBlock superblock = new SuperBlock();
            superblock.ReadFrom(superblockData, 0);

            if (superblock.Magic != SuperBlock.Ext2Magic)
            {
                throw new IOException("Invalid superblock magic - probably not an Ext2 file system");
            }

            Context = new Ext2Context()
            {
                RawStream = stream,
                SuperBlock = superblock,
                GetInode = GetInode
            };

            uint numGroups = Utilities.Ceil(superblock.BlocksCount, superblock.BlocksPerGroup);
            long blockDescStart = (superblock.FirstDataBlock + 1) * (long)superblock.BlockSize;

            stream.Position = blockDescStart;
            byte[] blockDescData = Utilities.ReadFully(stream, (int)numGroups * BlockGroup.DescriptorSize);

            _blockGroups = new BlockGroup[numGroups];
            for (int i = 0; i < numGroups; ++i)
            {
                BlockGroup bg = new BlockGroup();
                bg.ReadFrom(blockDescData, i * BlockGroup.DescriptorSize);
                _blockGroups[i] = bg;
            }

            RootDirectory = new Directory(Context, 2);
        }

        public override string VolumeLabel
        {
            get { throw new System.NotImplementedException(); }
        }

        public override string FriendlyName
        {
            get { return Context.SuperBlock.VolumeName; }
        }

        public UnixFileSystemInfo GetUnixFileInfo(string path)
        {
            File file = GetFile(path);

            return new UnixFileSystemInfo()
            {
                FileType = (UnixFileType)((file.Inode.Mode >> 12) & 0xff),
                Permissions = (UnixFilePermissions)(file.Inode.Mode & 0xfff),
                UserId = (((int)file.Inode.UserIdHigh) << 16) | file.Inode.UserIdLow,
                GroupId = (((int)file.Inode.GroupIdHigh) << 16) | file.Inode.GroupIdLow
            };
        }

        protected override File ConvertDirEntryToFile(DirEntry dirEntry)
        {
            if (dirEntry.Record.FileType == DirectoryRecord.FileTypeDirectory)
            {
                return new Directory(Context, dirEntry.Record.Inode);
            }
            else
            {
                return new File(Context, dirEntry.Record.Inode);
            }
        }

        private Inode GetInode(uint inodeNum)
        {
            uint index = inodeNum - 1;

            SuperBlock superBlock = Context.SuperBlock;

            uint group = index / superBlock.InodesPerGroup;
            uint groupOffset = index - (group * superBlock.InodesPerGroup);
            BlockGroup inodeBlockGroup = GetBlockGroup(group);

            uint inodesPerBlock = superBlock.BlockSize / superBlock.InodeSize;
            uint block = groupOffset / inodesPerBlock;
            uint blockOffset = groupOffset - (block * inodesPerBlock);

            Context.RawStream.Position = ((inodeBlockGroup.InodeTableBlock + block) * (long)superBlock.BlockSize) + (blockOffset * superBlock.InodeSize);
            byte[] inodeData = Utilities.ReadFully(Context.RawStream, superBlock.InodeSize);

            return Utilities.ToStruct<Inode>(inodeData, 0);
        }

        private BlockGroup GetBlockGroup(uint index)
        {
            return _blockGroups[index];
        }
    }
}

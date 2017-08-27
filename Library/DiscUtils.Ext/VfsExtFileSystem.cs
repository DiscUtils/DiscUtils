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
    internal sealed class VfsExtFileSystem : VfsReadOnlyFileSystem<DirEntry, File, Directory, Context>, IUnixFileSystem
    {
        internal const IncompatibleFeatures SupportedIncompatibleFeatures =
            IncompatibleFeatures.FileType
            | IncompatibleFeatures.FlexBlockGroups
            | IncompatibleFeatures.Extents
            | IncompatibleFeatures.NeedsRecovery
            | IncompatibleFeatures.SixtyFourBit;

        private readonly BlockGroup[] _blockGroups;

        public VfsExtFileSystem(Stream stream, FileSystemParameters parameters)
            : base(new ExtFileSystemOptions(parameters))
        {
            stream.Position = 1024;
            byte[] superblockData = StreamUtilities.ReadExact(stream, 1024);

            SuperBlock superblock = new SuperBlock();
            superblock.ReadFrom(superblockData, 0);

            if (superblock.Magic != SuperBlock.Ext2Magic)
            {
                throw new IOException("Invalid superblock magic - probably not an Ext file system");
            }

            if (superblock.RevisionLevel == SuperBlock.OldRevision)
            {
                throw new IOException("Old ext revision - not supported");
            }

            if ((superblock.IncompatibleFeatures & ~SupportedIncompatibleFeatures) != 0)
            {
                throw new IOException("Incompatible ext features present: " +
                                      (superblock.IncompatibleFeatures & ~SupportedIncompatibleFeatures));
            }

            Context = new Context
            {
                RawStream = stream,
                SuperBlock = superblock,
                Options = (ExtFileSystemOptions)Options
            };

            uint numGroups = MathUtilities.Ceil(superblock.BlocksCount, superblock.BlocksPerGroup);
            long blockDescStart = (superblock.FirstDataBlock + 1) * (long)superblock.BlockSize;

            stream.Position = blockDescStart;
            var bgDescSize = superblock.Has64Bit ? BlockGroup64.DescriptorSize64 : BlockGroup.DescriptorSize;
            byte[] blockDescData = StreamUtilities.ReadExact(stream, (int)numGroups * bgDescSize);

            _blockGroups = new BlockGroup[numGroups];
            for (int i = 0; i < numGroups; ++i)
            {
                BlockGroup bg = superblock.Has64Bit ? new BlockGroup64() : new BlockGroup();
                bg.ReadFrom(blockDescData, i * bgDescSize);
                _blockGroups[i] = bg;
            }

            var journalSuperBlock = new JournalSuperBlock();
            if (superblock.JournalInode != 0)
            {
                var journalInode = GetInode(superblock.JournalInode);
                var journalDataStream = journalInode.GetContentBuffer(Context);
                var journalData = StreamUtilities.ReadExact(journalDataStream, 0, 1024 + 12);
                journalSuperBlock.ReadFrom(journalData, 0);
                Context.JournalSuperblock = journalSuperBlock;
            }

            RootDirectory = new Directory(Context, 2, GetInode(2));
        }

        public override string FriendlyName
        {
            get { return "EXT-family"; }
        }

        public override string VolumeLabel
        {
            get { return Context.SuperBlock.VolumeName; }
        }

        public UnixFileSystemInfo GetUnixFileInfo(string path)
        {
            File file = GetFile(path);
            Inode inode = file.Inode;

            UnixFileType fileType = (UnixFileType)((inode.Mode >> 12) & 0xff);

            uint deviceId = 0;
            if (fileType == UnixFileType.Character || fileType == UnixFileType.Block)
            {
                if (inode.DirectBlocks[0] != 0)
                {
                    deviceId = inode.DirectBlocks[0];
                }
                else
                {
                    deviceId = inode.DirectBlocks[1];
                }
            }

            return new UnixFileSystemInfo
            {
                FileType = fileType,
                Permissions = (UnixFilePermissions)(inode.Mode & 0xfff),
                UserId = (inode.UserIdHigh << 16) | inode.UserIdLow,
                GroupId = (inode.GroupIdHigh << 16) | inode.GroupIdLow,
                Inode = file.InodeNumber,
                LinkCount = inode.LinksCount,
                DeviceId = deviceId
            };
        }

        protected override File ConvertDirEntryToFile(DirEntry dirEntry)
        {
            Inode inode = GetInode(dirEntry.Record.Inode);
            if (dirEntry.Record.FileType == DirectoryRecord.FileTypeDirectory)
            {
                return new Directory(Context, dirEntry.Record.Inode, inode);
            }
            if (dirEntry.Record.FileType == DirectoryRecord.FileTypeSymlink)
            {
                return new Symlink(Context, dirEntry.Record.Inode, inode);
            }
            return new File(Context, dirEntry.Record.Inode, inode);
        }

        private Inode GetInode(uint inodeNum)
        {
            uint index = inodeNum - 1;

            SuperBlock superBlock = Context.SuperBlock;

            uint group = index / superBlock.InodesPerGroup;
            uint groupOffset = index - group * superBlock.InodesPerGroup;
            BlockGroup inodeBlockGroup = GetBlockGroup(group);

            uint inodesPerBlock = superBlock.BlockSize / superBlock.InodeSize;
            uint block = groupOffset / inodesPerBlock;
            uint blockOffset = groupOffset - block * inodesPerBlock;

            Context.RawStream.Position = (inodeBlockGroup.InodeTableBlock + block) * (long)superBlock.BlockSize +
                                         blockOffset * superBlock.InodeSize;
            byte[] inodeData = StreamUtilities.ReadExact(Context.RawStream, superBlock.InodeSize);

            return EndianUtilities.ToStruct<Inode>(inodeData, 0);
        }

        private BlockGroup GetBlockGroup(uint index)
        {
            return _blockGroups[index];
        }

        /// <summary>
        /// Size of the Filesystem in bytes
        /// </summary>
        public override long Size
        {
            get
            {
                var superBlock = Context.SuperBlock;
                ulong blockCount = (superBlock.BlocksCountHigh << 32) | superBlock.BlocksCount;
                ulong inodeSize = superBlock.InodesCount * superBlock.InodeSize;
                ulong overhead = 0;
                ulong journalSize = 0;
                if (superBlock.OverheadBlocksCount != 0)
                {
                    overhead = superBlock.OverheadBlocksCount* superBlock.BlockSize;
                }
                if (Context.JournalSuperblock != null)
                {
                    journalSize = Context.JournalSuperblock.MaxLength* Context.JournalSuperblock.BlockSize;
                }
                return (long) (superBlock.BlockSize* blockCount - (inodeSize + overhead + journalSize));
            }
        }

        /// <summary>
        /// Used space of the Filesystem in bytes
        /// </summary>
        public override long UsedSpace
        {
            get { return Size - AvailableSpace; }
        }

        /// <summary>
        /// Available space of the Filesystem in bytes
        /// </summary>
        public override long AvailableSpace
        {
            get
            {
                var superBlock = Context.SuperBlock;
                if (superBlock.Has64Bit)
                {
                    ulong free = 0;
                    //ext4 64Bit Feature
                    foreach (BlockGroup64 blockGroup in _blockGroups)
                    {
                        free += (uint) (blockGroup.FreeBlocksCountHigh << 16 | blockGroup.FreeBlocksCount);
                    }
                    return (long) (superBlock.BlockSize* free);
                }
                else
                {
                    ulong free = 0;
                    //ext4 64Bit Feature
                    foreach (var blockGroup in _blockGroups)
                    {
                        free += blockGroup.FreeBlocksCount;
                    }
                    return (long) (superBlock.BlockSize* free);
                }
            }
        }
    }
}

//
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Xfs
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using DiscUtils.Streams;

    internal class Inode : IByteArraySerializable
    {
        public Inode(ulong number, Context context)
        {
            var sb = context.SuperBlock;
            RelativeInodeNumber = (uint) (number & sb.RelativeInodeMask);
            AllocationGroup = (uint) ((number & sb.AgInodeMask) >> (sb.AgBlocksLog2 + sb.InodesPerBlockLog2));
            AgBlock = (uint) ((number >> context.SuperBlock.InodesPerBlockLog2) & XFS_INO_MASK(context.SuperBlock.AgBlocksLog2));
            BlockOffset = (uint) (number & XFS_INO_MASK(sb.InodesPerBlockLog2));
        }

        private static uint XFS_INO_MASK(int k)
        {
            return (1u << k) - 1;
        }

        public Inode(uint allocationGroup, uint relInode)
        {
            AllocationGroup = allocationGroup;
            RelativeInodeNumber = relInode;
        }

        public uint AllocationGroup { get; private set; }

        public uint RelativeInodeNumber { get; private set; }

        public uint AgBlock { get; private set; }

        public uint BlockOffset { get; private set; }

        public const ushort InodeMagic = 0x494e;
        /// <summary>
        /// The inode signature where these two bytes are 0x494e, or "IN" in ASCII.
        /// </summary>
        public ushort Magic { get; private set; }

        /// <summary>
        /// Specifies the mode access bits and type of file using the standard S_Ixxx values defined in stat.h.
        /// </summary>
        public ushort Mode { get; private set; }

        /// <summary>
        /// Specifies the inode version which currently can only be 1 or 2. The inode version specifies the
        /// usage of the di_onlink, di_nlink and di_projid values in the inode core.Initially, inodes
        /// are created as v1 but can be converted on the fly to v2 when required.
        /// </summary>
        public byte Version { get; private set; }

        /// <summary>
        /// Specifies the format of the data fork in conjunction with the di_mode type. This can be one of
        /// several values. For directories and links, it can be "local" where all metadata associated with the
        /// file is within the inode, "extents" where the inode contains an array of extents to other filesystem
        /// blocks which contain the associated metadata or data or "btree" where the inode contains a
        /// B+tree root node which points to filesystem blocks containing the metadata or data. Migration
        /// between the formats depends on the amount of metadata associated with the inode. "dev" is
        /// used for character and block devices while "uuid" is currently not used.
        /// </summary>
        public InodeFormat Format { get; private set; }

        /// <summary>
        /// In v1 inodes, this specifies the number of links to the inode from directories. When the number
        /// exceeds 65535, the inode is converted to v2 and the link count is stored in di_nlink.
        /// </summary>
        public ushort Onlink { get; private set; }

        /// <summary>
        /// Specifies the owner's UID of the inode.
        /// </summary>
        public uint UserId { get; private set; }

        /// <summary>
        /// Specifies the owner's GID of the inode.
        /// </summary>
        public uint GroupId { get; private set; }

        /// <summary>
        /// Specifies the number of links to the inode from directories. This is maintained for both inode
        /// versions for current versions of XFS.Old versions of XFS did not support v2 inodes, and
        /// therefore this value was never updated and was classed as reserved space (part of <see cref="Padding"/>).
        /// </summary>
        public uint Nlink { get; private set; }

        /// <summary>
        /// Specifies the owner's project ID in v2 inodes. An inode is converted to v2 if the project ID is set.
        /// This value must be zero for v1 inodes.
        /// </summary>
        public ushort ProjectId { get; private set; }

        /// <summary>
        /// Reserved, must be zero.
        /// </summary>
        public byte[] Padding { get; private set; }

        /// <summary>
        /// Incremented on flush.
        /// </summary>
        public ushort FlushIterator { get; private set; }

        /// <summary>
        /// Specifies the last access time of the files using UNIX time conventions the following structure.
        /// This value maybe undefined if the filesystem is mounted with the "noatime" option.
        /// </summary>
        public DateTime AccessTime { get; private set; }

        /// <summary>
        /// Specifies the last time the file was modified.
        /// </summary>
        public DateTime ModificationTime { get; private set; }

        /// <summary>
        /// Specifies when the inode's status was last changed.
        /// </summary>
        public DateTime CreationTime { get; private set; }

        /// <summary>
        /// Specifies the EOF of the inode in bytes. This can be larger or smaller than the extent space
        /// (therefore actual disk space) used for the inode.For regular files, this is the filesize in bytes,
        /// directories, the space taken by directory entries and for links, the length of the symlink.
        /// </summary>
        public ulong Length { get; private set; }

        /// <summary>
        /// Specifies the number of filesystem blocks used to store the inode's data including relevant
        /// metadata like B+trees.This does not include blocks used for extended attributes.
        /// </summary>
        public ulong BlockCount { get; private set; }

        /// <summary>
        /// Specifies the extent size for filesystems with real-time devices and an extent size hint for
        /// standard filesystems. For normal filesystems, and with directories, the
        /// XFS_DIFLAG_EXTSZINHERIT flag must be set in di_flags if this field is used.Inodes
        /// created in these directories will inherit the di_extsize value and have
        /// XFS_DIFLAG_EXTSIZE set in their di_flags. When a file is written to beyond allocated
        /// space, XFS will attempt to allocate additional disk space based on this value.
        /// </summary>
        public uint ExtentSize { get; private set; }

        /// <summary>
        /// Specifies the number of data extents associated with this inode.
        /// </summary>
        public uint Extents { get; private set; }

        /// <summary>
        /// Specifies the number of extended attribute extents associated with this inode.
        /// </summary>
        public ushort AttributeExtents { get; private set; }

        /// <summary>
        /// Specifies the offset into the inode's literal area where the extended attribute fork starts. This is
        /// an 8-bit value that is multiplied by 8 to determine the actual offset in bytes(ie.attribute data is
        /// 64-bit aligned). This also limits the maximum size of the inode to 2048 bytes.This value is
        /// initially zero until an extended attribute is created.When in attribute is added, the nature of
        /// di_forkoff depends on the XFS_SB_VERSION2_ATTR2BIT flag in the superblock.
        /// </summary>
        public byte Forkoff { get; private set; }

        /// <summary>
        /// Specifies the format of the attribute fork. This uses the same values as di_format, but
        /// restricted to "local", "extents" and "btree" formats for extended attribute data.
        /// </summary>
        public sbyte AttributeFormat { get; private set; }

        /// <summary>
        /// DMAPI event mask.
        /// </summary>
        public uint DmApiEventMask { get; private set; }

        /// <summary>
        /// DMAPI state.
        /// </summary>
        public ushort DmState { get; private set; }

        /// <summary>
        /// Specifies flags associated with the inode.
        /// </summary>
        public InodeFlags Flags { get; private set; }

        /// <summary>
        /// A generation number used for inode identification. This is used by tools that do inode scanning
        /// such as backup tools and xfsdump. An inode's generation number can change by unlinking and
        /// creating a new file that reuses the inode.
        /// </summary>
        public uint Generation { get; private set; }
        
        public UnixFileType FileType { get { return (UnixFileType) ((Mode >> 12) & 0xF); } }

        public byte[] DataFork { get; private set; }

        public int Size
        {
            get { return 96; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Magic = EndianUtilities.ToUInt16BigEndian(buffer, offset);
            Mode = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x2);
            Version = buffer[offset + 0x4];
            Format = (InodeFormat)buffer[offset + 0x5];
            Onlink = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x6);
            UserId = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x8);
            GroupId = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0xC);
            Nlink = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x10);
            ProjectId = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x14);
            Padding = EndianUtilities.ToByteArray(buffer, offset + 0x16, 8);
            FlushIterator = EndianUtilities.ToUInt16BigEndian(buffer, 0x1E);
            AccessTime = ReadTimestamp(buffer, offset + 0x20);
            ModificationTime = ReadTimestamp(buffer, offset + 0x28);
            CreationTime = ReadTimestamp(buffer, offset + 0x30);
            Length = EndianUtilities.ToUInt64BigEndian(buffer, offset + 0x38);
            BlockCount = EndianUtilities.ToUInt64BigEndian(buffer, offset + 0x40);
            ExtentSize = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x48);
            Extents = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x4C);
            AttributeExtents = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x50);
            Forkoff = buffer[offset + 0x52];
            AttributeFormat = (sbyte) buffer[offset + 0x53];
            DmApiEventMask = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x54);
            DmState = EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x58);
            Flags = (InodeFlags) EndianUtilities.ToUInt16BigEndian(buffer, offset + 0x5A);
            Generation = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x5C);
            var dfLength = (Forkoff*8) - 0x64;
            if (dfLength < 0)
            {
                dfLength = buffer.Length - offset - 0x64;
            }
            DataFork = EndianUtilities.ToByteArray(buffer, offset + 0x64, dfLength);
            return Size;
        }

        private DateTime ReadTimestamp(byte[] buffer, int offset)
        {
            var seconds = EndianUtilities.ToUInt32BigEndian(buffer, offset);
            var nanoSeconds = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x4);
            return ((long)seconds).FromUnixTimeSeconds().AddTicks(nanoSeconds/100).LocalDateTime;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public Extent[] GetExtents()
        {
            var result = new Extent[Extents];
            int offset = Forkoff;
            for (int i = 0; i < Extents; i++)
            {
                var extent = new Extent();
                offset += extent.ReadFrom(DataFork, offset);
                result[i] = extent;
            }
            return result;
        }

        public IBuffer GetContentBuffer(Context context)
        {
            if (Format == InodeFormat.Local)
            {
                return new StreamBuffer(new MemoryStream(DataFork), Ownership.Dispose);
            }
            else if (Format == InodeFormat.Extents)
            {
                var extents = GetExtents();
                return BufferFromExtentList(context, extents);
            }
            else if (Format == InodeFormat.Btree)
            {
                var tree = new BTreeExtentRoot();
                tree.ReadFrom(DataFork, 0);
                tree.LoadBtree(context);
                var extents = tree.GetExtents();
                return BufferFromExtentList(context, extents);
            }
            throw new NotImplementedException();
        }

        public IBuffer BufferFromExtentList(Context context, IList<Extent> extents)
        {
            var builderExtents = new List<BuilderExtent>(extents.Count);
            foreach (var extent in extents)
            {
                var blockOffset = extent.GetOffset(context);
                var substream = new SubStream(context.RawStream, blockOffset, (long)extent.BlockCount*context.SuperBlock.Blocksize);
                builderExtents.Add(new BuilderSparseStreamExtent((long) extent.StartOffset * context.SuperBlock.Blocksize, substream));
            }
            return new StreamBuffer(new ExtentStream((long) this.Length, builderExtents), Ownership.Dispose);
        }
    }
}

//
// Copyright (c) 2008-2011, Kenneth Bell
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
    using DiscUtils.Internal;
    using System;

    internal class SuperBlock : IByteArraySerializable
    {
        public const uint XfsMagic = 0x58465342;

        public const uint XfsSbVersionNumbits = 0x000f;

        public const uint XfsSbVersion5 = 5;

        /// <summary>
        /// magic number == XFS_SB_MAGIC
        /// </summary>
        public uint Magic { get; private set; }
        /// <summary>
        /// logical block size, bytes
        /// </summary>
        public uint Blocksize { get; private set; }

        /// <summary>
        /// number of data blocks
        /// </summary>
        public ulong DataBlocks { get; private set; }

        /// <summary>
        /// number of realtime blocks
        /// </summary>
        public ulong RealtimeBlocks { get; private set; }

        /// <summary>
        /// number of realtime extents
        /// </summary>
        public ulong RealtimeExtents { get; private set; }

        /// <summary>
        /// user-visible file system unique id
        /// </summary>
        public Guid UniqueId { get; private set; }

        /// <summary>
        /// starting block of log if internal
        /// </summary>
        public ulong Logstart { get; private set; }

        /// <summary>
        /// root inode number
        /// </summary>
        public ulong RootInode { get; private set; }

        /// <summary>
        /// bitmap inode for realtime extents
        /// </summary>
        public ulong RealtimeBitmapInode { get; private set; }

        /// <summary>
        /// summary inode for rt bitmap
        /// </summary>
        public ulong RealtimeSummaryInode { get; private set; }

        /// <summary>
        /// realtime extent size, blocks
        /// </summary>
        public uint RealtimeExtentSize { get; private set; }

        /// <summary>
        /// size of an allocation group
        /// </summary>
        public uint AgBlocks { get; private set; }

        /// <summary>
        /// number of allocation groups
        /// </summary>
        public uint AgCount { get; private set; }

        /// <summary>
        /// number of rt bitmap blocks
        /// </summary>
        public uint RealtimeBitmapBlocks { get; private set; }

        /// <summary>
        /// number of log blocks
        /// </summary>
        public uint LogBlocks { get; private set; }

        /// <summary>
        /// header version == XFS_SB_VERSION
        /// </summary>
        public ushort Version { get; private set; }

        /// <summary>
        /// volume sector size, bytes
        /// </summary>
        public ushort SectorSize { get; private set; }

        /// <summary>
        /// inode size, bytes
        /// </summary>
        public ushort InodeSize { get; private set; }

        /// <summary>
        /// inodes per block
        /// </summary>
        public ushort InodesPerBlock { get; private set; }

        /// <summary>
        /// file system name
        /// </summary>
        public string FilesystemName { get; private set; }

        /// <summary>
        /// log2 of <see cref="Blocksize"/>
        /// </summary>
        public byte BlocksizeLog2 { get; private set; }

        /// <summary>
        /// log2 of <see cref="SectorSize"/>
        /// </summary>
        public byte SectorSizeLog2 { get; private set; }

        /// <summary>
        /// log2 of <see cref="InodeSize"/>
        /// </summary>
        public byte InodeSizeLog2 { get; private set; }

        /// <summary>
        /// log2 of <see cref="InodesPerBlock"/>
        /// </summary>
        public byte InodesPerBlockLog2 { get; private set; }

        /// <summary>
        /// log2 of <see cref="AgBlocks"/> (rounded up)
        /// </summary>
        public byte AgBlocksLog2 { get; private set; }

        /// <summary>
        /// log2 of <see cref="RealtimeExtents"/>
        /// </summary>
        public byte RealtimeExtentsLog2 { get; private set; }

        /// <summary>
        /// mkfs is in progress, don't mount
        /// </summary>
        public byte InProgress { get; private set; }

        /// <summary>
        /// max % of fs for inode space
        /// </summary>
        public byte InodesMaxPercent { get; private set; }

        /*
        * These fields must remain contiguous.  If you really
        * want to change their layout, make sure you fix the
        * code in xfs_trans_apply_sb_deltas().
        */
        #region statistics

        /// <summary>
        /// allocated inodes
        /// </summary>
        public ulong AllocatedInodes { get; private set; }

        /// <summary>
        /// free inodes
        /// </summary>
        public ulong FreeInodes { get; private set; }

        /// <summary>
        /// free data blocks
        /// </summary>
        public ulong FreeDataBlocks { get; private set; }

        /// <summary>
        /// free realtime extents
        /// </summary>
        public ulong FreeRealtimeExtents { get; private set; }

        #endregion

        /// <summary>
        /// user quota inode
        /// </summary>
        public ulong UserQuotaInode { get; private set; }

        /// <summary>
        /// group quota inode
        /// </summary>
        public ulong GroupQuotaInode { get; private set; }

        /// <summary>
        /// quota flags
        /// </summary>
        public ushort QuotaFlags { get; private set; }

        /// <summary>
        /// misc. flags
        /// </summary>
        public byte Flags { get; private set; }

        /// <summary>
        /// shared version number
        /// </summary>
        public byte SharedVersionNumber { get; private set; }

        /// <summary>
        /// inode chunk alignment, fsblocks
        /// </summary>
        public uint InodeChunkAlignment { get; private set; }

        /// <summary>
        /// stripe or raid unit
        /// </summary>
        public uint Unit { get; private set; }

        /// <summary>
        /// stripe or raid width
        /// </summary>
        public uint Width { get; private set; }

        /// <summary>
        /// log2 of dir block size (fsbs)
        /// </summary>
        public byte DirBlockLog2 { get; private set; }

        /// <summary>
        /// log2 of the log sector size
        /// </summary>
        public byte LogSectorSizeLog2 { get; private set; }

        /// <summary>
        /// sector size for the log, bytes
        /// </summary>
        public ushort LogSectorSize { get; private set; }

        /// <summary>
        /// stripe unit size for the log
        /// </summary>
        public uint LogUnitSize { get; private set; }

        /// <summary>
        /// additional feature bits
        /// </summary>
        public uint Features2 { get; private set; }

        /*
        * bad features2 field as a result of failing to pad the sb structure to
        * 64 bits. Some machines will be using this field for features2 bits.
        * Easiest just to mark it bad and not use it for anything else.
        *
        * This is not kept up to date in memory; it is always overwritten by
        * the value in sb_features2 when formatting the incore superblock to
        * the disk buffer.
        */
        /// <summary>
        /// bad features2 field as a result of failing to pad the sb structure to
        /// 64 bits. Some machines will be using this field for features2 bits.
        /// Easiest just to mark it bad and not use it for anything else.
        /// 
        /// This is not kept up to date in memory; it is always overwritten by
        /// the value in sb_features2 when formatting the incore superblock to
        /// the disk buffer.
        /// </summary>
        public uint BadFeatures2 { get; private set; }

        /* version 5 superblock fields start here */

        /* feature masks */
        public uint CompatibleFeatures { get; private set; }

        public ReadOnlyCompatibleFeatures ReadOnlyCompatibleFeatures { get; private set; }

        public uint IncompatibleFeatures { get; private set; }

        public uint LogIncompatibleFeatures { get; private set; }

        /// <summary>
        /// superblock crc
        /// </summary>
        public uint Crc { get; private set; }

        /// <summary>
        /// sparse inode chunk alignment
        /// </summary>
        public uint SparseInodeAlignment { get; private set; }

        /// <summary>
        /// project quota inode
        /// </summary>
        public ulong ProjectQuotaInode { get; private set; }

        /// <summary>
        /// last write sequence
        /// </summary>
        public long Lsn { get; private set; }

        /// <summary>
        /// metadata file system unique id
        /// </summary>
        public Guid MetaUuid { get; private set; }

        /* must be padded to 64 bit alignment */

        public uint RelativeInodeMask { get; private set; }

        public uint AgInodeMask { get; private set; }

        public uint DirBlockSize { get; private set; }

        public int Size
        {
            get
            {
                if (SbVersion >= XfsSbVersion5)
                {
                    return 264;
                }
                return 208;
            }
        }

        public uint SbVersion
        {
            get { return Version & XfsSbVersionNumbits; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Magic = Utilities.ToUInt32BigEndian(buffer, offset);
            Blocksize = Utilities.ToUInt32BigEndian(buffer, offset + 0x4);
            DataBlocks = Utilities.ToUInt64BigEndian(buffer, offset + 0x8);
            RealtimeBlocks = Utilities.ToUInt64BigEndian(buffer, offset + 0x10);
            RealtimeExtents = Utilities.ToUInt64BigEndian(buffer, offset + 0x18);
            UniqueId = Utilities.ToGuidBigEndian(buffer, offset + 0x20);
            Logstart = Utilities.ToUInt64BigEndian(buffer, offset + 0x30);
            RootInode = Utilities.ToUInt64BigEndian(buffer, offset + 0x38);
            RealtimeBitmapInode = Utilities.ToUInt64BigEndian(buffer, offset + 0x40);
            RealtimeSummaryInode = Utilities.ToUInt64BigEndian(buffer, offset + 0x48);
            RealtimeExtentSize = Utilities.ToUInt32BigEndian(buffer, offset + 0x50);
            AgBlocks = Utilities.ToUInt32BigEndian(buffer, offset + 0x54);
            AgCount = Utilities.ToUInt32BigEndian(buffer, offset + 0x58);
            RealtimeBitmapBlocks = Utilities.ToUInt32BigEndian(buffer, offset + 0x5C);
            LogBlocks = Utilities.ToUInt32BigEndian(buffer, offset + 0x60);
            Version = Utilities.ToUInt16BigEndian(buffer, offset + 0x64);
            SectorSize = Utilities.ToUInt16BigEndian(buffer, offset + 0x66);
            InodeSize = Utilities.ToUInt16BigEndian(buffer, offset + 0x68);
            InodesPerBlock = Utilities.ToUInt16BigEndian(buffer, offset + 0x6A);
            FilesystemName = Utilities.BytesToZString(buffer, offset + 0x6C, 12);
            BlocksizeLog2 = buffer[offset + 0x78];
            SectorSizeLog2 = buffer[offset + 0x79];
            InodeSizeLog2 = buffer[offset + 0x7A];
            InodesPerBlockLog2 = buffer[offset + 0x7B];
            AgBlocksLog2 = buffer[offset + 0x7C];
            RealtimeExtentsLog2 = buffer[offset + 0x7D];
            InProgress = buffer[offset + 0x7E];
            InodesMaxPercent = buffer[offset + 0x7F];
            AllocatedInodes = Utilities.ToUInt64BigEndian(buffer, offset + 0x80);
            FreeInodes = Utilities.ToUInt64BigEndian(buffer, offset + 0x88);
            FreeDataBlocks = Utilities.ToUInt64BigEndian(buffer, offset + 0x90);
            FreeRealtimeExtents = Utilities.ToUInt64BigEndian(buffer, offset + 0x98);
            UserQuotaInode = Utilities.ToUInt64BigEndian(buffer, offset + 0xA0);
            GroupQuotaInode = Utilities.ToUInt64BigEndian(buffer, offset + 0xA8);
            QuotaFlags = Utilities.ToUInt16BigEndian(buffer, offset + 0xB0);
            Flags = buffer[offset + 0xB2];
            SharedVersionNumber = buffer[offset + 0xB3];
            InodeChunkAlignment = Utilities.ToUInt32BigEndian(buffer, offset + 0xB4);
            Unit = Utilities.ToUInt32BigEndian(buffer, offset + 0xB8);
            Width = Utilities.ToUInt32BigEndian(buffer, offset + 0xBC);
            DirBlockLog2 = buffer[offset + 0xC0];
            LogSectorSizeLog2 = buffer[offset + 0xC1];
            LogSectorSize = Utilities.ToUInt16BigEndian(buffer, offset + 0xC2);
            LogUnitSize = Utilities.ToUInt32BigEndian(buffer, offset + 0xC4);
            Features2 = Utilities.ToUInt32BigEndian(buffer, offset + 0xC8);
            BadFeatures2 = Utilities.ToUInt32BigEndian(buffer, offset + 0xCC);
            return 208;
        }

        public int ReadFromVersion5(byte[] buffer, int offset)
        {
            CompatibleFeatures = Utilities.ToUInt32BigEndian(buffer, offset);
            ReadOnlyCompatibleFeatures = (ReadOnlyCompatibleFeatures)Utilities.ToUInt32BigEndian(buffer, offset + 0x04);
            IncompatibleFeatures = Utilities.ToUInt32BigEndian(buffer, offset + 0x08);
            LogIncompatibleFeatures = Utilities.ToUInt32BigEndian(buffer, offset + 0x0C);
            Crc = Utilities.ToUInt32BigEndian(buffer, offset + 0x10);
            SparseInodeAlignment = Utilities.ToUInt32BigEndian(buffer, offset + 0x14);
            ProjectQuotaInode = Utilities.ToUInt64BigEndian(buffer, offset + 0x18);
            Lsn = Utilities.ToInt64BigEndian(buffer, offset + 0x20);
            MetaUuid = Utilities.ToGuidBigEndian(buffer, offset + 0x28);

            var agOffset = AgBlocksLog2 + InodesPerBlockLog2;
            RelativeInodeMask = 0xffffffff >> (32-agOffset);
            AgInodeMask = ~RelativeInodeMask;

            DirBlockSize = Blocksize << DirBlockLog2;
            return 56;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public uint xfs_btree_compute_maxlevels()
        {
            var len = AgBlocks;
            uint level;
            var limits = new uint[] {xfs_rmapbt_maxrecs(false), xfs_rmapbt_maxrecs(true)};
            ulong maxblocks = (len + limits[0] - 1) / limits[0];
            for (level = 1; maxblocks > 1; level++)
                maxblocks = (maxblocks + limits[1] - 1) / limits[1];
            return level;
        }

        private uint xfs_rmapbt_maxrecs(bool leaf)
        {
            var blocklen = Blocksize - 56;
            if (leaf)
                return blocklen/24;
            return blocklen/(2*20 + 4);
        }
    }
}

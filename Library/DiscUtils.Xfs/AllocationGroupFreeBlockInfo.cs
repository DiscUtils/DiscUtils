//
// Copyright (c) 2016, Bianco Veigel
// Copyright (c) 2017, Timo Walter
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
    using DiscUtils.Streams;
    using System;

    internal class AllocationGroupFreeBlockInfo : IByteArraySerializable
    {
        public const uint AgfMagic = 0x58414746;
        public const uint BtreeMagicOffset = 0x41425442;
        public const uint BtreeMagicCount = 0x41425443;
        /// <summary>
        /// Specifies the magic number for the AGF sector: "XAGF" (0x58414746).
        /// </summary>
        public uint Magic { get; private set; }

        /// <summary>
        /// Set to XFS_AGF_VERSION which is currently 1.
        /// </summary>
        public uint Version { get; private set; }

        /// <summary>
        /// Specifies the AG number for the sector.
        /// </summary>
        public uint SequenceNumber { get; private set; }

        /// <summary>
        /// Specifies the size of the AG in filesystem blocks. For all AGs except the last, this must be equal
        /// to the superblock's <see cref="SuperBlock.AgBlocks"/> value. For the last AG, this could be less than the
        /// <see cref="SuperBlock.AgBlocks"/> value. It is this value that should be used to determine the size of the AG.
        /// </summary>
        public uint Length { get; private set; }

        /// <summary>
        /// Specifies the block number for the root of the two free space B+trees.
        /// </summary>
        public uint[] RootBlockNumbers { get; private set; }

        public uint Spare0 { get; private set; }

        /// <summary>
        /// Specifies the level or depth of the two free space B+trees. For a fresh AG, this will be one, and
        /// the "roots" will point to a single leaf of level 0.
        /// </summary>
        public uint[] Levels;

        public uint Spare1 { get; private set; }

        /// <summary>
        /// Specifies the index of the first "free list" block.
        /// </summary>
        public uint FreeListFirst { get; private set; }

        /// <summary>
        /// Specifies the index of the last "free list" block.
        /// </summary>
        public uint FreeListLast { get; private set; }

        /// <summary>
        /// Specifies the number of blocks in the "free list".
        /// </summary>
        public uint FreeListCount { get; private set; }

        /// <summary>
        /// Specifies the current number of free blocks in the AG.
        /// </summary>
        public uint FreeBlocks { get; private set; }

        /// <summary>
        /// Specifies the number of blocks of longest contiguous free space in the AG.
        /// </summary>
        public uint Longest { get; private set; }

        /// <summary>
        /// Specifies the number of blocks used for the free space B+trees. This is only used if the
        /// XFS_SB_VERSION2_LAZYSBCOUNTBIT bit is set in <see cref="SuperBlock.Features2"/>.
        /// </summary>
        public uint BTreeBlocks { get; private set; }

        /// <summary>
        /// stores a sorted array of block offset and block counts in the leaves of the B+tree, sorted by the offset
        /// </summary>
        public BtreeHeader FreeSpaceOffset { get; private set; }

        /// <summary>
        /// stores a sorted array of block offset and block counts in the leaves of the B+tree, sorted by the count or size
        /// </summary>
        public BtreeHeader FreeSpaceCount { get; private set; }

        public Guid UniqueId { get; private set; }

        /// <summary>
        /// last write sequence
        /// </summary>
        public ulong Lsn { get; private set; }

        public uint Crc { get; private set; }

        public int Size { get; }

        private uint SbVersion { get; }

        public AllocationGroupFreeBlockInfo(SuperBlock superBlock)
        {
            SbVersion = superBlock.SbVersion;
            Size = SbVersion >= 5 ? 92 : 64;
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Magic = EndianUtilities.ToUInt32BigEndian(buffer, offset);
            Version = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x4);
            SequenceNumber = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x8);
            Length = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0xC);
            RootBlockNumbers = new uint[2];
            RootBlockNumbers[0] = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x10);
            RootBlockNumbers[1] = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x14);
            Spare0 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x18);
            Levels = new uint[2];

            Levels[0] = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x1C);
            Levels[1] = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x20);
            Spare1 = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x24);
            FreeListFirst = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x28);
            FreeListLast = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x2C);
            FreeListCount = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x30);
            FreeBlocks = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x34);
            Longest = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x38);
            BTreeBlocks = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x3C);
            if (SbVersion >= 5)
            {
                UniqueId = EndianUtilities.ToGuidBigEndian(buffer, offset + 0x40);
                Lsn = EndianUtilities.ToUInt64BigEndian(buffer, offset + 0x50);
                Crc = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x58);
            }

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

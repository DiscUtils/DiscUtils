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
    using System.IO;

    internal class AllocationGroupInodeBtreeInfo : IByteArraySerializable
    {
        public const uint AgiMagic = 0x58414749;

        /// <summary>
        /// Specifies the magic number for the AGI sector: "XAGI" (0x58414749)
        /// </summary>
        public uint Magic { get; private set; }

        /// <summary>
        /// Set to XFS_AGI_VERSION which is currently 1.
        /// </summary>
        public uint Version { get; private set; }

        /// <summary>
        /// Specifies the AG number for the sector.
        /// </summary>
        public uint SequenceNumber { get; private set; }

        /// <summary>
        /// Specifies the size of the AG in filesystem blocks.
        /// </summary>
        public uint Length { get; private set; }

        /// <summary>
        /// Specifies the number of inodes allocated for the AG.
        /// </summary>
        public uint Count { get; private set; }

        /// <summary>
        /// Specifies the block number in the AG containing the root of the inode B+tree.
        /// </summary>
        public uint Root { get; private set; }

        /// <summary>
        /// Specifies the number of levels in the inode B+tree.
        /// </summary>
        public uint Level { get; private set; }

        /// <summary>
        /// Specifies the number of free inodes in the AG.
        /// </summary>
        public uint FreeCount { get; private set; }

        /// <summary>
        /// Specifies AG relative inode number most recently allocated.
        /// </summary>
        public uint NewInode { get; private set; }

        /// <summary>
        /// Deprecated and not used, it's always set to NULL (-1).
        /// </summary>
        [Obsolete]
        public int DirInode => -1;

        /// <summary>
        /// Hash table of unlinked (deleted) inodes that are still being referenced.
        /// </summary>
        public int[] Unlinked { get; private set; }

        /// <summary>
        /// root of the inode B+tree
        /// </summary>
        public BtreeHeader RootInodeBtree { get; private set; }

        public Guid UniqueId { get; private set; }

        /// <summary>
        /// last write sequence
        /// </summary>
        public ulong Lsn { get; private set; }

        public uint Crc { get; private set; }

        public int Size { get; private set; }

        private uint SbVersion { get; }

        public AllocationGroupInodeBtreeInfo(SuperBlock superBlock)
        {
            SbVersion = superBlock.SbVersion;
            Size = SbVersion >= 5 ? 334 : 296;
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Magic = EndianUtilities.ToUInt32BigEndian(buffer, offset);
            Version = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x4);
            SequenceNumber = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x8);
            Length = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0xc);
            Count = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x10);
            Root = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x14);
            Level = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x18);
            FreeCount = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x1c);
            NewInode = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x20);
            Unlinked = new int[64];
            for (int i = 0; i < Unlinked.Length; i++)
            {
                Unlinked[i] = EndianUtilities.ToInt32BigEndian(buffer, offset + 0x28 + i*0x4);
            }
            if (SbVersion >= 5)
            {
                UniqueId = EndianUtilities.ToGuidBigEndian(buffer, offset + 0x132);
                Lsn = EndianUtilities.ToUInt64BigEndian(buffer, offset + 0x142);
                Crc = EndianUtilities.ToUInt32BigEndian(buffer, offset + 0x14A);
            }
            return Size;
        }
        
        public void LoadBtree(Context context, long offset)
        {
            var data = context.RawStream;
            data.Position = offset + context.SuperBlock.Blocksize*(long)Root;
            if (Level == 1)
            {
                RootInodeBtree = new BTreeInodeLeaf(SbVersion);
            }
            else
            {
                RootInodeBtree = new BTreeInodeNode(SbVersion);
            }
            var buffer = StreamUtilities.ReadExact(data, (int) context.SuperBlock.Blocksize);
            RootInodeBtree.ReadFrom(buffer, 0);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

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

using DiscUtils.Streams;

namespace DiscUtils.Btrfs.Base.Items
{
    /// <summary>
    /// Contains the stat information for an inode
    /// </summary>
    internal class RootItem : BaseItem
    {
        public static readonly int Length = 375;

        public RootItem(Key key) : base(key) { }

        /// <summary>
        /// Several fields are initialized but only flags is interpreted at runtime.
        /// generation=1, size=3,nlink=1, nbytes=leafsize, mode=040755
        /// flags depends on kernel version.
        /// </summary>
        public InodeItem Inode { get; private set; }

        /// <summary>
        /// transid of the transaction that created this root. 
        /// </summary>
        public ulong Generation { get; private set; }

        /// <summary>
        /// For file trees, the objectid of the root directory in this tree (always 256). Otherwise, 0.
        /// </summary>
        public ulong RootDirId { get; private set; }

        /// <summary>
        /// The disk offset in bytes for the root node of this tree.
        /// </summary>
        public ulong ByteNr { get; private set; }

        /// <summary>
        /// Unused. Always 0.
        /// </summary>
        public ulong ByteLimit { get; private set; }

        /// <summary>
        /// Unused
        /// </summary>
        public ulong BytesUsed { get; private set; }

        /// <summary>
        /// The last transid of the transaction that created a snapshot of this root.
        /// </summary>
        public ulong LastSnapshot { get; private set; }

        /// <summary>
        /// flags
        /// </summary>
        public ulong Flags { get; private set; }

        /// <summary>
        /// Originally indicated a reference count. In modern usage, it is only 0 or 1.
        /// </summary>
        public uint Refs { get; private set; }

        /// <summary>
        /// Contains key of last dropped item during subvolume removal or relocation. Zeroed otherwise.
        /// </summary>
        public Key DropProgress { get; private set; }

        /// <summary>
        /// The tree level of the node described in drop_progress.
        /// </summary>
        public byte DropLevel { get; private set; }

        /// <summary>
        /// The height of the tree rooted at bytenr.
        /// </summary>
        public byte Level { get; private set; }

        public override int Size
        {
            get { return Length; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Inode = EndianUtilities.ToStruct<InodeItem>(buffer, offset);
            Generation = EndianUtilities.ToUInt64LittleEndian(buffer, offset+ 160);
            RootDirId = EndianUtilities.ToUInt64LittleEndian(buffer, offset+ 168);
            ByteNr = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 176);
            ByteLimit = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 184);
            BytesUsed = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 192);
            LastSnapshot = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 200);
            Flags = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 208);
            Refs = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 216);
            DropProgress = EndianUtilities.ToStruct<Key>(buffer, offset+220);
            DropLevel = buffer[offset+237];
            Level = buffer[offset+238];

            //The following fields depend on the subvol_uuids+subvol_times features
            //239 	__le64 	generation_v2 	If equal to generation, indicates validity of the following fields.
            //If the root is modified using an older kernel, this field and generation will become out of sync. This is normal and recoverable.
            //247 	u8[16] 	uuid 	This subvolume's UUID.
            //263 	u8[16] 	parent_uuid 	The parent's UUID (for use with send/receive).
            //279 	u8[16] 	received_uuid 	The received UUID (for used with send/receive).
            //295 	__le64 	ctransid 	The transid of the last transaction that modified this tree, with some exceptions (like the internal caches or relocation).
            //303 	__le64 	otransid 	The transid of the transaction that created this tree.
            //311 	__le64 	stransid 	The transid for the transaction that sent this subvolume. Nonzero for received subvolume.
            //319 	__le64 	rtransid 	The transid for the transaction that received this subvolume. Nonzero for received subvolume.
            //327 	struct btrfs_timespec 	ctime 	Timestamp for ctransid.
            //339 	struct btrfs_timespec 	otime 	Timestamp for otransid.
            //351 	struct btrfs_timespec 	stime 	Timestamp for stransid.
            //363 	struct btrfs_timespec 	rtime 	Timestamp for rtransid.
            //375 	__le64[8] 	reserved 	Reserved for future use. 

            return Size;
        }
    }
}

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
    /// define the location and parameters of the root of a btree
    /// </summary>
    internal class InodeItem : BaseItem
    {
        public InodeItem(Key key) : base(key) { }

        public InodeItem() : this(null) { }

        public static readonly int Length = 160;

        public ulong Generation { get; private set; }
        public ulong TransId { get; private set; }
        /// <summary>
        /// Size of the file in bytes.
        /// </summary>
        public ulong FileSize { get; private set; }

        /// <summary>
        /// Size allocated to this file, in bytes;
        /// Sum of the offset fields of all EXTENT_DATA items for this inode. For directories: 0.
        /// </summary>
        public ulong NBytes { get; private set; }

        /// <summary>
        /// Unused for normal inodes. Contains byte offset of block group when used as a free space inode.
        /// </summary>
        public ulong BlockGroup { get; private set; }

        /// <summary>
        /// Count of INODE_REF entries for the inode. When used outside of a file tree, 1.
        /// </summary>
        public uint LinkCount { get; private set; }

        /// <summary>
        /// stat.st_uid
        /// </summary>
        public uint Uid { get; private set; }

        /// <summary>
        /// stat.st_gid
        /// </summary>
        public uint Gid { get; private set; }

        /// <summary>
        /// stat.st_mode
        /// </summary>
        public uint Mode { get; private set; }

        /// <summary>
        /// stat.st_rdev
        /// </summary>
        public ulong RDev { get; private set; }

        /// <summary>
        /// Inode flags
        /// </summary>
        public InodeFlag Flags { get; private set; }

        /// <summary>
        /// Sequence number used for NFS compatibility. Initialized to 0 and incremented each time mtime value is changed.
        /// </summary>
        public ulong Sequence { get; private set; }

        /// <summary>
        /// stat.st_atime
        /// </summary>
        public TimeSpec ATime { get; private set; }

        /// <summary>
        /// stat.st_ctime
        /// </summary>
        public TimeSpec CTime { get; private set; }

        /// <summary>
        /// stat.st_mtime
        /// </summary>
        public TimeSpec MTime { get; private set; }

        /// <summary>
        /// Timestamp of inode creation
        /// </summary>
        public TimeSpec OTime { get; private set; }
        
        public override int Size
        {
            get { return Length; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            Generation = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            TransId = EndianUtilities.ToUInt64LittleEndian(buffer, offset+8);
            FileSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset+16);
            NBytes = EndianUtilities.ToUInt64LittleEndian(buffer, offset+24);
            BlockGroup = EndianUtilities.ToUInt64LittleEndian(buffer, offset+32);
            LinkCount = EndianUtilities.ToUInt32LittleEndian(buffer, offset+40);
            Uid = EndianUtilities.ToUInt32LittleEndian(buffer, offset+44);
            Gid = EndianUtilities.ToUInt32LittleEndian(buffer, offset+48);
            Mode = EndianUtilities.ToUInt32LittleEndian(buffer, offset+52);
            RDev = EndianUtilities.ToUInt64LittleEndian(buffer, offset+56);
            Flags = (InodeFlag)EndianUtilities.ToUInt64LittleEndian(buffer, offset+64);
            Sequence = EndianUtilities.ToUInt64LittleEndian(buffer, offset+72);
            ATime = EndianUtilities.ToStruct<TimeSpec>(buffer, offset+112);
            CTime = EndianUtilities.ToStruct<TimeSpec>(buffer, offset+124);
            MTime = EndianUtilities.ToStruct<TimeSpec>(buffer, offset+136);
            OTime = EndianUtilities.ToStruct<TimeSpec>(buffer, offset+148);
            return Size;
        }
    }
}

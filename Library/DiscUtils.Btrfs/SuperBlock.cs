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
using System.Text;
using DiscUtils.Internal;

namespace DiscUtils.Btrfs
{
    internal class SuperBlock : IByteArraySerializable
    {
        public static readonly int Length = 0x1000;
        public static readonly ulong BtrfsMagic = BitConverter.ToUInt64(Encoding.ASCII.GetBytes("_BHRfS_M"),0);

        /// <summary>
        /// Checksum of everything past this field (from 20 to 1000)
        /// </summary>
        public byte[] Checksum { get; private set; }

        /// <summary>
        /// FS UUID
        /// </summary>
        public Guid FsUuid { get; private set; }

        /// <summary>
        /// physical address of this block (different for mirrors)
        /// </summary>
        public ulong PhysicalAddress { get; private set; }

        /// <summary>
        /// flags       
        /// </summary>
        public ulong Flags { get; private set; }

        /// <summary>
        /// magic ("_BHRfS_M") 
        /// </summary>
        public ulong Magic { get; private set; }

        /// <summary>
        /// generation
        /// </summary>
        public ulong Generation { get; private set; }

        /// <summary>
        /// logical address of the root tree root
        /// </summary>
        public ulong Root { get; private set; }

        /// <summary>
        /// logical address of the chunk tree root
        /// </summary>
        public ulong ChunkRoot { get; private set; }

        /// <summary>
        /// logical address of the log tree root
        /// </summary>
        public ulong LogRoot { get; private set; }

        /// <summary>
        /// log_root_transid
        /// </summary>
        public ulong LogRootTransId { get; private set; }

        /// <summary>
        /// total_bytes
        /// </summary>
        public ulong TotalBytes { get; private set; }

        /// <summary>
        /// bytes_used
        /// </summary>
        public ulong BytesUsed { get; private set; }

        /// <summary>
        /// root_dir_objectid (usually 6)
        /// </summary>
        public ulong RootDirObjectid { get; private set; }

        /// <summary>
        /// num_devices
        /// </summary>
        public ulong NumDevices { get; private set; }

        /// <summary>
        /// sectorsize
        /// </summary>
        public uint SectorSize { get; private set; }

        /// <summary>
        /// nodesize
        /// </summary>
        public uint NodeSize { get; private set; }

        /// <summary>
        /// leafsize
        /// </summary>
        public uint LeafSize { get; private set; }

        /// <summary>
        /// stripesize
        /// </summary>
        public uint StripeSize { get; private set; }

        /// <summary>
        /// chunk_root_generation
        /// </summary>
        public ulong ChunkRootGeneration { get; private set; }

        /// <summary>
        /// compat_flags
        /// </summary>
        public ulong CompatFlags { get; private set; }

        /// <summary>
        /// compat_ro_flags - only implementations that support the flags can write to the filesystem
        /// </summary>
        public ulong CompatRoFlags { get; private set; }

        /// <summary>
        /// incompat_flags - only implementations that support the flags can use the filesystem
        /// </summary>
        public ulong IncompatFlags { get; private set; }

        /// <summary>
        /// csum_type - Btrfs currently uses the CRC32c little-endian hash function with seed -1.
        /// </summary>
        public ushort ChecksumType { get; private set; }

        /// <summary>
        /// root_level
        /// </summary>
        public byte RootLevel { get; private set; }

        /// <summary>
        /// chunk_root_level
        /// </summary>
        public byte ChunkRootLevel { get; private set; }

        /// <summary>
        /// log_root_level
        /// </summary>
        public byte LogRootLevel { get; private set; }

        /// <summary>
        /// label (may not contain '/' or '\\')
        /// </summary>
        public string Label { get; private set; }

        public int Size
        {
            get { return Length; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Checksum = Utilities.ToByteArray(buffer, offset, 0x20);
            FsUuid = Utilities.ToGuidLittleEndian(buffer, offset + 0x20);
            PhysicalAddress = Utilities.ToUInt64LittleEndian(buffer, offset + 0x30);
            Flags = Utilities.ToUInt64LittleEndian(buffer, offset + 0x38);
            Magic = Utilities.ToUInt64LittleEndian(buffer, offset + 0x40);
            Generation = Utilities.ToUInt64LittleEndian(buffer, offset + 0x48);
            Root = Utilities.ToUInt64LittleEndian(buffer, offset + 0x50);
            
            ChunkRoot = Utilities.ToUInt64LittleEndian(buffer, offset + 0x58);
            LogRoot = Utilities.ToUInt64LittleEndian(buffer, offset + 0x60);
            LogRootTransId = Utilities.ToUInt64LittleEndian(buffer, offset + 0x68);
            TotalBytes = Utilities.ToUInt64LittleEndian(buffer, offset + 0x70);
            BytesUsed = Utilities.ToUInt64LittleEndian(buffer, offset + 0x78);
            RootDirObjectid = Utilities.ToUInt64LittleEndian(buffer, offset + 0x80);
            NumDevices = Utilities.ToUInt64LittleEndian(buffer, offset + 0x88);
            SectorSize = Utilities.ToUInt32LittleEndian(buffer, offset + 0x90);
            NodeSize = Utilities.ToUInt32LittleEndian(buffer, offset + 0x94);
            LeafSize = Utilities.ToUInt32LittleEndian(buffer, offset + 0x98);
            StripeSize = Utilities.ToUInt32LittleEndian(buffer, offset + 0x9c);
            
            ChunkRootGeneration = Utilities.ToUInt64LittleEndian(buffer, offset + 0xa4);
            CompatFlags = Utilities.ToUInt64LittleEndian(buffer, offset + 0xac);
            CompatRoFlags = Utilities.ToUInt64LittleEndian(buffer, offset + 0xb4);
            IncompatFlags = Utilities.ToUInt64LittleEndian(buffer, offset + 0xbc);
            ChecksumType = Utilities.ToUInt16LittleEndian(buffer, offset + 0xc4);
            RootLevel = buffer[offset + 0xc6];
            ChunkRootLevel = buffer[offset + 0xc7];
            LogRootLevel = buffer[offset + 0xc8];
            //c9 	62 		DEV_ITEM data for this device
            var labelData = Utilities.ToByteArray(buffer, offset + 0x12b, 0x100);
            Label = Encoding.UTF8.GetString(labelData, 0, Array.IndexOf(labelData, (byte) 0));
            //22b 	100 		reserved
            var n = Utilities.ToUInt32LittleEndian(buffer, offset + 0xa0);
            //32b 	800 		(n bytes valid) Contains (KEY, CHUNK_ITEM) pairs for all SYSTEM chunks. This is needed to bootstrap the mapping from logical addresses to physical.
            //b2b 	4d5 		Currently unused 
            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

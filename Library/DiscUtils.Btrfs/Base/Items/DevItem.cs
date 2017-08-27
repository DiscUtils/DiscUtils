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
using DiscUtils.Streams;

namespace DiscUtils.Btrfs.Base.Items
{
    /// <summary>
    /// Maps logical address to physical
    /// </summary>
    internal class DevItem : BaseItem
    {
        public static readonly int Length = 0x62;
        public DevItem(Key key) : base(key) { }

        /// <summary>
        ///  the internal btrfs device id  
        /// </summary>
        public ulong DeviceId { get; private set; }

        /// <summary>
        ///  size of the device  
        /// </summary>
        public ulong DeviceSize { get; private set; }

        /// <summary>
        ///   number of bytes used 
        /// </summary>
        public ulong DeviceSizeUsed { get; private set; }

        /// <summary>
        /// optimal io alignment
        /// </summary>
        public uint OptimalIoAlignment { get; private set; }

        /// <summary>
        /// optimal io width
        /// </summary>
        public uint OptimalIoWidth { get; private set; }

        /// <summary>
        /// minimal io size (sector size)
        /// </summary>
        public uint MinimalIoSize { get; private set; }

        /// <summary>
        /// type and info about this device 
        /// </summary>
        public BlockGroupFlag Type { get; private set; }

        /// <summary>
        /// expected generation for this device 
        /// </summary>
        public ulong Generation { get; private set; }

        /// <summary>
        /// starting byte of this partition on the device,
        /// to allow for stripe alignment in the future
        /// </summary>
        public ulong StartOffset { get; private set; }

        /// <summary>
        /// grouping information for allocation decisions 
        /// </summary>
        public uint DevGroup { get; private set; }

        /// <summary>
        /// seek speed 0-100 where 100 is fastest 
        /// </summary>
        public byte SeekSpeed { get; private set; }

        /// <summary>
        /// bandwidth 0-100 where 100 is fastest 
        /// </summary>
        public byte Bandwidth { get; private set; }

        /// <summary>
        /// btrfs generated uuid for this device 
        /// </summary>
        public Guid DeviceUuid { get; private set; }

        /// <summary>
        /// uuid of FS who owns this device 
        /// </summary>
        public Guid FsUuid { get; private set; }

        public override int Size
        {
            get { return Length; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            DeviceId = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            DeviceSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x8);
            DeviceSizeUsed = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x8);
            OptimalIoAlignment = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x18);
            OptimalIoWidth = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x1c);
            MinimalIoSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x20);
            Type = (BlockGroupFlag)EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x24);
            Generation = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x2c);
            StartOffset = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 0x34);
            DevGroup = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x3c);
            SeekSpeed = buffer[offset + 0x40];
            Bandwidth = buffer[offset + 0x41];
            DeviceUuid = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x42);
            FsUuid = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x52);
            return Size;
        }
    }
}

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

using System;
using DiscUtils.Streams;

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Provides access to partition records in a BIOS (MBR) partition table.
    /// </summary>
    public sealed class BiosPartitionInfo : PartitionInfo
    {
        private readonly BiosPartitionRecord _record;
        private readonly BiosPartitionTable _table;

        internal BiosPartitionInfo(BiosPartitionTable table, BiosPartitionRecord record)
        {
            _table = table;
            _record = record;
        }

        /// <summary>
        /// Gets the type of the partition.
        /// </summary>
        public override byte BiosType
        {
            get { return _record.PartitionType; }
        }

        /// <summary>
        /// Gets the end (inclusive) of the partition as a CHS address.
        /// </summary>
        public ChsAddress End
        {
            get { return new ChsAddress(_record.EndCylinder, _record.EndHead, _record.EndSector); }
        }

        /// <summary>
        /// Gets the first sector of the partion (relative to start of disk) as a Logical Block Address.
        /// </summary>
        public override long FirstSector
        {
            get { return _record.LBAStartAbsolute; }
        }

        /// <summary>
        /// Always returns <see cref="System.Guid"/>.Empty.
        /// </summary>
        public override Guid GuidType
        {
            get { return Guid.Empty; }
        }

        /// <summary>
        /// Gets a value indicating whether this partition is active (bootable).
        /// </summary>
        public bool IsActive
        {
            get { return _record.Status != 0; }
        }

        /// <summary>
        /// Gets a value indicating whether the partition is a primary (rather than extended) partition.
        /// </summary>
        public bool IsPrimary
        {
            get { return PrimaryIndex >= 0; }
        }

        /// <summary>
        /// Gets the last sector of the partion (relative to start of disk) as a Logical Block Address (inclusive).
        /// </summary>
        public override long LastSector
        {
            get { return _record.LBAStartAbsolute + _record.LBALength - 1; }
        }

        /// <summary>
        /// Gets the index of the partition in the primary partition table, or <c>-1</c> if not a primary partition.
        /// </summary>
        public int PrimaryIndex
        {
            get { return _record.Index; }
        }

        /// <summary>
        /// Gets the start of the partition as a CHS address.
        /// </summary>
        public ChsAddress Start
        {
            get { return new ChsAddress(_record.StartCylinder, _record.StartHead, _record.StartSector); }
        }

        /// <summary>
        /// Gets the type of the partition as a string.
        /// </summary>
        public override string TypeAsString
        {
            get { return _record.FriendlyPartitionType; }
        }

        internal override PhysicalVolumeType VolumeType
        {
            get { return PhysicalVolumeType.BiosPartition; }
        }

        /// <summary>
        /// Opens a stream to access the content of the partition.
        /// </summary>
        /// <returns>The new stream.</returns>
        public override SparseStream Open()
        {
            return _table.Open(_record);
        }
    }
}
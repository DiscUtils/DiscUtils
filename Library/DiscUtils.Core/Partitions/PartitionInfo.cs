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
using System.Globalization;
using DiscUtils.Streams;

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Base class representing a disk partition.
    /// </summary>
    /// <remarks>The purpose of this class is to provide a minimal view of a partition,
    /// such that callers can access existing partitions without specific knowledge of
    /// the partitioning system.</remarks>
    public abstract class PartitionInfo
    {
        /// <summary>
        /// Gets the type of the partition, in legacy BIOS form, when available.
        /// </summary>
        /// <remarks>Zero for GUID-style partitions.</remarks>
        public abstract byte BiosType { get; }

        /// <summary>
        /// Gets the first sector of the partion (relative to start of disk) as a Logical Block Address.
        /// </summary>
        public abstract long FirstSector { get; }

        /// <summary>
        /// Gets the type of the partition, as a GUID, when available.
        /// </summary>
        /// <remarks><see cref="System.Guid"/>.Empty for MBR-style partitions.</remarks>
        public abstract Guid GuidType { get; }

        /// <summary>
        /// Gets the last sector of the partion (relative to start of disk) as a Logical Block Address (inclusive).
        /// </summary>
        public abstract long LastSector { get; }

        /// <summary>
        /// Gets the length of the partition in sectors.
        /// </summary>
        public virtual long SectorCount
        {
            get { return 1 + LastSector - FirstSector; }
        }

        /// <summary>
        /// Gets the partition type as a 'friendly' string.
        /// </summary>
        public abstract string TypeAsString { get; }

        /// <summary>
        /// Gets the physical volume type for this type of partition.
        /// </summary>
        internal abstract PhysicalVolumeType VolumeType { get; }

        /// <summary>
        /// Opens a stream that accesses the partition's contents.
        /// </summary>
        /// <returns>The new stream.</returns>
        public abstract SparseStream Open();

        /// <summary>
        /// Gets a summary of the partition information as 'first - last (type)'.
        /// </summary>
        /// <returns>A string representation of the partition information.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "0x{0:X} - 0x{1:X} ({2})", FirstSector, LastSector,
                TypeAsString);
        }
    }
}
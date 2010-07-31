//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.Collections.ObjectModel;

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Base class for classes which represent a disk partitioning scheme.
    /// </summary>
    /// <remarks>After modifying the table, by creating or deleting a partition assume that any
    /// previously stored partition indexes of higher value are no longer valid.  Re-enumerate
    /// the partitions to discover the next index-to-partition mapping.</remarks>
    public abstract class PartitionTable
    {
        /// <summary>
        /// Gets the GUID that uniquely identifies this disk, if supported (else returns <c>null</c>).
        /// </summary>
        public abstract Guid DiskGuid { get; }

        /// <summary>
        /// Creates a new partition that encompasses the entire disk.
        /// </summary>
        /// <param name="type">The partition type</param>
        /// <param name="active">Whether the partition is active (bootable)</param>
        /// <returns>The index of the partition</returns>
        /// <remarks>The partition table must be empty before this method is called,
        /// otherwise IOException is thrown.</remarks>
        public abstract int Create(WellKnownPartitionType type, bool active);

        /// <summary>
        /// Creates a new partition with a target size.
        /// </summary>
        /// <param name="size">The target size (in bytes)</param>
        /// <param name="type">The partition type</param>
        /// <param name="active">Whether the partition is active (bootable)</param>
        /// <returns>The index of the new partition</returns>
        public abstract int Create(long size, WellKnownPartitionType type, bool active);

        /// <summary>
        /// Deletes a partition at a given index.
        /// </summary>
        /// <param name="index">The index of the partition</param>
        public abstract void Delete(int index);

        /// <summary>
        /// Gets the list of partitions that contain user data (i.e. non-system / empty).
        /// </summary>
        public abstract ReadOnlyCollection<PartitionInfo> Partitions { get; }

        /// <summary>
        /// Gets information about a particular User partition.
        /// </summary>
        /// <param name="index">The index of the partition</param>
        /// <returns>Information about the partition</returns>
        public PartitionInfo this[int index]
        {
            get { return Partitions[index]; }
        }

        /// <summary>
        /// Gets the number of User partitions on the disk.
        /// </summary>
        public int Count
        {
            get { return Partitions.Count; }
        }
    }
}

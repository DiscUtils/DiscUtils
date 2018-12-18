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
using System.Collections.ObjectModel;
using System.IO;
using DiscUtils.Partitions;
using DiscUtils.Streams;

namespace DiscUtils.ApplePartitionMap
{
    /// <summary>
    /// Interprets Apple Partition Map structures that partition a disk.
    /// </summary>
    public sealed class PartitionMap : PartitionTable
    {
        private readonly PartitionMapEntry[] _partitions;
        private readonly Stream _stream;

        /// <summary>
        /// Initializes a new instance of the PartitionMap class.
        /// </summary>
        /// <param name="stream">Stream containing the contents of a disk.</param>
        public PartitionMap(Stream stream)
        {
            _stream = stream;

            stream.Position = 0;
            byte[] initialBytes = StreamUtilities.ReadExact(stream, 1024);

            BlockZero b0 = new BlockZero();
            b0.ReadFrom(initialBytes, 0);

            PartitionMapEntry initialPart = new PartitionMapEntry(_stream);
            initialPart.ReadFrom(initialBytes, 512);

            byte[] partTableData = StreamUtilities.ReadExact(stream, (int)(initialPart.MapEntries - 1) * 512);

            _partitions = new PartitionMapEntry[initialPart.MapEntries - 1];
            for (uint i = 0; i < initialPart.MapEntries - 1; ++i)
            {
                _partitions[i] = new PartitionMapEntry(_stream);
                _partitions[i].ReadFrom(partTableData, (int)(512 * i));
            }
        }

        /// <summary>
        /// Gets the GUID of the disk, always returns Guid.Empty.
        /// </summary>
        public override Guid DiskGuid
        {
            get { return Guid.Empty; }
        }

        /// <summary>
        /// Gets the partitions present on the disk.
        /// </summary>
        public override ReadOnlyCollection<PartitionInfo> Partitions
        {
            get { return new ReadOnlyCollection<PartitionInfo>(_partitions); }
        }

        /// <summary>
        /// Creates a new partition that encompasses the entire disk.
        /// </summary>
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <returns>The index of the partition.</returns>
        /// <remarks>The partition table must be empty before this method is called,
        /// otherwise IOException is thrown.</remarks>
        public override int Create(WellKnownPartitionType type, bool active)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new partition with a target size.
        /// </summary>
        /// <param name="size">The target size (in bytes).</param>
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <returns>The index of the new partition.</returns>
        public override int Create(long size, WellKnownPartitionType type, bool active)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new aligned partition that encompasses the entire disk.
        /// </summary>
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <param name="alignment">The alignment (in byte).</param>
        /// <returns>The index of the partition.</returns>
        /// <remarks>The partition table must be empty before this method is called,
        /// otherwise IOException is thrown.</remarks>
        /// <remarks>
        /// Traditionally partitions were aligned to the physical structure of the underlying disk,
        /// however with modern storage greater efficiency is acheived by aligning partitions on
        /// large values that are a power of two.
        /// </remarks>
        public override int CreateAligned(WellKnownPartitionType type, bool active, int alignment)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new aligned partition with a target size.
        /// </summary>
        /// <param name="size">The target size (in bytes).</param>
        /// <param name="type">The partition type.</param>
        /// <param name="active">Whether the partition is active (bootable).</param>
        /// <param name="alignment">The alignment (in byte).</param>
        /// <returns>The index of the new partition.</returns>
        /// <remarks>
        /// Traditionally partitions were aligned to the physical structure of the underlying disk,
        /// however with modern storage greater efficiency is achieved by aligning partitions on
        /// large values that are a power of two.
        /// </remarks>
        public override int CreateAligned(long size, WellKnownPartitionType type, bool active, int alignment)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a partition at a given index.
        /// </summary>
        /// <param name="index">The index of the partition.</param>
        public override void Delete(int index)
        {
            throw new NotImplementedException();
        }
    }
}
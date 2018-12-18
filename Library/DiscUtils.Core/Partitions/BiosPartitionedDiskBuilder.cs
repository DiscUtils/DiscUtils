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
using System.Collections.Generic;
using DiscUtils.Streams;

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Builds a stream with the contents of a BIOS partitioned disk.
    /// </summary>
    /// <remarks>
    /// This class assembles a disk image dynamically in memory.  The
    /// constructed stream will read data from the partition content
    /// streams only when a client of this class tries to read from
    /// that partition.
    /// </remarks>
    public class BiosPartitionedDiskBuilder : StreamBuilder
    {
        private Geometry _biosGeometry;
        private readonly SparseMemoryStream _bootSectors;
        private readonly long _capacity;

        private readonly Dictionary<int, BuilderExtent> _partitionContents;

        /// <summary>
        /// Initializes a new instance of the BiosPartitionedDiskBuilder class.
        /// </summary>
        /// <param name="capacity">The capacity of the disk (in bytes).</param>
        /// <param name="biosGeometry">The BIOS geometry of the disk.</param>
        public BiosPartitionedDiskBuilder(long capacity, Geometry biosGeometry)
        {
            _capacity = capacity;
            _biosGeometry = biosGeometry;

            _bootSectors = new SparseMemoryStream();
            _bootSectors.SetLength(capacity);
            PartitionTable = BiosPartitionTable.Initialize(_bootSectors, _biosGeometry);

            _partitionContents = new Dictionary<int, BuilderExtent>();
        }

        /// <summary>
        /// Initializes a new instance of the BiosPartitionedDiskBuilder class.
        /// </summary>
        /// <param name="capacity">The capacity of the disk (in bytes).</param>
        /// <param name="bootSectors">The boot sector(s) of the disk.</param>
        /// <param name="biosGeometry">The BIOS geometry of the disk.</param>
        [Obsolete("Use the variant that takes VirtualDisk, this method breaks for disks with extended partitions", false
         )]
        public BiosPartitionedDiskBuilder(long capacity, byte[] bootSectors, Geometry biosGeometry)
        {
            if (bootSectors == null)
            {
                throw new ArgumentNullException(nameof(bootSectors));
            }

            _capacity = capacity;
            _biosGeometry = biosGeometry;

            _bootSectors = new SparseMemoryStream();
            _bootSectors.SetLength(capacity);
            _bootSectors.Write(bootSectors, 0, bootSectors.Length);
            PartitionTable = new BiosPartitionTable(_bootSectors, biosGeometry);

            _partitionContents = new Dictionary<int, BuilderExtent>();
        }

        /// <summary>
        /// Initializes a new instance of the BiosPartitionedDiskBuilder class by
        /// cloning the partition structure of a source disk.
        /// </summary>
        /// <param name="sourceDisk">The disk to clone.</param>
        public BiosPartitionedDiskBuilder(VirtualDisk sourceDisk)
        {
            if (sourceDisk == null)
            {
                throw new ArgumentNullException(nameof(sourceDisk));
            }

            _capacity = sourceDisk.Capacity;
            _biosGeometry = sourceDisk.BiosGeometry;

            _bootSectors = new SparseMemoryStream();
            _bootSectors.SetLength(_capacity);

            foreach (StreamExtent extent in new BiosPartitionTable(sourceDisk).GetMetadataDiskExtents())
            {
                sourceDisk.Content.Position = extent.Start;
                byte[] buffer = StreamUtilities.ReadExact(sourceDisk.Content, (int)extent.Length);
                _bootSectors.Position = extent.Start;
                _bootSectors.Write(buffer, 0, buffer.Length);
            }

            PartitionTable = new BiosPartitionTable(_bootSectors, _biosGeometry);

            _partitionContents = new Dictionary<int, BuilderExtent>();
        }

        /// <summary>
        /// Gets the partition table in the disk.
        /// </summary>
        public BiosPartitionTable PartitionTable { get; }

        /// <summary>
        /// Sets a stream representing the content of a partition in the partition table.
        /// </summary>
        /// <param name="index">The index of the partition.</param>
        /// <param name="stream">The stream with the contents of the partition.</param>
        public void SetPartitionContent(int index, SparseStream stream)
        {
            _partitionContents[index] = new BuilderSparseStreamExtent(PartitionTable[index].FirstSector * Sizes.Sector,
                stream);
        }

        /// <summary>
        /// Updates the CHS fields in partition records to reflect a new BIOS geometry.
        /// </summary>
        /// <param name="geometry">The disk's new BIOS geometry.</param>
        /// <remarks>The partitions are not relocated to a cylinder boundary, just the CHS fields are updated on the
        /// assumption the LBA fields are definitive.</remarks>
        public void UpdateBiosGeometry(Geometry geometry)
        {
            PartitionTable.UpdateBiosGeometry(geometry);
            _biosGeometry = geometry;
        }

        protected override List<BuilderExtent> FixExtents(out long totalLength)
        {
            totalLength = _capacity;

            List<BuilderExtent> extents = new List<BuilderExtent>();

            foreach (StreamExtent extent in PartitionTable.GetMetadataDiskExtents())
            {
                _bootSectors.Position = extent.Start;
                byte[] buffer = StreamUtilities.ReadExact(_bootSectors, (int)extent.Length);

                extents.Add(new BuilderBufferExtent(extent.Start, buffer));
            }

            extents.AddRange(_partitionContents.Values);
            return extents;
        }
    }
}
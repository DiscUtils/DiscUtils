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
using System.Collections.Generic;
using System.IO;

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
        private long _capacity;
        private BiosPartitionTable _partitionTable;
        private Geometry _biosGeometry;
        private int _bootSectorsLength;
        private SparseMemoryStream _bootSectors;

        private Dictionary<int, BuilderExtent> _partitionContents;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="capacity">The capacity of the disk (in bytes)</param>
        /// <param name="biosGeometry">The BIOS geometry of the disk</param>
        public BiosPartitionedDiskBuilder(long capacity, Geometry biosGeometry)
        {
            _capacity = capacity;
            _biosGeometry = biosGeometry;
            _bootSectorsLength = Sizes.Sector;

            _bootSectors = new SparseMemoryStream();
            _bootSectors.SetLength(capacity);
            _partitionTable = BiosPartitionTable.Initialize(_bootSectors, biosGeometry);

            _partitionContents = new Dictionary<int, BuilderExtent>();
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="capacity">The capacity of the disk (in bytes)</param>
        /// <param name="bootSectors">The boot sector(s) of the disk.</param>
        /// <param name="biosGeometry">The BIOS geometry of the disk.</param>
        public BiosPartitionedDiskBuilder(long capacity, byte[] bootSectors, Geometry biosGeometry)
        {
            _capacity = capacity;
            _biosGeometry = biosGeometry;
            _bootSectorsLength = bootSectors.Length;

            _bootSectors = new SparseMemoryStream();
            _bootSectors.SetLength(capacity);
            _bootSectors.Write(bootSectors, 0, bootSectors.Length);
            _partitionTable = new BiosPartitionTable(_bootSectors, biosGeometry);

            _partitionContents = new Dictionary<int, BuilderExtent>();
        }

        /// <summary>
        /// Gets the partition table in the disk.
        /// </summary>
        public BiosPartitionTable PartitionTable
        {
            get { return _partitionTable; }
        }

        /// <summary>
        /// Sets a stream representing the content of a partition in the partition table.
        /// </summary>
        /// <param name="index">The index of the partition</param>
        /// <param name="stream">The stream with the contents of the partition</param>
        public void SetPartitionContent(int index, SparseStream stream)
        {
            _partitionContents[index] = new BuilderSparseStreamExtent(_partitionTable[index].FirstSector * Sizes.Sector, stream);
        }

        internal override List<BuilderExtent> FixExtents(out long totalLength)
        {
            totalLength = _capacity;

            byte[] bootSectorsData = new byte[_bootSectorsLength];
            _bootSectors.Position = 0;
            _bootSectors.Read(bootSectorsData, 0, _bootSectorsLength);

            List<BuilderExtent> extents = new List<BuilderExtent>();
            extents.Add(new BuilderBufferExtent(0, bootSectorsData));
            extents.AddRange(_partitionContents.Values);
            return extents;
        }
    }
}

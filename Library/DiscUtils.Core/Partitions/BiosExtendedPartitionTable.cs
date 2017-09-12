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

using System.Collections.Generic;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Partitions
{
    internal class BiosExtendedPartitionTable
    {
        private readonly Stream _disk;
        private readonly uint _firstSector;

        public BiosExtendedPartitionTable(Stream disk, uint firstSector)
        {
            _disk = disk;
            _firstSector = firstSector;
        }

        public BiosPartitionRecord[] GetPartitions()
        {
            List<BiosPartitionRecord> result = new List<BiosPartitionRecord>();

            uint partPos = _firstSector;
            while (partPos != 0)
            {
                _disk.Position = (long)partPos * Sizes.Sector;
                byte[] sector = StreamUtilities.ReadExact(_disk, Sizes.Sector);
                if (sector[510] != 0x55 || sector[511] != 0xAA)
                {
                    throw new IOException("Invalid extended partition sector");
                }

                uint nextPartPos = 0;
                for (int offset = 0x1BE; offset <= 0x1EE; offset += 0x10)
                {
                    BiosPartitionRecord thisPart = new BiosPartitionRecord(sector, offset, partPos, -1);

                    if (thisPart.StartCylinder != 0 || thisPart.StartHead != 0 || thisPart.StartSector != 0 || 
                        (thisPart.LBAStart != 0 && thisPart.LBALength != 0))
                    {
                        if (thisPart.PartitionType != 0x05 && thisPart.PartitionType != 0x0F)
                        {
                            result.Add(thisPart);
                        }
                        else
                        {
                            nextPartPos = _firstSector + thisPart.LBAStart;
                        }
                    }
                }

                partPos = nextPartPos;
            }

            return result.ToArray();
        }

        /// <summary>
        /// Gets all of the disk ranges containing partition table data.
        /// </summary>
        /// <returns>Set of stream extents, indicated as byte offset from the start of the disk.</returns>
        public IEnumerable<StreamExtent> GetMetadataDiskExtents()
        {
            List<StreamExtent> extents = new List<StreamExtent>();

            uint partPos = _firstSector;
            while (partPos != 0)
            {
                extents.Add(new StreamExtent((long)partPos * Sizes.Sector, Sizes.Sector));

                _disk.Position = (long)partPos * Sizes.Sector;
                byte[] sector = StreamUtilities.ReadExact(_disk, Sizes.Sector);
                if (sector[510] != 0x55 || sector[511] != 0xAA)
                {
                    throw new IOException("Invalid extended partition sector");
                }

                uint nextPartPos = 0;
                for (int offset = 0x1BE; offset <= 0x1EE; offset += 0x10)
                {
                    BiosPartitionRecord thisPart = new BiosPartitionRecord(sector, offset, partPos, -1);

                    if (thisPart.StartCylinder != 0 || thisPart.StartHead != 0 || thisPart.StartSector != 0)
                    {
                        if (thisPart.PartitionType == 0x05 || thisPart.PartitionType == 0x0F)
                        {
                            nextPartPos = _firstSector + thisPart.LBAStart;
                        }
                    }
                }

                partPos = nextPartPos;
            }

            return extents;
        }
    }
}
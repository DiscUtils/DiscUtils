//
// Copyright (c) 2008-2009, Kenneth Bell
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

namespace DiscUtils.Partitions
{
    internal class BiosExtendedPartitionTable
    {
        private Stream _disk;
        private Geometry _geometry;
        private uint _firstSector;

        public BiosExtendedPartitionTable(Stream disk, Geometry geometry, uint firstSector)
        {
            _disk = disk;
            _geometry = geometry;
            _firstSector = firstSector;
        }

        public BiosPartitionRecord[] GetPartitions()
        {
            List<BiosPartitionRecord> result = new List<BiosPartitionRecord>();

            uint partPos = _firstSector;
            while (partPos != 0)
            {
                _disk.Position = (long)partPos * Utilities.SectorSize;
                byte[] sector = Utilities.ReadFully(_disk, Utilities.SectorSize);

                BiosPartitionRecord thisPart = new BiosPartitionRecord(sector, 0x01BE, partPos);
                BiosPartitionRecord nextPart = new BiosPartitionRecord(sector, 0x01CE, partPos);

                result.Add(thisPart);
                if (nextPart.StartCylinder != 0 || nextPart.StartHead != 0 || nextPart.StartSector != 0)
                {
                    partPos += nextPart.LBAStart;
                }
                else
                {
                    partPos = 0;
                }
            }

            return result.ToArray();
        }
    }
}

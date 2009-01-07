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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Represents a GUID Partition Table.
    /// </summary>
    public class GuidPartitionTable : PartitionTable
    {
        private Stream _diskData;
        private Geometry _diskGeometry;
        private GptHeader _header;

        /// <summary>
        /// Creates a new instance to access an existing partition table on a disk.
        /// </summary>
        /// <param name="disk">The disk containing the partition table</param>
        public GuidPartitionTable(VirtualDisk disk)
        {
            Init(disk.Content, disk.Geometry);
        }

        /// <summary>
        /// Creates a new instance to access an existing partition table.
        /// </summary>
        /// <param name="disk">The stream containing the disk data</param>
        /// <param name="diskGeometry">The geometry of the disk</param>
        public GuidPartitionTable(Stream disk, Geometry diskGeometry)
        {
            Init(disk, diskGeometry);
        }

        /// <summary>
        /// Creates a new partition that encompasses the entire disk.
        /// </summary>
        /// <param name="type">The partition type</param>
        /// <param name="active">Whether the partition is active (bootable)</param>
        /// <returns>The index of the partition</returns>
        /// <remarks>The partition table must be empty before this method is called,
        /// otherwise IOException is thrown.</remarks>
        public override int Create(WellKnownPartitionType type, bool active)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new primary partition with a target size.
        /// </summary>
        /// <param name="size">The target size (in bytes)</param>
        /// <param name="type">The partition type</param>
        /// <param name="active">Whether the partition is active (bootable)</param>
        /// <returns>The index of the new partition</returns>
        public override int Create(long size, WellKnownPartitionType type, bool active)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a partition at a given index.
        /// </summary>
        /// <param name="index">The index of the partition</param>
        public override void Delete(int index)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a collection of the partitions for storing Operating System file-systems.
        /// </summary>
        public override ReadOnlyCollection<PartitionInfo> Partitions
        {
            get
            {
                return new ReadOnlyCollection<PartitionInfo>(Utilities.Map<GptEntry, GuidPartitionInfo>(GetAllEntries(), (e) => new GuidPartitionInfo(this, e)));
            }
        }


        internal Stream Open(GptEntry entry)
        {
            return new SubStream(_diskData, entry.FirstUsedLogicalBlock * _diskGeometry.BytesPerSector, entry.LastUsedLogicalBlock * _diskGeometry.BytesPerSector);
        }


        private void Init(Stream disk, Geometry diskGeometry)
        {
            BiosPartitionTable bpt;
            try
            {
                bpt = new BiosPartitionTable(disk, diskGeometry);
            }
            catch(IOException ioe)
            {
                throw new IOException("Invalid GPT disk, protective MBR table not present or invalid", ioe);
            }

            if (bpt.Count != 1 || bpt[0].BiosType != BiosPartitionTypes.GptProtective)
            {
                throw new IOException("Invalid GPT disk, protective MBR table is not valid");
            }

            _diskData = disk;
            _diskGeometry = diskGeometry;

            byte[] sector = new byte[diskGeometry.BytesPerSector];
            disk.Position = 512;
            disk.Read(sector, 0, sector.Length);

            _header = new GptHeader();
            if (!_header.ReadFrom(sector, 0, 512))
            {
                disk.Position = disk.Length - diskGeometry.BytesPerSector;
                disk.Read(sector, 0, sector.Length);
                _header.ReadFrom(sector, 0, sector.Length);
            }
        }


        private IEnumerable<GptEntry> GetAllEntries()
        {
            _diskData.Position = _header.PartitionEntriesLba * _diskGeometry.BytesPerSector;
            byte[] buffer = Utilities.ReadFully(_diskData, (int)(_header.PartitionEntrySize * _header.PartitionEntryCount));

            for (int i = 0; i < _header.PartitionEntryCount; ++i)
            {
                GptEntry entry = new GptEntry();
                entry.ReadFrom(buffer, i * _header.PartitionEntrySize, _header.PartitionEntrySize);
                if (entry.PartitionType != Guid.Empty)
                {
                    yield return entry;
                }
            }
        }
    }
}

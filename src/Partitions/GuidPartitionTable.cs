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
using System.Globalization;

namespace DiscUtils.Partitions
{
    /// <summary>
    /// Represents a GUID Partition Table.
    /// </summary>
    public class GuidPartitionTable : PartitionTable
    {
        private Stream _diskData;
        private Geometry _diskGeometry;
        private GptHeader _primaryHeader;
        private GptHeader _secondaryHeader;
        private byte[] _entryBuffer;

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
            int offset = GetPartitionOffset(index);
            Array.Clear(_entryBuffer, offset, _primaryHeader.PartitionEntrySize);
            Write();
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

            disk.Position = diskGeometry.BytesPerSector;
            byte[] sector = Utilities.ReadFully(disk, diskGeometry.BytesPerSector);

            _primaryHeader = new GptHeader(diskGeometry.BytesPerSector);
            if (!_primaryHeader.ReadFrom(sector, 0, 512) || !ReadEntries(_primaryHeader))
            {
                disk.Position = disk.Length - diskGeometry.BytesPerSector;
                disk.Read(sector, 0, sector.Length);
                _secondaryHeader = new GptHeader(diskGeometry.BytesPerSector);
                if (!_secondaryHeader.ReadFrom(sector, 0, sector.Length) || !ReadEntries(_secondaryHeader))
                {
                    throw new IOException("No valid GUID Partition Table found");
                }

                // Generate from the primary table from the secondary one
                _primaryHeader = new GptHeader(_secondaryHeader);
                _primaryHeader.HeaderLba = _secondaryHeader.AlternateHeaderLba;
                _primaryHeader.AlternateHeaderLba = _secondaryHeader.HeaderLba;
                _primaryHeader.PartitionEntriesLba = 2;

                // If the disk is writeable, fix up the primary partition table based on the
                // (valid) secondary table.
                if (disk.CanWrite)
                {
                    WritePrimaryHeader();
                }
            }

            if (_secondaryHeader == null)
            {
                _secondaryHeader = new GptHeader(diskGeometry.BytesPerSector);
                disk.Position = disk.Length - diskGeometry.BytesPerSector;
                disk.Read(sector, 0, sector.Length);
                if (!_secondaryHeader.ReadFrom(sector, 0, sector.Length) || !ReadEntries(_secondaryHeader))
                {
                    // Generate from the secondary table from the primary one
                    _secondaryHeader = new GptHeader(_primaryHeader);
                    _secondaryHeader.HeaderLba = _secondaryHeader.AlternateHeaderLba;
                    _secondaryHeader.AlternateHeaderLba = _secondaryHeader.HeaderLba;
                    _secondaryHeader.PartitionEntriesLba = _secondaryHeader.HeaderLba - (((_secondaryHeader.PartitionEntryCount * _secondaryHeader.PartitionEntrySize) + diskGeometry.BytesPerSector - 1)) / diskGeometry.BytesPerSector;

                    // If the disk is writeable, fix up the secondary partition table based on the
                    // (valid) primary table.
                    if (disk.CanWrite)
                    {
                        WriteSecondaryHeader();
                    }
                }
            }

        }

        private void Write()
        {
            WritePrimaryHeader();
            WriteSecondaryHeader();
        }

        private void WritePrimaryHeader()
        {
            byte[] buffer = new byte[_diskGeometry.BytesPerSector];
            _primaryHeader.EntriesCrc = CalcEntriesCrc();
            _primaryHeader.WriteTo(buffer, 0);
            _diskData.Position = _diskGeometry.BytesPerSector;
            _diskData.Write(buffer, 0, buffer.Length);

            _diskData.Position = 2 * _diskGeometry.BytesPerSector;
            _diskData.Write(_entryBuffer, 0, _entryBuffer.Length);
        }

        private void WriteSecondaryHeader()
        {
            byte[] buffer = new byte[_diskGeometry.BytesPerSector];
            _secondaryHeader.EntriesCrc = CalcEntriesCrc();
            _secondaryHeader.WriteTo(buffer, 0);
            _diskData.Position = _diskData.Length - _diskGeometry.BytesPerSector;
            _diskData.Write(buffer, 0, buffer.Length);

            _diskData.Position = _secondaryHeader.PartitionEntriesLba * _diskGeometry.BytesPerSector;
            _diskData.Write(_entryBuffer, 0, _entryBuffer.Length);
        }

        private bool ReadEntries(GptHeader header)
        {
            _diskData.Position = header.PartitionEntriesLba * _diskGeometry.BytesPerSector;
            _entryBuffer = Utilities.ReadFully(_diskData, (int)(header.PartitionEntrySize * header.PartitionEntryCount));
            if (header.EntriesCrc != CalcEntriesCrc())
            {
                return false;
            }
            return true;
        }

        private uint CalcEntriesCrc()
        {
            uint calcSig = Crc32.Compute(0xFFFFFFFF, _entryBuffer, 0, _entryBuffer.Length) ^ 0xFFFFFFFF;
            return calcSig;
        }


        private IEnumerable<GptEntry> GetAllEntries()
        {
            for (int i = 0; i < _primaryHeader.PartitionEntryCount; ++i)
            {
                GptEntry entry = new GptEntry();
                entry.ReadFrom(_entryBuffer, i * _primaryHeader.PartitionEntrySize, _primaryHeader.PartitionEntrySize);
                if (entry.PartitionType != Guid.Empty)
                {
                    yield return entry;
                }
            }
        }

        private int GetPartitionOffset(int index)
        {
            bool found = false;
            int entriesSoFar = 0;
            int position = 0;

            while (!found && position < _primaryHeader.PartitionEntryCount)
            {
                GptEntry entry = new GptEntry();
                entry.ReadFrom(_entryBuffer, position * _primaryHeader.PartitionEntrySize, _primaryHeader.PartitionEntrySize);
                if (entry.PartitionType != Guid.Empty)
                {
                    if (index == entriesSoFar)
                    {
                        found = true;
                        break;
                    }
                    entriesSoFar++;
                }
                position++;
            }

            if (found)
            {
                return position * _primaryHeader.PartitionEntrySize;
            }
            else
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture, "No such partition: {0}", index));
            }
        }


    }
}

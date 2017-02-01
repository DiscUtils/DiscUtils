//
// Copyright (c) 2014, Quamotion
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
using DiscUtils.Partitions;

namespace DiscUtils.Dmg
{
    internal class UdifPartitionTable : PartitionTable
    {
        private readonly UdifBuffer _buffer;
        private readonly Disk _disk;
        private readonly Collection<PartitionInfo> _partitions;

        public UdifPartitionTable(Disk disk, UdifBuffer buffer)
        {
            _buffer = buffer;
            _partitions = new Collection<PartitionInfo>();
            _disk = disk;

            foreach (CompressedBlock block in _buffer.Blocks)
            {
                UdifPartitionInfo partition = new UdifPartitionInfo(_disk, block);
                _partitions.Add(partition);
            }
        }

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

        public override void Delete(int index)
        {
            throw new NotImplementedException();
        }

        public override int CreateAligned(long size, WellKnownPartitionType type, bool active, int alignment)
        {
            throw new NotImplementedException();
        }

        public override int Create(long size, WellKnownPartitionType type, bool active)
        {
            throw new NotImplementedException();
        }

        public override int CreateAligned(WellKnownPartitionType type, bool active, int alignment)
        {
            throw new NotImplementedException();
        }

        public override int Create(WellKnownPartitionType type, bool active)
        {
            throw new NotImplementedException();
        }
    }
}
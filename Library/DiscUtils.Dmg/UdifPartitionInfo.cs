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
using DiscUtils.Streams;
using DiscUtils.Partitions;

namespace DiscUtils.Dmg
{
    internal class UdifPartitionInfo : PartitionInfo
    {
        private readonly CompressedBlock _block;
        private readonly Disk _disk;

        public UdifPartitionInfo(Disk disk, CompressedBlock block)
        {
            _block = block;
            _disk = disk;
        }

        public override byte BiosType
        {
            get { return 0; }
        }

        public override long FirstSector
        {
            get { return _block.FirstSector; }
        }

        public override Guid GuidType
        {
            get { return Guid.Empty; }
        }

        public override long LastSector
        {
            get { return _block.FirstSector + _block.SectorCount; }
        }

        public override long SectorCount
        {
            get { return _block.SectorCount; }
        }

        public override string TypeAsString
        {
            get { return GetType().FullName; }
        }

        internal override PhysicalVolumeType VolumeType
        {
            get { return PhysicalVolumeType.ApplePartition; }
        }

        public override SparseStream Open()
        {
            return new SubStream(_disk.Content, FirstSector * _disk.SectorSize, SectorCount * _disk.SectorSize);
        }
    }
}
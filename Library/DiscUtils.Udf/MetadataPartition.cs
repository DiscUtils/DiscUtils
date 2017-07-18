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
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal class MetadataPartition : LogicalPartition
    {
        private readonly File _metadataFile;
        private MetadataPartitionMap _partitionMap;

        public MetadataPartition(UdfContext context, LogicalVolumeDescriptor volumeDescriptor,
                                 MetadataPartitionMap partitionMap)
            : base(context, volumeDescriptor)
        {
            _partitionMap = partitionMap;

            PhysicalPartition physical = context.PhysicalPartitions[partitionMap.PartitionNumber];
            long fileEntryPos = partitionMap.MetadataFileLocation * (long)volumeDescriptor.LogicalBlockSize;

            byte[] entryData = StreamUtilities.ReadExact(physical.Content, fileEntryPos, _context.PhysicalSectorSize);
            if (!DescriptorTag.IsValid(entryData, 0))
            {
                throw new IOException("Invalid descriptor tag looking for Metadata file entry");
            }

            DescriptorTag dt = EndianUtilities.ToStruct<DescriptorTag>(entryData, 0);
            if (dt.TagIdentifier == TagIdentifier.ExtendedFileEntry)
            {
                ExtendedFileEntry efe = EndianUtilities.ToStruct<ExtendedFileEntry>(entryData, 0);
                _metadataFile = new File(context, physical, efe, _volumeDescriptor.LogicalBlockSize);
            }
            else
            {
                throw new NotImplementedException("Only EFE implemented for Metadata file entry");
            }
        }

        public override IBuffer Content
        {
            get { return _metadataFile.FileContent; }
        }
    }
}
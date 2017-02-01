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

namespace DiscUtils.Udf
{
    internal abstract class LogicalPartition : Partition
    {
        protected UdfContext _context;
        protected LogicalVolumeDescriptor _volumeDescriptor;

        protected LogicalPartition(UdfContext context, LogicalVolumeDescriptor volumeDescriptor)
        {
            _context = context;
            _volumeDescriptor = volumeDescriptor;
        }

        public long LogicalBlockSize
        {
            get { return _volumeDescriptor.LogicalBlockSize; }
        }

        public static LogicalPartition FromDescriptor(UdfContext context, LogicalVolumeDescriptor volumeDescriptor,
                                                      int index)
        {
            PartitionMap map = volumeDescriptor.PartitionMaps[index];

            Type1PartitionMap asType1 = map as Type1PartitionMap;
            if (asType1 != null)
            {
                return new Type1Partition(context, volumeDescriptor, asType1);
            }

            MetadataPartitionMap asMetadata = map as MetadataPartitionMap;
            if (asMetadata != null)
            {
                return new MetadataPartition(context, volumeDescriptor, asMetadata);
            }

            throw new NotImplementedException("Unrecognized partition map type");
        }
    }
}
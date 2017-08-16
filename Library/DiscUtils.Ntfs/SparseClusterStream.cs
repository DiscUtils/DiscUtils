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
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class SparseClusterStream : ClusterStream
    {
        private readonly NtfsAttribute _attr;
        private readonly RawClusterStream _rawStream;

        public SparseClusterStream(NtfsAttribute attr, RawClusterStream rawStream)
        {
            _attr = attr;
            _rawStream = rawStream;
        }

        public override long AllocatedClusterCount
        {
            get { return _rawStream.AllocatedClusterCount; }
        }

        public override IEnumerable<Range<long, long>> StoredClusters
        {
            get { return _rawStream.StoredClusters; }
        }

        public override bool IsClusterStored(long vcn)
        {
            return _rawStream.IsClusterStored(vcn);
        }

        public override void ExpandToClusters(long numVirtualClusters, NonResidentAttributeRecord extent, bool allocate)
        {
            _rawStream.ExpandToClusters(CompressionStart(numVirtualClusters), extent, false);
        }

        public override void TruncateToClusters(long numVirtualClusters)
        {
            long alignedNum = CompressionStart(numVirtualClusters);
            _rawStream.TruncateToClusters(alignedNum);
            if (alignedNum != numVirtualClusters)
            {
                _rawStream.ReleaseClusters(numVirtualClusters, (int)(alignedNum - numVirtualClusters));
            }
        }

        public override void ReadClusters(long startVcn, int count, byte[] buffer, int offset)
        {
            _rawStream.ReadClusters(startVcn, count, buffer, offset);
        }

        public override int WriteClusters(long startVcn, int count, byte[] buffer, int offset)
        {
            int clustersAllocated = 0;
            clustersAllocated += _rawStream.AllocateClusters(startVcn, count);
            clustersAllocated += _rawStream.WriteClusters(startVcn, count, buffer, offset);
            return clustersAllocated;
        }

        public override int ClearClusters(long startVcn, int count)
        {
            return _rawStream.ReleaseClusters(startVcn, count);
        }

        private long CompressionStart(long vcn)
        {
            return MathUtilities.RoundUp(vcn, _attr.CompressionUnitSize);
        }
    }
}
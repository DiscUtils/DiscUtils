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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Low-level non-resident attribute operations.
    /// </summary>
    /// <remarks>
    /// Responsible for:
    /// * Cluster Allocation / Release
    /// * Reading clusters from disk
    /// * Writing clusters to disk
    /// * Substituting zeros for 'sparse'/'unallocated' clusters
    /// Not responsible for:
    /// * Compression / Decompression
    /// * Extending attributes.
    /// </remarks>
    internal sealed class RawClusterStream : ClusterStream
    {
        private readonly int _bytesPerCluster;
        private readonly INtfsContext _context;
        private readonly CookedDataRuns _cookedRuns;
        private readonly Stream _fsStream;
        private readonly bool _isMft;

        public RawClusterStream(INtfsContext context, CookedDataRuns cookedRuns, bool isMft)
        {
            _context = context;
            _cookedRuns = cookedRuns;
            _isMft = isMft;

            _fsStream = _context.RawStream;
            _bytesPerCluster = context.BiosParameterBlock.BytesPerCluster;
        }

        public override long AllocatedClusterCount
        {
            get
            {
                long total = 0;
                for (int i = 0; i < _cookedRuns.Count; ++i)
                {
                    CookedDataRun run = _cookedRuns[i];
                    total += run.IsSparse ? 0 : run.Length;
                }

                return total;
            }
        }

        public override IEnumerable<Range<long, long>> StoredClusters
        {
            get
            {
                Range<long, long> lastVcnRange = null;
                List<Range<long, long>> ranges = new List<Range<long, long>>();

                int runCount = _cookedRuns.Count;
                for (int i = 0; i < runCount; i++)
                {
                    CookedDataRun cookedRun = _cookedRuns[i];
                    if (!cookedRun.IsSparse)
                    {
                        long startPos = cookedRun.StartVcn;
                        if (lastVcnRange != null && lastVcnRange.Offset + lastVcnRange.Count == startPos)
                        {
                            lastVcnRange = new Range<long, long>(lastVcnRange.Offset,
                                lastVcnRange.Count + cookedRun.Length);
                            ranges[ranges.Count - 1] = lastVcnRange;
                        }
                        else
                        {
                            lastVcnRange = new Range<long, long>(cookedRun.StartVcn, cookedRun.Length);
                            ranges.Add(lastVcnRange);
                        }
                    }
                }

                return ranges;
            }
        }

        public override bool IsClusterStored(long vcn)
        {
            int runIdx = _cookedRuns.FindDataRun(vcn, 0);
            return !_cookedRuns[runIdx].IsSparse;
        }

        public bool AreAllClustersStored(long vcn, int count)
        {
            int runIdx = 0;
            long focusVcn = vcn;
            while (focusVcn < vcn + count)
            {
                runIdx = _cookedRuns.FindDataRun(focusVcn, runIdx);

                CookedDataRun run = _cookedRuns[runIdx];
                if (run.IsSparse)
                {
                    return false;
                }

                focusVcn = run.StartVcn + run.Length;
            }

            return true;
        }

        public override void ExpandToClusters(long numVirtualClusters, NonResidentAttributeRecord extent, bool allocate)
        {
            long totalVirtualClusters = _cookedRuns.NextVirtualCluster;
            if (totalVirtualClusters < numVirtualClusters)
            {
                NonResidentAttributeRecord realExtent = extent;
                if (realExtent == null)
                {
                    realExtent = _cookedRuns.Last.AttributeExtent;
                }

                DataRun newRun = new DataRun(0, numVirtualClusters - totalVirtualClusters, true);
                realExtent.DataRuns.Add(newRun);
                _cookedRuns.Append(newRun, extent);
                realExtent.LastVcn = numVirtualClusters - 1;
            }

            if (allocate)
            {
                AllocateClusters(totalVirtualClusters, (int)(numVirtualClusters - totalVirtualClusters));
            }
        }

        public override void TruncateToClusters(long numVirtualClusters)
        {
            if (numVirtualClusters < _cookedRuns.NextVirtualCluster)
            {
                ReleaseClusters(numVirtualClusters, (int)(_cookedRuns.NextVirtualCluster - numVirtualClusters));

                int runIdx = _cookedRuns.FindDataRun(numVirtualClusters, 0);

                if (numVirtualClusters != _cookedRuns[runIdx].StartVcn)
                {
                    _cookedRuns.SplitRun(runIdx, numVirtualClusters);
                    runIdx++;
                }

                _cookedRuns.TruncateAt(runIdx);
            }
        }

        public int AllocateClusters(long startVcn, int count)
        {
            if (startVcn + count > _cookedRuns.NextVirtualCluster)
            {
                throw new IOException("Attempt to allocate unknown clusters");
            }

            int totalAllocated = 0;
            int runIdx = 0;

            long focus = startVcn;
            while (focus < startVcn + count)
            {
                runIdx = _cookedRuns.FindDataRun(focus, runIdx);
                CookedDataRun run = _cookedRuns[runIdx];

                if (run.IsSparse)
                {
                    if (focus != run.StartVcn)
                    {
                        _cookedRuns.SplitRun(runIdx, focus);
                        runIdx++;
                        run = _cookedRuns[runIdx];
                    }

                    long numClusters = Math.Min(startVcn + count - focus, run.Length);
                    if (numClusters != run.Length)
                    {
                        _cookedRuns.SplitRun(runIdx, focus + numClusters);
                        run = _cookedRuns[runIdx];
                    }

                    long nextCluster = -1;
                    for (int i = runIdx - 1; i >= 0; --i)
                    {
                        if (!_cookedRuns[i].IsSparse)
                        {
                            nextCluster = _cookedRuns[i].StartLcn + _cookedRuns[i].Length;
                            break;
                        }
                    }

                    Tuple<long, long>[] alloced = _context.ClusterBitmap.AllocateClusters(numClusters, nextCluster, _isMft,
                                                              AllocatedClusterCount);

                    List<DataRun> runs = new List<DataRun>();

                    long lcn = runIdx == 0 ? 0 : _cookedRuns[runIdx - 1].StartLcn;
                    foreach (Tuple<long, long> allocation in alloced)
                    {
#if NET20
                        runs.Add(new DataRun(allocation.Item1 - lcn, allocation.Item2, false));
                        lcn = allocation.Item1;
#else
                        runs.Add(new DataRun(allocation.Item1 - lcn, allocation.Item2, false));
                        lcn = allocation.Item1;
#endif
                    }

                    _cookedRuns.MakeNonSparse(runIdx, runs);

                    totalAllocated += (int)numClusters;
                    focus += numClusters;
                }
                else
                {
                    focus = run.StartVcn + run.Length;
                }
            }

            return totalAllocated;
        }

        public int ReleaseClusters(long startVcn, int count)
        {
            int runIdx = 0;

            int totalReleased = 0;

            long focus = startVcn;
            while (focus < startVcn + count)
            {
                runIdx = _cookedRuns.FindDataRun(focus, runIdx);
                CookedDataRun run = _cookedRuns[runIdx];

                if (run.IsSparse)
                {
                    focus += run.Length;
                }
                else
                {
                    if (focus != run.StartVcn)
                    {
                        _cookedRuns.SplitRun(runIdx, focus);
                        runIdx++;
                        run = _cookedRuns[runIdx];
                    }

                    long numClusters = Math.Min(startVcn + count - focus, run.Length);
                    if (numClusters != run.Length)
                    {
                        _cookedRuns.SplitRun(runIdx, focus + numClusters);
                        run = _cookedRuns[runIdx];
                    }

                    _context.ClusterBitmap.FreeClusters(new Range<long, long>(run.StartLcn, run.Length));
                    _cookedRuns.MakeSparse(runIdx);
                    totalReleased += (int)run.Length;

                    focus += numClusters;
                }
            }

            return totalReleased;
        }

        public override void ReadClusters(long startVcn, int count, byte[] buffer, int offset)
        {
            StreamUtilities.AssertBufferParameters(buffer, offset, count * _bytesPerCluster);

            int runIdx = 0;
            int totalRead = 0;
            while (totalRead < count)
            {
                long focusVcn = startVcn + totalRead;

                runIdx = _cookedRuns.FindDataRun(focusVcn, runIdx);
                CookedDataRun run = _cookedRuns[runIdx];

                int toRead = (int)Math.Min(count - totalRead, run.Length - (focusVcn - run.StartVcn));

                if (run.IsSparse)
                {
                    Array.Clear(buffer, offset + totalRead * _bytesPerCluster, toRead * _bytesPerCluster);
                }
                else
                {
                    long lcn = _cookedRuns[runIdx].StartLcn + (focusVcn - run.StartVcn);
                    _fsStream.Position = lcn * _bytesPerCluster;
                    StreamUtilities.ReadExact(_fsStream, buffer, offset + totalRead * _bytesPerCluster, toRead * _bytesPerCluster);
                }

                totalRead += toRead;
            }
        }

        public override int WriteClusters(long startVcn, int count, byte[] buffer, int offset)
        {
            StreamUtilities.AssertBufferParameters(buffer, offset, count * _bytesPerCluster);

            int runIdx = 0;
            int totalWritten = 0;
            while (totalWritten < count)
            {
                long focusVcn = startVcn + totalWritten;

                runIdx = _cookedRuns.FindDataRun(focusVcn, runIdx);
                CookedDataRun run = _cookedRuns[runIdx];

                if (run.IsSparse)
                {
                    throw new NotImplementedException("Writing to sparse datarun");
                }

                int toWrite = (int)Math.Min(count - totalWritten, run.Length - (focusVcn - run.StartVcn));

                long lcn = _cookedRuns[runIdx].StartLcn + (focusVcn - run.StartVcn);
                _fsStream.Position = lcn * _bytesPerCluster;
                _fsStream.Write(buffer, offset + totalWritten * _bytesPerCluster, toWrite * _bytesPerCluster);

                totalWritten += toWrite;
            }

            return 0;
        }

        public override int ClearClusters(long startVcn, int count)
        {
            byte[] zeroBuffer = new byte[16 * _bytesPerCluster];

            int clustersAllocated = 0;

            int numWritten = 0;
            while (numWritten < count)
            {
                int toWrite = Math.Min(count - numWritten, 16);

                clustersAllocated += WriteClusters(startVcn + numWritten, toWrite, zeroBuffer, 0);

                numWritten += toWrite;
            }

            return -clustersAllocated;
        }
    }
}
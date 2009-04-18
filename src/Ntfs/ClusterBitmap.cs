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
using System.IO;

namespace DiscUtils.Ntfs
{
    internal class ClusterBitmap
    {
        private File _file;
        private Bitmap _bitmap;
        private static Random s_rng = new Random();

        public ClusterBitmap(File file)
        {
            _file = file;
            NtfsAttribute attr = _file.GetAttribute(AttributeType.Data);
            _bitmap = new Bitmap(
                attr.OpenRaw(FileAccess.ReadWrite),
                Utilities.Ceil(file.Context.BiosParameterBlock.TotalSectors64, file.Context.BiosParameterBlock.SectorsPerCluster));
        }

        public Tuple<long, long>[] AllocateClusters(long count, long proposedStart)
        {
            List<Tuple<long, long>> result = new List<Tuple<long, long>>();

            long numFound = 0;

            long numClusters = _file.Context.RawStream.Length / _file.Context.BiosParameterBlock.BytesPerCluster;

            numFound += FindClusters(count - numFound, result, numClusters / 8, numClusters, proposedStart);

            if (numFound < count)
            {
                numFound = FindClusters(count - numFound, result, numClusters / 16, numClusters / 8, proposedStart);
            }
            if (numFound < count)
            {
                numFound = FindClusters(count - numFound, result, numClusters / 32, numClusters / 16, proposedStart);
            }
            if (numFound < count)
            {
                numFound = FindClusters(count - numFound, result, 0, numClusters / 32, proposedStart);
            }

            if (numFound < count)
            {
                FreeClusters(result.ToArray());
                throw new IOException("Out of disk space");
            }

            return result.ToArray();
        }

        internal void FreeClusters(params Tuple<long, long>[] runs)
        {
            foreach (var run in runs)
            {
                _bitmap.MarkAbsentRange(run.First, run.Second);
            }
        }

        /// <summary>
        /// Finds one or more free clusters in a range.
        /// </summary>
        /// <param name="count">The number of clusters required.</param>
        /// <param name="result">The list of clusters found (i.e. out param)</param>
        /// <param name="start">The first cluster in the range to look at</param>
        /// <param name="end">The last cluster in the range to look at (exclusive)</param>
        /// <param name="proposedStart">The proposed first cluster</param>
        /// <returns>The number of clusters found in the range</returns>
        private long FindClusters(long count, List<Tuple<long, long>> result, long start, long end, long proposedStart)
        {
            long numFound = 0;

            long focusCluster;
            if (proposedStart < 0)
            {
                Random rng = GetRandom();

                focusCluster = start + (long)(rng.NextDouble() * (end - start));
            }
            else
            {
                focusCluster = proposedStart;
            }

            long numInspected = 0;
            while (numFound < count && focusCluster >= start && numInspected != end - start)
            {
                if (!_bitmap.IsPresent(focusCluster))
                {
                    // Start of a run...
                    long runStart = focusCluster;
                    _bitmap.MarkPresent(focusCluster);
                    ++focusCluster;

                    while (!_bitmap.IsPresent(focusCluster) && focusCluster - runStart < count)
                    {
                        _bitmap.MarkPresent(focusCluster);
                        ++focusCluster;
                        ++numInspected;
                    }

                    result.Add(new Tuple<long, long>(runStart, focusCluster - runStart));
                    numFound += (focusCluster - runStart);
                }

                ++focusCluster;
                ++numInspected;
                if (focusCluster >= end)
                {
                    focusCluster = start;
                }
            }

            return numFound;
        }

        private Random GetRandom()
        {
            Random rng = _file.Context.Options.RandomNumberGenerator;
            if (rng == null)
            {
                rng = s_rng;
            }
            return rng;
        }

    }
}

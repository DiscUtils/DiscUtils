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

namespace DiscUtils.Ntfs
{
    using System.Collections.Generic;
    using System.IO;

    internal class CookedDataRuns
    {
        private List<CookedDataRun> _runs;

        public CookedDataRuns()
        {
            _runs = new List<CookedDataRun>();
        }

        public CookedDataRuns(IEnumerable<DataRun> rawRuns)
        {
            _runs = new List<CookedDataRun>();
            Append(rawRuns);
        }

        public long NextVirtualCluster
        {
            get
            {
                if (_runs.Count == 0)
                {
                    return 0;
                }
                else
                {
                    int lastRun = _runs.Count - 1;
                    return _runs[lastRun].StartVcn + _runs[lastRun].Length;
                }
            }
        }

        public long LastLogicalCluster
        {
            get
            {
                if (_runs.Count == 0)
                {
                    return 0;
                }
                else
                {
                    int lastRun = _runs.Count - 1;
                    return _runs[lastRun].StartLcn + (_runs[lastRun].IsSparse ? 0 : _runs[lastRun].Length - 1);
                }
            }
        }

        public CookedDataRun Last
        {
            get
            {
                if (_runs.Count == 0)
                {
                    return null;
                }
                else
                {
                    return _runs[_runs.Count - 1];
                }
            }
        }

        public CookedDataRun this[int index]
        {
            get { return _runs[index]; }
        }

        public int FindDataRun(long vcn, int startIdx)
        {
            for (int i = startIdx; i < _runs.Count; ++i)
            {
                if (_runs[i].StartVcn + _runs[i].Length > vcn)
                {
                    return i;
                }
            }

            throw new IOException("Looking for VCN outside of data runs");
        }

        public void Append(DataRun rawRun)
        {
            CookedDataRun last = Last;
            _runs.Add(new CookedDataRun(rawRun, NextVirtualCluster, last == null ? 0 : last.StartLcn));
        }

        public void Append(IEnumerable<DataRun> rawRuns)
        {
            long vcn = NextVirtualCluster;
            long lcn = 0;
            foreach (var run in rawRuns)
            {
                _runs.Add(new CookedDataRun(run, vcn, lcn));
                vcn += run.RunLength;
                lcn += run.RunOffset;
            }
        }

        /// <summary>
        /// Truncates the set of data runs
        /// </summary>
        /// <param name="length">The desired length (in clusters)</param>
        public void Truncate(long length)
        {
            int i = 0;
            while (i < _runs.Count && _runs[i].StartVcn + _runs[i].Length <= length)
            {
                ++i;
            }

            if (i < _runs.Count && _runs[i].StartVcn < length)
            {
                _runs[i].DataRun.RunLength = length - _runs[i].StartVcn;
                ++i;
            }

            while (i < _runs.Count)
            {
                _runs.RemoveAt(i);
            }
        }
    }
}

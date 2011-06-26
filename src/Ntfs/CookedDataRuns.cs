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
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class CookedDataRuns
    {
        private List<CookedDataRun> _runs;

        public CookedDataRuns()
        {
            _runs = new List<CookedDataRun>();
        }

        public CookedDataRuns(IEnumerable<DataRun> rawRuns, NonResidentAttributeRecord attributeExtent)
        {
            _runs = new List<CookedDataRun>();
            Append(rawRuns, attributeExtent);
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

        public int Count
        {
            get { return _runs.Count; }
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

        public void Append(DataRun rawRun, NonResidentAttributeRecord attributeExtent)
        {
            CookedDataRun last = Last;
            _runs.Add(new CookedDataRun(rawRun, NextVirtualCluster, last == null ? 0 : last.StartLcn, attributeExtent));
        }

        public void Append(IEnumerable<DataRun> rawRuns, NonResidentAttributeRecord attributeExtent)
        {
            long vcn = NextVirtualCluster;
            long lcn = 0;
            foreach (var run in rawRuns)
            {
                _runs.Add(new CookedDataRun(run, vcn, lcn, attributeExtent));
                vcn += run.RunLength;
                lcn += run.RunOffset;
            }
        }

        public void MakeSparse(int index)
        {
            long prevLcn = index == 0 ? 0 : _runs[index - 1].StartLcn;
            CookedDataRun run = _runs[index];

            if (run.IsSparse)
            {
                throw new ArgumentException("Run is already sparse", "index");
            }

            _runs[index] = new CookedDataRun(new DataRun(0, run.Length, true), run.StartVcn, prevLcn, run.AttributeExtent);
            run.AttributeExtent.ReplaceRun(run.DataRun, _runs[index].DataRun);

            for (int i = index + 1; i < _runs.Count; ++i)
            {
                if (!_runs[i].IsSparse)
                {
                    _runs[i].DataRun.RunOffset += run.StartLcn - prevLcn;
                    break;
                }
            }
        }

        public void MakeNonSparse(int index, IEnumerable<DataRun> rawRuns)
        {
            long prevLcn = index == 0 ? 0 : _runs[index - 1].StartLcn;
            CookedDataRun run = _runs[index];

            if (!run.IsSparse)
            {
                throw new ArgumentException("Run is already non-sparse", "index");
            }

            _runs.RemoveAt(index);
            int insertIdx = run.AttributeExtent.RemoveRun(run.DataRun);

            CookedDataRun lastNewRun = null;
            long lcn = prevLcn;
            long vcn = run.StartVcn;
            foreach (var rawRun in rawRuns)
            {
                CookedDataRun newRun = new CookedDataRun(rawRun, vcn, lcn, run.AttributeExtent);

                _runs.Insert(index, newRun);
                run.AttributeExtent.InsertRun(insertIdx, rawRun);

                vcn += rawRun.RunLength;
                lcn += rawRun.RunOffset;

                lastNewRun = newRun;
                insertIdx++;

                index++;
            }

            for (int i = index; i < _runs.Count; ++i)
            {
                if (_runs[i].IsSparse)
                {
                    _runs[i].StartLcn = lastNewRun.StartLcn;
                }
                else
                {
                    _runs[i].DataRun.RunOffset = _runs[i].StartLcn - lastNewRun.StartLcn;
                    break;
                }
            }
        }

        public void SplitRun(int runIdx, long vcn)
        {
            CookedDataRun run = _runs[runIdx];

            if (run.StartVcn >= vcn || run.StartVcn + run.Length <= vcn)
            {
                throw new ArgumentException("Attempt to split run outside of it's range", "vcn");
            }

            long distance = vcn - run.StartVcn;
            long offset = run.IsSparse ? 0 : distance;
            CookedDataRun newRun = new CookedDataRun(new DataRun(offset, run.Length - distance, run.IsSparse), vcn, run.StartLcn, run.AttributeExtent);

            run.Length = distance;

            _runs.Insert(runIdx + 1, newRun);
            run.AttributeExtent.InsertRun(run.DataRun, newRun.DataRun);

            for (int i = runIdx + 2; i < _runs.Count; ++i)
            {
                if (_runs[i].IsSparse)
                {
                    _runs[i].StartLcn += offset;
                }
                else
                {
                    _runs[i].DataRun.RunOffset -= offset;
                    break;
                }
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

        internal void CollapseRuns()
        {
            int i = 0;
            while (i < _runs.Count - 1)
            {
                if (_runs[i].IsSparse && _runs[i + 1].IsSparse)
                {
                    _runs[i].Length += _runs[i + 1].Length;
                    _runs[i + 1].AttributeExtent.RemoveRun(_runs[i + 1].DataRun);
                    _runs.RemoveAt(i + 1);
                }
                else if (!_runs[i].IsSparse && !_runs[i].IsSparse && _runs[i].StartLcn + _runs[i].Length == _runs[i + 1].StartLcn)
                {
                    _runs[i].Length += _runs[i + 1].Length;
                    _runs[i + 1].AttributeExtent.RemoveRun(_runs[i + 1].DataRun);
                    _runs.RemoveAt(i + 1);

                    for (int j = i + 1; j < _runs.Count; ++j)
                    {
                        if (_runs[j].IsSparse)
                        {
                            _runs[j].StartLcn = _runs[i].StartLcn;
                        }
                        else
                        {
                            _runs[j].DataRun.RunOffset = _runs[j].StartLcn - _runs[i].StartLcn;
                            break;
                        }
                    }
                }
                else
                {
                    ++i;
                }
            }
        }
    }
}

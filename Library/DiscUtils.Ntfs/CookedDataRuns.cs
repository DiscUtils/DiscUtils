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
using System.IO;

namespace DiscUtils.Ntfs
{
    internal class CookedDataRuns
    {
        private int _firstDirty = int.MaxValue;
        private int _lastDirty;
        private readonly List<CookedDataRun> _runs;

        public CookedDataRuns()
        {
            _runs = new List<CookedDataRun>();
        }

        public CookedDataRuns(IEnumerable<DataRun> rawRuns, NonResidentAttributeRecord attributeExtent)
        {
            _runs = new List<CookedDataRun>();
            Append(rawRuns, attributeExtent);
        }

        public int Count
        {
            get { return _runs.Count; }
        }

        public CookedDataRun this[int index]
        {
            get { return _runs[index]; }
        }

        public CookedDataRun Last
        {
            get
            {
                if (_runs.Count == 0)
                {
                    return null;
                }
                return _runs[_runs.Count - 1];
            }
        }

        public long NextVirtualCluster
        {
            get
            {
                if (_runs.Count == 0)
                {
                    return 0;
                }
                int lastRun = _runs.Count - 1;
                return _runs[lastRun].StartVcn + _runs[lastRun].Length;
            }
        }

        public int FindDataRun(long vcn, int startIdx)
        {
            int numRuns = _runs.Count;
            if (numRuns > 0)
            {
                CookedDataRun run = _runs[numRuns - 1];
                if (vcn >= run.StartVcn)
                {
                    if (run.StartVcn + run.Length > vcn)
                    {
                        return numRuns - 1;
                    }
                    throw new IOException("Looking for VCN outside of data runs");
                }

                for (int i = startIdx; i < numRuns; ++i)
                {
                    run = _runs[i];
                    if (run.StartVcn + run.Length > vcn)
                    {
                        return i;
                    }
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
            foreach (DataRun run in rawRuns)
            {
                _runs.Add(new CookedDataRun(run, vcn, lcn, attributeExtent));
                vcn += run.RunLength;
                lcn += run.RunOffset;
            }
        }

        public void MakeSparse(int index)
        {
            if (index < _firstDirty)
            {
                _firstDirty = index;
            }

            if (index > _lastDirty)
            {
                _lastDirty = index;
            }

            long prevLcn = index == 0 ? 0 : _runs[index - 1].StartLcn;
            CookedDataRun run = _runs[index];

            if (run.IsSparse)
            {
                throw new ArgumentException("Run is already sparse", nameof(index));
            }

            _runs[index] = new CookedDataRun(new DataRun(0, run.Length, true), run.StartVcn, prevLcn,
                run.AttributeExtent);
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
            if (index < _firstDirty)
            {
                _firstDirty = index;
            }

            if (index > _lastDirty)
            {
                _lastDirty = index;
            }

            long prevLcn = index == 0 ? 0 : _runs[index - 1].StartLcn;
            CookedDataRun run = _runs[index];

            if (!run.IsSparse)
            {
                throw new ArgumentException("Run is already non-sparse", nameof(index));
            }

            _runs.RemoveAt(index);
            int insertIdx = run.AttributeExtent.RemoveRun(run.DataRun);

            CookedDataRun lastNewRun = null;
            long lcn = prevLcn;
            long vcn = run.StartVcn;
            foreach (DataRun rawRun in rawRuns)
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
            if (runIdx < _firstDirty)
            {
                _firstDirty = runIdx;
            }

            if (runIdx > _lastDirty)
            {
                _lastDirty = runIdx;
            }

            CookedDataRun run = _runs[runIdx];

            if (run.StartVcn >= vcn || run.StartVcn + run.Length <= vcn)
            {
                throw new ArgumentException("Attempt to split run outside of it's range", nameof(vcn));
            }

            long distance = vcn - run.StartVcn;
            long offset = run.IsSparse ? 0 : distance;
            CookedDataRun newRun = new CookedDataRun(new DataRun(offset, run.Length - distance, run.IsSparse), vcn,
                run.StartLcn, run.AttributeExtent);

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
        /// Truncates the set of data runs.
        /// </summary>
        /// <param name="index">The first run to be truncated.</param>
        public void TruncateAt(int index)
        {
            while (index < _runs.Count)
            {
                _runs[index].AttributeExtent.RemoveRun(_runs[index].DataRun);
                _runs.RemoveAt(index);
            }
        }

        internal void CollapseRuns()
        {
            int i = _firstDirty > 1 ? _firstDirty - 1 : 0;
            while (i < _runs.Count - 1 && i <= _lastDirty + 1)
            {
                if (_runs[i].IsSparse && _runs[i + 1].IsSparse)
                {
                    _runs[i].Length += _runs[i + 1].Length;
                    _runs[i + 1].AttributeExtent.RemoveRun(_runs[i + 1].DataRun);
                    _runs.RemoveAt(i + 1);
                }
                else if (!_runs[i].IsSparse && !_runs[i].IsSparse &&
                         _runs[i].StartLcn + _runs[i].Length == _runs[i + 1].StartLcn)
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

            _firstDirty = int.MaxValue;
            _lastDirty = 0;
        }
    }
}
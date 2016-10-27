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

    internal class CookedDataRun
    {
        private long _startVcn;
        private long _startLcn;
        private DataRun _raw;
        private NonResidentAttributeRecord _attributeExtent;

        public CookedDataRun(DataRun raw, long startVcn, long prevLcn, NonResidentAttributeRecord attributeExtent)
        {
            _raw = raw;
            _startVcn = startVcn;
            _startLcn = prevLcn + raw.RunOffset;
            _attributeExtent = attributeExtent;

            if (startVcn < 0)
            {
                throw new ArgumentOutOfRangeException("startVcn", startVcn, "VCN must be >= 0");
            }

            if (_startLcn < 0)
            {
                throw new ArgumentOutOfRangeException("prevLcn", prevLcn, "LCN must be >= 0");
            }
        }

        public long StartVcn
        {
            get { return _startVcn; }
        }

        public long StartLcn
        {
            get { return _startLcn; }
            set { _startLcn = value; }
        }

        public long Length
        {
            get { return _raw.RunLength; }
            set { _raw.RunLength = value; }
        }

        public bool IsSparse
        {
            get { return _raw.IsSparse; }
        }

        public DataRun DataRun
        {
            get { return _raw; }
        }

        public NonResidentAttributeRecord AttributeExtent
        {
            get { return _attributeExtent; }
        }
    }
}

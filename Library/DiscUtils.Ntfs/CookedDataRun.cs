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

namespace DiscUtils.Ntfs
{
    internal class CookedDataRun
    {
        public CookedDataRun(DataRun raw, long startVcn, long prevLcn, NonResidentAttributeRecord attributeExtent)
        {
            DataRun = raw;
            StartVcn = startVcn;
            StartLcn = prevLcn + raw.RunOffset;
            AttributeExtent = attributeExtent;

            if (startVcn < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startVcn), startVcn, "VCN must be >= 0");
            }

            if (StartLcn < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prevLcn), prevLcn, "LCN must be >= 0");
            }
        }

        public NonResidentAttributeRecord AttributeExtent { get; }

        public DataRun DataRun { get; }

        public bool IsSparse
        {
            get { return DataRun.IsSparse; }
        }

        public long Length
        {
            get { return DataRun.RunLength; }
            set { DataRun.RunLength = value; }
        }

        public long StartLcn { get; set; }

        public long StartVcn { get; }
    }
}
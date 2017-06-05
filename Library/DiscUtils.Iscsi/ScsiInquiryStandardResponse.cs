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

using DiscUtils.Streams;

namespace DiscUtils.Iscsi
{
    internal class ScsiInquiryStandardResponse : ScsiResponse
    {
        private bool _truncated;

        public LunClass DeviceType { get; private set; }

        public override uint NeededDataLength
        {
            get { return 36; }
        }

        public string ProductId { get; private set; }

        public string ProductRevision { get; private set; }

        public bool Removable { get; private set; }

        public byte SpecificationVersion { get; private set; }

        public override bool Truncated
        {
            get { return _truncated; }
        }

        public string VendorId { get; private set; }

        public override void ReadFrom(byte[] buffer, int offset, int count)
        {
            if (count < 36)
            {
                _truncated = true;
                return;
            }

            DeviceType = (LunClass)(buffer[0] & 0x1F);
            Removable = (buffer[1] & 0x80) != 0;
            SpecificationVersion = buffer[2];

            VendorId = EndianUtilities.BytesToString(buffer, 8, 8);
            ProductId = EndianUtilities.BytesToString(buffer, 16, 16);
            ProductRevision = EndianUtilities.BytesToString(buffer, 32, 4);
        }
    }
}
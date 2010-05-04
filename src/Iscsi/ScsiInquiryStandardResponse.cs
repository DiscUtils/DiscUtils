//
// Copyright (c) 2008-2010, Kenneth Bell
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


namespace DiscUtils.Iscsi
{
    internal class ScsiInquiryStandardResponse : ScsiResponse
    {
        private bool _truncated;

        private LunClass _deviceType;
        private bool _removable;
        private byte _version;
        private string _vendorId;
        private string _productId;
        private string _productRevision;

        public LunClass DeviceType
        {
            get { return _deviceType; }
        }

        public bool Removable
        {
            get { return _removable; }
        }

        public byte SpecificationVersion
        {
            get { return _version; }
        }

        public string VendorId
        {
            get { return _vendorId; }
        }

        public string ProductId
        {
            get { return _productId; }
        }

        public string ProductRevision
        {
            get { return _productRevision; }
        }

        public override void ReadFrom(byte[] buffer, int offset, int count)
        {
            if (count < 36)
            {
                _truncated = true;
                return;
            }

            _deviceType = (LunClass)(buffer[0] & 0x1F);
            _removable = (buffer[1] & 0x80) != 0;
            _version = buffer[2];

            _vendorId = Utilities.BytesToString(buffer, 8, 8);
            _productId = Utilities.BytesToString(buffer, 16, 16);
            _productRevision = Utilities.BytesToString(buffer, 32, 4);
        }

        public override bool Truncated
        {
            get { return _truncated; }
        }

        public override uint NeededDataLength
        {
            get { return 36; }
        }
    }
}

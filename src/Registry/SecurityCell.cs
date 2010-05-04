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

using System;
using System.Security.AccessControl;

namespace DiscUtils.Registry
{
    internal sealed class SecurityCell : Cell
    {
        private int _prevIndex;
        private int _nextIndex;
        private int _usageCount;
        private RegistrySecurity _secDesc;

        public SecurityCell(RegistrySecurity secDesc)
            : this(-1)
        {
            _secDesc = secDesc;
        }

        public SecurityCell(int index)
            : base(index)
        {
            _prevIndex = -1;
            _nextIndex = -1;
        }

        public int PreviousIndex
        {
            get { return _prevIndex; }
            set { _prevIndex = value; }
        }

        public int NextIndex
        {
            get { return _nextIndex; }
            set { _nextIndex = value; }
        }

        public int UsageCount
        {
            get { return _usageCount; }
            set { _usageCount = value; }
        }

        public RegistrySecurity SecurityDescriptor
        {
            get { return _secDesc; }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            _prevIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x04);
            _nextIndex = Utilities.ToInt32LittleEndian(buffer, offset + 0x08);
            _usageCount = Utilities.ToInt32LittleEndian(buffer, offset + 0x0C);
            int secDescSize = Utilities.ToInt32LittleEndian(buffer, offset + 0x10);

            byte[] secDesc = new byte[secDescSize];
            Array.Copy(buffer, offset + 0x14, secDesc, 0, secDescSize);
            _secDesc = new RegistrySecurity();
            _secDesc.SetSecurityDescriptorBinaryForm(secDesc);

            return 0x14 + secDescSize;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            byte[] sd = _secDesc.GetSecurityDescriptorBinaryForm();

            Utilities.StringToBytes("sk", buffer, offset, 2);
            Utilities.WriteBytesLittleEndian(_prevIndex, buffer, offset + 0x04);
            Utilities.WriteBytesLittleEndian(_nextIndex, buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian(_usageCount, buffer, offset + 0x0C);
            Utilities.WriteBytesLittleEndian(sd.Length, buffer, offset + 0x10);
            Array.Copy(sd, 0, buffer, offset + 0x14, sd.Length);
        }

        public override int Size
        {
            get
            {
                int sdLen = _secDesc.GetSecurityDescriptorBinaryForm().Length;
                return 0x14 + sdLen;
            }
        }

        public override string ToString()
        {
            return "SecDesc:" + _secDesc.GetSecurityDescriptorSddlForm(AccessControlSections.All) + " (refCount:" + _usageCount + ")";
        }
    }
}

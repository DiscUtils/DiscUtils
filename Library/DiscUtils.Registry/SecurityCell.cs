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
using System.Security.AccessControl;
using DiscUtils.Streams;

namespace DiscUtils.Registry
{
    internal sealed class SecurityCell : Cell
    {
        public SecurityCell(RegistrySecurity secDesc)
            : this(-1)
        {
            SecurityDescriptor = secDesc;
        }

        public SecurityCell(int index)
            : base(index)
        {
            PreviousIndex = -1;
            NextIndex = -1;
        }

        public int NextIndex { get; set; }

        public int PreviousIndex { get; set; }

        public RegistrySecurity SecurityDescriptor { get; private set; }

        public override int Size
        {
            get
            {
                int sdLen = SecurityDescriptor.GetSecurityDescriptorBinaryForm().Length;
                return 0x14 + sdLen;
            }
        }

        public int UsageCount { get; set; }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            PreviousIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x04);
            NextIndex = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x08);
            UsageCount = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x0C);
            int secDescSize = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x10);

            byte[] secDesc = new byte[secDescSize];
            Array.Copy(buffer, offset + 0x14, secDesc, 0, secDescSize);
            SecurityDescriptor = new RegistrySecurity();
            SecurityDescriptor.SetSecurityDescriptorBinaryForm(secDesc);

            return 0x14 + secDescSize;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            byte[] sd = SecurityDescriptor.GetSecurityDescriptorBinaryForm();

            EndianUtilities.StringToBytes("sk", buffer, offset, 2);
            EndianUtilities.WriteBytesLittleEndian(PreviousIndex, buffer, offset + 0x04);
            EndianUtilities.WriteBytesLittleEndian(NextIndex, buffer, offset + 0x08);
            EndianUtilities.WriteBytesLittleEndian(UsageCount, buffer, offset + 0x0C);
            EndianUtilities.WriteBytesLittleEndian(sd.Length, buffer, offset + 0x10);
            Array.Copy(sd, 0, buffer, offset + 0x14, sd.Length);
        }

        public override string ToString()
        {
            return "SecDesc:" + SecurityDescriptor.GetSecurityDescriptorSddlForm(AccessControlSections.All) + " (refCount:" +
                   UsageCount + ")";
        }
    }
}
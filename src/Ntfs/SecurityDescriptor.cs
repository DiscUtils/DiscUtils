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
using System.IO;
using System.Security.AccessControl;

namespace DiscUtils.Ntfs
{
    internal sealed class SecurityDescriptor : IByteArraySerializable, IDiagnosticTracer
    {
        private FileSystemSecurity _securityDescriptor;

        public SecurityDescriptor()
        {
            _securityDescriptor = new FileSecurity();
        }

        public FileSystemSecurity Descriptor
        {
            get { return _securityDescriptor; }
        }

        public static uint CalcHash(FileSystemSecurity descriptor)
        {
            byte[] buffer = descriptor.GetSecurityDescriptorBinaryForm();
            uint hash = 0;
            for (int i = 0; i < buffer.Length / 4; ++i)
            {
                hash = Utilities.ToUInt32LittleEndian(buffer, i * 4) + ((hash << 3) | (hash >> 29));
            }
            return hash;
        }

        #region IByteArraySerializable Members

        public void ReadFrom(byte[] buffer, int offset)
        {
            byte[] temp;
            if (offset == 0)
            {
                temp = buffer;
            }
            else
            {
                temp = new byte[buffer.Length - offset];
                Array.Copy(buffer, offset, temp, 0, temp.Length);
            }

            _securityDescriptor.SetSecurityDescriptorBinaryForm(temp, AccessControlSections.All);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            byte[] temp = _securityDescriptor.GetSecurityDescriptorBinaryForm();
            Array.Copy(temp, 0, buffer, offset, temp.Length);
        }

        public int Size
        {
            get
            {
                return _securityDescriptor.GetSecurityDescriptorBinaryForm().Length;
            }
        }

        #endregion

        #region IDiagnosticTracer Members

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "Descriptor: " + _securityDescriptor.GetSecurityDescriptorSddlForm(AccessControlSections.All));
        }

        #endregion
    }
}

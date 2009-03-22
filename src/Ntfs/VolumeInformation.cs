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

namespace DiscUtils.Ntfs
{
    internal sealed class VolumeInformation : IByteArraySerializable, IDiagnosticTraceable
    {
        private byte _majorVersion;
        private byte _minorVersion;
        private VolumeInformationFlags _flags;

        #region IByteArraySerializable Members

        public void ReadFrom(byte[] buffer, int offset)
        {
            _majorVersion = buffer[0x08];
            _minorVersion = buffer[0x09];
            _flags = (VolumeInformationFlags)Utilities.ToUInt16LittleEndian(buffer, 0x0A);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public int Size
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region IDiagnosticTracer Members

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "  Version: " + _majorVersion + "." + _minorVersion);
            writer.WriteLine(indent + "    Flags: " + _flags);
        }

        #endregion
    }
}

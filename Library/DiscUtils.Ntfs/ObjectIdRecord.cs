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
using System.Globalization;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class ObjectIdRecord : IByteArraySerializable
    {
        public Guid BirthDomainId;
        public Guid BirthObjectId;
        public Guid BirthVolumeId;
        public FileRecordReference MftReference;

        public int Size
        {
            get { return 0x38; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            MftReference = new FileRecordReference();
            MftReference.ReadFrom(buffer, offset);

            BirthVolumeId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x08);
            BirthObjectId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x18);
            BirthDomainId = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0x28);
            return 0x38;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            MftReference.WriteTo(buffer, offset);
            EndianUtilities.WriteBytesLittleEndian(BirthVolumeId, buffer, offset + 0x08);
            EndianUtilities.WriteBytesLittleEndian(BirthObjectId, buffer, offset + 0x18);
            EndianUtilities.WriteBytesLittleEndian(BirthDomainId, buffer, offset + 0x28);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "[Data-MftRef:{0},BirthVolId:{1},BirthObjId:{2},BirthDomId:{3}]", MftReference, BirthVolumeId,
                BirthObjectId, BirthDomainId);
        }
    }
}
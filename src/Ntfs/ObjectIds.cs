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
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DiscUtils.Ntfs
{
    internal sealed class ObjectIds
    {
        private Index<IndexKey, IndexData> _index;

        public ObjectIds(File file)
        {
            _index = new Index<IndexKey, IndexData>(file, "$O", file.FileSystem.BiosParameterBlock, null);
        }

        internal void Add(Guid objId, FileReference mftRef, Guid birthId, Guid birthVolumeId, Guid birthDomainId)
        {
            IndexKey newKey = new IndexKey();
            newKey.Id = objId;

            IndexData newData = new IndexData();
            newData.MftReference = mftRef;
            newData.BirthObjectId = birthId;
            newData.BirthVolumeId = birthVolumeId;
            newData.BirthDomainId = birthDomainId;

            _index[newKey] = newData;
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "OBJECT ID INDEX");

            foreach (var entry in _index.Entries)
            {
                writer.WriteLine(indent + "  OBJECT ID INDEX ENTRY");
                writer.WriteLine(indent + "             Id: " + entry.Key.Id);
                writer.WriteLine(indent + "  MFT Reference: " + entry.Value.MftReference);
                writer.WriteLine(indent + "   Birth Volume: " + entry.Value.BirthVolumeId);
                writer.WriteLine(indent + "       Birth Id: " + entry.Value.BirthObjectId);
                writer.WriteLine(indent + "   Birth Domain: " + entry.Value.BirthDomainId);
            }
        }

        internal sealed class IndexKey : IByteArraySerializable
        {
            public Guid Id;

            public void ReadFrom(byte[] buffer, int offset)
            {
                Id = Utilities.ToGuidLittleEndian(buffer, offset + 0);
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Utilities.WriteBytesLittleEndian(Id, buffer, offset + 0);
            }

            public int Size
            {
                get { return 16; }
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Key-Id:{0}]", Id);
            }
        }

        internal sealed class IndexData : IByteArraySerializable
        {
            public FileReference MftReference;
            public Guid BirthVolumeId;
            public Guid BirthObjectId;
            public Guid BirthDomainId;

            public void ReadFrom(byte[] buffer, int offset)
            {
                MftReference = new FileReference();
                MftReference.ReadFrom(buffer, offset);

                BirthVolumeId = Utilities.ToGuidLittleEndian(buffer, offset + 0x08);
                BirthObjectId = Utilities.ToGuidLittleEndian(buffer, offset + 0x18);
                BirthDomainId = Utilities.ToGuidLittleEndian(buffer, offset + 0x28);
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                MftReference.WriteTo(buffer, offset);
                Utilities.WriteBytesLittleEndian(BirthVolumeId, buffer, offset + 0x08);
                Utilities.WriteBytesLittleEndian(BirthObjectId, buffer, offset + 0x18);
                Utilities.WriteBytesLittleEndian(BirthDomainId, buffer, offset + 0x28);
            }

            public int Size
            {
                get { return 0x38; }
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Data-MftRef:{0},BirthVolId:{1},BirthObjId:{2},BirthDomId:{3}]", MftReference, BirthVolumeId, BirthObjectId, BirthDomainId);
            }
        }
    }
}

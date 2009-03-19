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
    internal sealed class ObjectIds : File
    {
        private Index<IndexKey, IndexData> _index;

        public ObjectIds(NtfsFileSystem fileSystem, FileRecord fileRecord)
            : base(fileSystem, fileRecord)
        {
            _index = new Index<IndexKey, IndexData>(this, "$O", _fileSystem.BiosParameterBlock, new IndexKeyComparer());
        }

        public override void Dump(TextWriter writer, string indent)
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

        private sealed class IndexKey : IByteArraySerializable
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

        private sealed class IndexKeyComparer : IComparer<IndexKey>
        {
            public int Compare(IndexKey x, IndexKey y)
            {
                return x.Id.CompareTo(y.Id);
            }
        }

        private sealed class IndexData : IByteArraySerializable
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
                throw new NotImplementedException();
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

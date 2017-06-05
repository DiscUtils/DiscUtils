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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class ObjectIds
    {
        private readonly File _file;
        private readonly IndexView<IndexKey, ObjectIdRecord> _index;

        public ObjectIds(File file)
        {
            _file = file;
            _index = new IndexView<IndexKey, ObjectIdRecord>(file.GetIndex("$O"));
        }

        internal IEnumerable<KeyValuePair<Guid, ObjectIdRecord>> All
        {
            get
            {
                foreach (KeyValuePair<IndexKey, ObjectIdRecord> record in _index.Entries)
                {
                    yield return new KeyValuePair<Guid, ObjectIdRecord>(record.Key.Id, record.Value);
                }
            }
        }

        internal void Add(Guid objId, FileRecordReference mftRef, Guid birthId, Guid birthVolumeId, Guid birthDomainId)
        {
            IndexKey newKey = new IndexKey();
            newKey.Id = objId;

            ObjectIdRecord newData = new ObjectIdRecord();
            newData.MftReference = mftRef;
            newData.BirthObjectId = birthId;
            newData.BirthVolumeId = birthVolumeId;
            newData.BirthDomainId = birthDomainId;

            _index[newKey] = newData;
            _file.UpdateRecordInMft();
        }

        internal void Remove(Guid objId)
        {
            IndexKey key = new IndexKey();
            key.Id = objId;

            _index.Remove(key);
            _file.UpdateRecordInMft();
        }

        internal bool TryGetValue(Guid objId, out ObjectIdRecord value)
        {
            IndexKey key = new IndexKey();
            key.Id = objId;

            return _index.TryGetValue(key, out value);
        }

        internal void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "OBJECT ID INDEX");

            foreach (KeyValuePair<IndexKey, ObjectIdRecord> entry in _index.Entries)
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

            public int Size
            {
                get { return 16; }
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                Id = EndianUtilities.ToGuidLittleEndian(buffer, offset + 0);
                return 16;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                EndianUtilities.WriteBytesLittleEndian(Id, buffer, offset + 0);
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Key-Id:{0}]", Id);
            }
        }
    }
}
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

using System.Collections;
using System.Collections.Generic;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class AttributeList : IByteArraySerializable, IDiagnosticTraceable, ICollection<AttributeListRecord>
    {
        private readonly List<AttributeListRecord> _records;

        public AttributeList()
        {
            _records = new List<AttributeListRecord>();
        }

        public int Size
        {
            get
            {
                int total = 0;
                foreach (AttributeListRecord record in _records)
                {
                    total += record.Size;
                }

                return total;
            }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            _records.Clear();

            int pos = 0;
            while (pos < buffer.Length)
            {
                AttributeListRecord r = new AttributeListRecord();
                pos += r.ReadFrom(buffer, offset + pos);
                _records.Add(r);
            }

            return pos;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            int pos = offset;
            foreach (AttributeListRecord record in _records)
            {
                record.WriteTo(buffer, offset + pos);
                pos += record.Size;
            }
        }

        public int Count
        {
            get { return _records.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void Add(AttributeListRecord item)
        {
            _records.Add(item);
            _records.Sort();
        }

        public void Clear()
        {
            _records.Clear();
        }

        public bool Contains(AttributeListRecord item)
        {
            return _records.Contains(item);
        }

        public void CopyTo(AttributeListRecord[] array, int arrayIndex)
        {
            _records.CopyTo(array, arrayIndex);
        }

        public bool Remove(AttributeListRecord item)
        {
            return _records.Remove(item);
        }

        #region IEnumerable<AttributeListRecord> Members

        public IEnumerator<AttributeListRecord> GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _records.GetEnumerator();
        }

        #endregion

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE LIST RECORDS");
            foreach (AttributeListRecord r in _records)
            {
                r.Dump(writer, indent + "  ");
            }
        }
    }
}
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

using System.IO;

namespace DiscUtils.Ntfs.Attributes
{
    internal class IndexRootAttribute : BaseAttribute
    {
        private uint _attrType;
        private AttributeCollationRule _collationRule;
        private uint _indexAllocationEntrySize;
        private byte _rawClustersPerIndexRecord;

        public const int HeaderOffset = 0x10;

        public IndexRootAttribute(ResidentFileAttributeRecord record)
            : base(null, record)
        {
            using (Stream s = Open(FileAccess.Read))
            {
                byte[] data = Utilities.ReadFully(s, 0x10);
                _attrType = Utilities.ToUInt32LittleEndian(data, 0x00);
                _collationRule = (AttributeCollationRule)Utilities.ToUInt32LittleEndian(data, 0x04);
                _indexAllocationEntrySize = Utilities.ToUInt32LittleEndian(data, 0x08);
                _rawClustersPerIndexRecord = data[0x0C];
            }
        }

        public AttributeCollationRule CollationRule
        {
            get { return _collationRule; }
        }

        public uint IndexAllocationSize
        {
            get { return _indexAllocationEntrySize; }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "INDEX ROOT ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "                Attr Type: " + _attrType);
            writer.WriteLine(indent + "           Collation Rule: " + _collationRule);
            writer.WriteLine(indent + "         Index Alloc Size: " + _indexAllocationEntrySize);
            writer.WriteLine(indent + "  Raw Clusters Per Record: " + _rawClustersPerIndexRecord);

            using (Stream s = Open(FileAccess.Read))
            {
                s.Position = HeaderOffset;
                byte[] data = Utilities.ReadFully(s, IndexHeader.Size);
                IndexHeader header = new IndexHeader(data, 0);
                writer.WriteLine(indent + "    Offset To First Entry: " + header.OffsetToFirstEntry);
                writer.WriteLine(indent + "    Total Size Of Entries: " + header.TotalSizeOfEntries);
                writer.WriteLine(indent + "    Alloc Size Of Entries: " + header.AllocatedSizeOfEntries);
                writer.WriteLine(indent + "          Has Child Nodes: " + header.HasChildNodes);
            }
        }

        public override void Save()
        {
            throw new System.NotImplementedException();
        }
    }

}

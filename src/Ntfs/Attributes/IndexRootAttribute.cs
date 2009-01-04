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
        private uint _rootAttrType;
        private uint _rootCollationRule;
        private uint _rootIndexAllocationEntrySize;
        private byte _rootClustersPerIndexRecord;

        private IndexEntryHeader _header;

        public IndexRootAttribute(ResidentFileAttributeRecord record)
            : base(null, record)
        {
            using (Stream s = Open(FileAccess.Read))
            {
                byte[] data = record.Data;
                _rootAttrType = Utilities.ToUInt32LittleEndian(data, 0x00);
                _rootCollationRule = Utilities.ToUInt32LittleEndian(data, 0x04);
                _rootIndexAllocationEntrySize = Utilities.ToUInt32LittleEndian(data, 0x08);
                _rootClustersPerIndexRecord = data[0x0C];

                _header = new IndexEntryHeader(data, 0x10);
            }
        }

        public IndexEntryHeader Header
        {
            get { return _header; }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "INDEX ROOT ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "            Root Attr Type: " + _rootAttrType);
            writer.WriteLine(indent + "       Root Collation Rule: " + _rootCollationRule);
            writer.WriteLine(indent + "     Root Index Alloc Size: " + _rootIndexAllocationEntrySize);
            writer.WriteLine(indent + "  Root Clusters Per Record: " + _rootClustersPerIndexRecord);

            writer.WriteLine(indent + "     Offset To First Entry: " + _header.OffsetToFirstEntry);
            writer.WriteLine(indent + "     Total Size Of Entries: " + _header.TotalSizeOfEntries);
            writer.WriteLine(indent + "     Alloc Size Of Entries: " + _header.AllocatedSizeOfEntries);
            writer.WriteLine(indent + "                     Flags: " + _header.Flags);
        }
    }

}

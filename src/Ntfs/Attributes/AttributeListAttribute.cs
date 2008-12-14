//
// Copyright (c) 2008, Kenneth Bell
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

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiscUtils.Ntfs.Attributes
{
    internal class AttributeListAttribute : BaseAttribute
    {
        private List<AttributeListRecord> _records;

        public AttributeListAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
            using (Stream s = Open(FileAccess.Read))
            {
                byte[] data = Utilities.ReadFully(s, (int)record.DataLength);

                _records = new List<AttributeListRecord>();

                int pos = 0;
                while (pos < data.Length)
                {
                    AttributeListRecord r = new AttributeListRecord();
                    pos += r.Read(data, pos);
                    _records.Add(r);
                }
            }
        }

        public IEnumerable<AttributeListRecord> Records
        {
            get { return _records; }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE LIST ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            foreach (AttributeListRecord r in _records)
            {
                r.Dump(writer, indent + "  ");
            }
        }

    }


    internal class AttributeListRecord
    {
        public AttributeType Type;
        public ushort RecordLength;
        public byte NameLength;
        public byte NameOffset;
        public string Name;
        public ulong StartVcn;
        public FileReference BaseFileReference;
        public ushort AttributeId;

        public int Read(byte[] data, int offset)
        {
            Type = (AttributeType)Utilities.ToUInt32LittleEndian(data, offset + 0x00);
            RecordLength = Utilities.ToUInt16LittleEndian(data, offset + 0x04);
            NameLength = data[offset + 0x06];
            NameOffset = data[offset + 0x07];
            StartVcn = Utilities.ToUInt64LittleEndian(data, offset + 0x08);
            BaseFileReference = new FileReference(Utilities.ToUInt64LittleEndian(data, offset + 0x10));
            AttributeId = Utilities.ToUInt16LittleEndian(data, offset + 0x18);

            if (NameLength > 0)
            {
                Name = Encoding.Unicode.GetString(data, offset + NameOffset, NameLength * 2);
            }
            else
            {
                Name = null;
            }
            return RecordLength;
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "ATTRIBUTE RECORD");
            writer.WriteLine(indent + "                 Type: " + Type);
            writer.WriteLine(indent + "        Record Length: " + RecordLength);
            writer.WriteLine(indent + "                 Name: " + Name);
            writer.WriteLine(indent + "            Start VCN: " + StartVcn);
            writer.WriteLine(indent + "  Base File Reference: " + BaseFileReference);
            writer.WriteLine(indent + "         Attribute ID: " + AttributeId);
        }
    }
}

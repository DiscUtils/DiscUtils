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
    internal class FileNameAttribute : BaseAttribute
    {
        private FileNameRecord _fileNameRecord;

        public FileNameAttribute(ResidentFileAttributeRecord record)
            : base(null, record)
        {
            _fileNameRecord = new FileNameRecord(record.Data, 0);
        }

        public FileNameRecord FileNameRecord
        {
            get { return _fileNameRecord; }
        }

        public FileAttributes Attributes
        {
            get { return FileNameRecord.ConvertFlags(_fileNameRecord.Flags); }
        }

        public override string ToString()
        {
            return _fileNameRecord.FileName;
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE NAME ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "   Parent Directory: " + _fileNameRecord.ParentDirectory);
            writer.WriteLine(indent + "      Creation Time: " + _fileNameRecord.CreationTime);
            writer.WriteLine(indent + "  Modification Time: " + _fileNameRecord.ModificationTime);
            writer.WriteLine(indent + "   MFT Changed Time: " + _fileNameRecord.MftChangedTime);
            writer.WriteLine(indent + "   Last Access Time: " + _fileNameRecord.LastAccessTime);
            writer.WriteLine(indent + "     Allocated Size: " + _fileNameRecord.AllocatedSize);
            writer.WriteLine(indent + "          Real Size: " + _fileNameRecord.RealSize);
            writer.WriteLine(indent + "              Flags: " + _fileNameRecord.Flags);
            writer.WriteLine(indent + "            Unknown: " + _fileNameRecord.Unknown);
            writer.WriteLine(indent + "          File Name: " + _fileNameRecord.FileName);
        }
    }
}

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

namespace DiscUtils.Ntfs
{
    internal class MasterFileTable : File
    {
        private Bitmap _bitmap;
        private Stream _records;

        private int _recordLength;


        public MasterFileTable(NtfsFileSystem fileSystem, FileRecord baseRecord)
            : base(fileSystem, baseRecord)
        {
            _bitmap = new Bitmap((BitmapFileAttribute)GetAttribute(AttributeType.Bitmap));
            _records = GetAttribute(AttributeType.Data).Open();

            // TODO - How to figure out the record length?
            _recordLength = _fileSystem.BiosParameterBlock.MftRecordSize;
        }

        public IEnumerable<File> Files
        {
            get
            {
                List<File> files = new List<File>();

                byte[] recordBuffer = new byte[_recordLength];

                long numEntries = _records.Length / _recordLength;
                for (long i = 0; i < numEntries; ++i)
                {
                    if (_bitmap.IsPresent(i))
                    {
                        _records.Position = i * _recordLength;
                        int numRead = Utilities.ReadFully(_records, recordBuffer, 0, _recordLength);
                        if (numRead != _recordLength)
                        {
                            throw new IOException("Truncated File Record in MFT");
                        }

                        FileRecord record = new FileRecord(_fileSystem.BiosParameterBlock.BytesPerSector);
                        record.FromBytes(recordBuffer, 0);
                        files.Add(new File(_fileSystem, record));
                    }
                }

                return files;
            }
        }

        public Directory GetDirectory(int index)
        {
            FileRecord record = GetRecord(index);
            if (record != null)
            {
                return new Directory(_fileSystem, record);
            }
            return null;
        }

        public File GetFile(FileReference fileReference)
        {
            FileRecord record = GetRecord(fileReference.MftIndex);
            if (record != null)
            {
                return new File(_fileSystem, record);
            }
            return null;
        }

        public File GetFileOrDirectory(FileReference fileReference)
        {
            FileRecord record = GetRecord(fileReference.MftIndex);
            if (record != null)
            {
                FileNameFileAttribute fnAttr = FileAttribute.FromRecord(_fileSystem, record.GetAttribute(AttributeType.FileName)) as FileNameFileAttribute;
                if ((fnAttr.Attributes & FileAttributes.Directory) != 0)
                {
                    return new Directory(_fileSystem, record);
                }
                else
                {
                    return new File(_fileSystem, record);
                }
            }
            return null;
        }

        private FileRecord GetRecord(long index)
        {
            if (_bitmap.IsPresent(index))
            {
                _records.Position = index * _recordLength;
                byte[] recordBuffer = Utilities.ReadFully(_records, _recordLength);

                FileRecord record = new FileRecord(_fileSystem.BiosParameterBlock.BytesPerSector);
                record.FromBytes(recordBuffer, 0);
                return record;
            }
            return null;
        }

        public override void Dump(TextWriter writer, string indent)
        {
            int recordSize = _fileSystem.BiosParameterBlock.MftRecordSize;
            ushort bytesPerSector = _fileSystem.BiosParameterBlock.BytesPerSector;

            writer.WriteLine(indent + "MASTER FILE TABLE");
            using (Stream mftStream = OpenAttribute(AttributeType.Data))
            {
                while (mftStream.Position < mftStream.Length)
                {
                    byte[] recordData = Utilities.ReadFully(mftStream, recordSize);

                    if (Utilities.BytesToString(recordData, 0, 4) != "FILE")
                    {
                        continue;
                    }

                    FileRecord record = new FileRecord(bytesPerSector);
                    record.FromBytes(recordData, 0);
                    record.Dump(writer, indent + "  ");
                }
            }
        }
    }
}

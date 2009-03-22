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
using System.IO;

namespace DiscUtils.Ntfs
{
    internal class MasterFileTable : IDiagnosticTracer
    {
        private NtfsFileSystem _fileSystem;
        private File _self;
        private File _mirror;
        private Bitmap _bitmap;
        private Stream _records;
        private ObjectCache<long, File> _fileCache;

        private int _recordLength;

        /// <summary>
        /// MFT index of the MFT file itself.
        /// </summary>
        public const long MftIndex = 0;

        /// <summary>
        /// MFT index of the MFT Mirror file.
        /// </summary>
        public const long MftMirrorIndex = 1;

        /// <summary>
        /// MFT Index of the Log file.
        /// </summary>
        public const long LogFileIndex = 2;

        /// <summary>
        /// MFT Index of the Volume file.
        /// </summary>
        public const long VolumeIndex = 3;

        /// <summary>
        /// MFT Index of the Attribute Definition file.
        /// </summary>
        public const long AttrDefIndex = 4;

        /// <summary>
        /// MFT Index of the Root Directory.
        /// </summary>
        public const long RootDirIndex = 5;

        /// <summary>
        /// MFT Index of the Bitmap file.
        /// </summary>
        public const long BitmapIndex = 6;

        /// <summary>
        /// MFT Index of the Boot sector(s).
        /// </summary>
        public const long BootIndex = 7;

        /// <summary>
        /// MFT Index of the Bad Bluster file.
        /// </summary>
        public const long BadClusIndex = 8;

        /// <summary>
        /// MFT Index of the Security Descriptor file.
        /// </summary>
        public const long SecureIndex = 9;

        /// <summary>
        /// MFT Index of the Uppercase mapping file.
        /// </summary>
        public const long UpCaseIndex = 10;

        /// <summary>
        /// MFT Index of the Optional Extensions directory.
        /// </summary>
        public const long ExtendIndex = 11;

        /// <summary>
        /// First MFT Index available for 'normal' files.
        /// </summary>
        private const uint FirstAvailableMftIndex = 24;

        public MasterFileTable(NtfsFileSystem fileSystem, FileRecord baseRecord)
        {
            _fileSystem = fileSystem;
            _self = new File(fileSystem, baseRecord);

            _bitmap = new Bitmap(_self.GetAttribute(AttributeType.Bitmap).Open(FileAccess.ReadWrite), long.MaxValue);
            _records = _self.GetAttribute(AttributeType.Data).Open(FileAccess.ReadWrite);
            _fileCache = new ObjectCache<long,File>();
            _fileCache[MftIndex] = _self;

            _recordLength = fileSystem.BiosParameterBlock.MftRecordSize;

            _mirror = GetFile(MftMirrorIndex);
        }

        public Directory GetDirectory(long index)
        {
            return (Directory)GetFile(index);
        }

        public Directory GetDirectory(FileReference fileReference)
        {
            return (Directory)GetFile(fileReference);
        }

        public File GetFile(FileReference fileReference)
        {
            FileRecord record = GetRecord(fileReference);
            if (record == null)
            {
                return null;
            }

            File file = _fileCache[fileReference.MftIndex];

            if (file != null && file.MftReference.SequenceNumber != record.SequenceNumber)
            {
                file = null;
            }

            if (file == null)
            {
                if ((record.Flags & FileRecordFlags.IsDirectory) != 0)
                {
                    file = new Directory(_fileSystem, record);
                }
                else
                {
                    file = new File(_fileSystem, record);
                }
                _fileCache[fileReference.MftIndex] = file;
            }

            return file;
        }

        public File GetFile(long index)
        {
            FileRecord record = GetRecord(index);
            if (record == null)
            {
                return null;
            }

            File file = _fileCache[index];

            if (file != null && file.MftReference.SequenceNumber != record.SequenceNumber)
            {
                file = null;
            }

            if (file == null)
            {
                if ((record.Flags & FileRecordFlags.IsDirectory) != 0)
                {
                    file = new Directory(_fileSystem, record);
                }
                else
                {
                    file = new File(_fileSystem, record);
                }
                _fileCache[index] = file;
            }

            return file;
        }

        internal File AllocateFile()
        {
            uint index = (uint)_bitmap.AllocateFirstAvailable(FirstAvailableMftIndex);

            FileRecord newRecord;
            if ((index + 1) * _recordLength <= _records.Length)
            {
                newRecord = GetRecord(index);
                newRecord.ReInitialize(_fileSystem.BiosParameterBlock.BytesPerSector, _recordLength, index);
            }
            else
            {
                newRecord = new FileRecord(_fileSystem.BiosParameterBlock.BytesPerSector, _recordLength, index);
            }
            WriteRecord(newRecord);
            File file = new File(_fileSystem, newRecord);
            _fileCache[file.MftReference.MftIndex] = file;
            return file;
        }

        internal FileRecord GetRecord(FileReference fileReference)
        {
            FileRecord result = GetRecord(fileReference.MftIndex);

            if (fileReference.SequenceNumber != 0 && result.SequenceNumber != 0)
            {
                if (fileReference.SequenceNumber != result.SequenceNumber)
                {
                    throw new IOException("Attempt to get an MFT record with an old reference");
                }
            }

            return result;
        }

        internal FileRecord GetRecord(long index)
        {
            if (_bitmap.IsPresent(index))
            {
                if ((index + 1) * _recordLength <= _records.Length)
                {
                    _records.Position = index * _recordLength;
                    byte[] recordBuffer = Utilities.ReadFully(_records, _recordLength);

                    FileRecord record = new FileRecord(_fileSystem.BiosParameterBlock.BytesPerSector);
                    record.FromBytes(recordBuffer, 0);
                    return record;
                }
                else
                {
                    return new FileRecord(_fileSystem.BiosParameterBlock.BytesPerSector, _recordLength, (uint)index);
                }
            }
            return null;
        }

        public int RecordSize
        {
            get { return _recordLength; }
        }

        internal void WriteRecord(FileRecord record)
        {
            int recordSize = record.Size;
            if (recordSize > _recordLength)
            {
                throw new NotImplementedException("Multi-record files");
            }

            byte[] buffer = new byte[_recordLength];
            record.ToBytes(buffer, 0);

            _records.Position = record.MasterFileTableIndex * _recordLength;
            _records.Write(buffer, 0, _recordLength);
            _records.Flush();

            // Need to update Mirror...
            if (record.MasterFileTableIndex < 4)
            {
                using (Stream s = _mirror.GetAttribute(AttributeType.Data).Open(FileAccess.ReadWrite))
                {
                    s.Position = record.MasterFileTableIndex * _recordLength;
                    s.Write(buffer, 0, _recordLength);
                }
            }
        }

        public void Dump(TextWriter writer, string indent)
        {
            int recordSize = _fileSystem.BiosParameterBlock.MftRecordSize;
            ushort bytesPerSector = _fileSystem.BiosParameterBlock.BytesPerSector;

            writer.WriteLine(indent + "MASTER FILE TABLE");
            writer.WriteLine(indent + "  Record Length: " + _recordLength);
            using (Stream mftStream = _self.OpenAttribute(AttributeType.Data, FileAccess.Read))
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

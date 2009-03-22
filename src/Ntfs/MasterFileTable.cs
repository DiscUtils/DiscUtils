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
    /// <summary>
    /// Class representing the $MFT file on disk, including mirror.
    /// </summary>
    /// <remarks>This class only understands basic record structure, and is
    /// ignorant of files that span multiple records.  This class should only
    /// be used by the NtfsFileSystem and File classes.</remarks>
    internal class MasterFileTable : IDiagnosticTraceable
    {
        private File _self;
        private File _mirror;
        private Bitmap _bitmap;
        private Stream _records;

        private int _recordLength;
        private int _bytesPerSector;

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

        public MasterFileTable()
        {
        }

        public static FileRecord GetBootstrapRecord(Stream fsStream, BiosParameterBlock bpb)
        {
            fsStream.Position = bpb.MftCluster * bpb.SectorsPerCluster * bpb.BytesPerSector;
            byte[] mftSelfRecordData = Utilities.ReadFully(fsStream, bpb.MftRecordSize * bpb.SectorsPerCluster * bpb.BytesPerSector);
            FileRecord mftSelfRecord = new FileRecord(bpb.BytesPerSector);
            mftSelfRecord.FromBytes(mftSelfRecordData, 0);
            return mftSelfRecord;
        }

        public void Initialize(File file)
        {
            _self = file;

            _bitmap = new Bitmap(_self.GetAttribute(AttributeType.Bitmap).OpenRaw(FileAccess.ReadWrite), long.MaxValue);
            _records = _self.GetAttribute(AttributeType.Data).OpenRaw(FileAccess.ReadWrite);

            _recordLength = _self.FileSystem.BiosParameterBlock.MftRecordSize;
            _bytesPerSector = _self.FileSystem.BiosParameterBlock.BytesPerSector;
        }

        public int RecordSize
        {
            get { return _recordLength; }
        }

        private File Mirror
        {
            get
            {
                if (_mirror == null)
                {
                    _mirror = _self.FileSystem.GetFile(MftMirrorIndex);
                }
                return _mirror;
            }
        }

        public IEnumerable<FileRecord> Records
        {
            get
            {
                using (Stream mftStream = _self.OpenAttribute(AttributeType.Data, FileAccess.Read))
                {
                    while (mftStream.Position < mftStream.Length)
                    {
                        byte[] recordData = Utilities.ReadFully(mftStream, _recordLength);

                        if (Utilities.BytesToString(recordData, 0, 4) != "FILE")
                        {
                            continue;
                        }

                        FileRecord record = new FileRecord(_bytesPerSector);
                        record.FromBytes(recordData, 0);
                        yield return record;
                    }
                }
            }
        }

        public FileRecord AllocateRecord()
        {
            uint index = (uint)_bitmap.AllocateFirstAvailable(FirstAvailableMftIndex);

            FileRecord newRecord;
            if ((index + 1) * _recordLength <= _records.Length)
            {
                newRecord = GetRecord(index);
                newRecord.ReInitialize(_bytesPerSector, _recordLength, index);
            }
            else
            {
                newRecord = new FileRecord(_bytesPerSector, _recordLength, index);
            }
            WriteRecord(newRecord);
            return newRecord;
        }

        public FileRecord GetRecord(FileReference fileReference)
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

        public FileRecord GetRecord(long index)
        {
            if (_bitmap.IsPresent(index))
            {
                if ((index + 1) * _recordLength <= _records.Length)
                {
                    _records.Position = index * _recordLength;
                    byte[] recordBuffer = Utilities.ReadFully(_records, _recordLength);

                    FileRecord record = new FileRecord(_bytesPerSector);
                    record.FromBytes(recordBuffer, 0);
                    return record;
                }
                else
                {
                    return new FileRecord(_bytesPerSector, _recordLength, (uint)index);
                }
            }
            return null;
        }

        public void WriteRecord(FileRecord record)
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

            // Need to update Mirror.  OpenRaw is OK because this is short duration, and we don't
            // extend or otherwise modify any meta-data, just the content of the Data stream.
            if (record.MasterFileTableIndex < 4)
            {
                using (Stream s = Mirror.GetAttribute(AttributeType.Data).OpenRaw(FileAccess.ReadWrite))
                {
                    s.Position = record.MasterFileTableIndex * _recordLength;
                    s.Write(buffer, 0, _recordLength);
                }
            }
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "MASTER FILE TABLE");
            writer.WriteLine(indent + "  Record Length: " + _recordLength);

            foreach (var record in Records)
            {
                record.Dump(writer, indent + "  ");

                foreach (AttributeRecord attr in record.Attributes)
                {
                    attr.Dump(writer, indent + "     ");
                }
            }
        }

    }
}

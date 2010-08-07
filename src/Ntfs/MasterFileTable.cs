//
// Copyright (c) 2008-2010, Kenneth Bell
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
    /// <summary>
    /// Class representing the $MFT file on disk, including mirror.
    /// </summary>
    /// <remarks>This class only understands basic record structure, and is
    /// ignorant of files that span multiple records.  This class should only
    /// be used by the NtfsFileSystem and File classes.</remarks>
    internal class MasterFileTable : IDiagnosticTraceable, IDisposable
    {
        private File _self;
        private Bitmap _bitmap;
        private Stream _recordStream;
        private ObjectCache<long, FileRecord> _recordCache;

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

        public MasterFileTable(INtfsContext context)
        {
            BiosParameterBlock bpb = context.BiosParameterBlock;

            _recordCache = new ObjectCache<long, FileRecord>();
            _recordLength = bpb.MftRecordSize;
            _bytesPerSector = bpb.BytesPerSector;

            // Temporary record stream - until we've bootstrapped the MFT properly
            _recordStream = new SubStream(context.RawStream, bpb.MftCluster * bpb.SectorsPerCluster * bpb.BytesPerSector, 24 * _recordLength);
        }

        public void Dispose()
        {
            if (_recordStream != null)
            {
                _recordStream.Dispose();
                _recordStream = null;
            }

            GC.SuppressFinalize(this);
        }

        public FileRecord GetBootstrapRecord()
        {
            _recordStream.Position = 0;
            byte[] mftSelfRecordData = Utilities.ReadFully(_recordStream, _recordLength);
            FileRecord mftSelfRecord = new FileRecord(_bytesPerSector);
            mftSelfRecord.FromBytes(mftSelfRecordData, 0);
            _recordCache[MftIndex] = mftSelfRecord;
            return mftSelfRecord;
        }

        public void Initialize(File file)
        {
            _self = file;

            if (_recordStream != null)
            {
                _recordStream.Dispose();
            }

            NtfsStream bitmapStream = _self.GetStream(AttributeType.Bitmap, null);
            _bitmap = new Bitmap(bitmapStream.Open(FileAccess.ReadWrite), long.MaxValue);

            NtfsStream recordsStream = _self.GetStream(AttributeType.Data, null);
            _recordStream = recordsStream.Open(FileAccess.ReadWrite);

        }

        public File InitializeNew(INtfsContext context, long firstBitmapCluster, ulong numBitmapClusters, long firstRecordsCluster, ulong numRecordsClusters)
        {
            BiosParameterBlock bpb = context.BiosParameterBlock;

            FileRecord fileRec = new FileRecord(bpb.BytesPerSector, bpb.MftRecordSize, (uint)MftIndex);
            fileRec.Flags = FileRecordFlags.InUse;
            fileRec.SequenceNumber = 1;
            _recordCache[MftIndex] = fileRec;

            _self = new File(context, fileRec);

            StandardInformation.InitializeNewFile(_self, FileAttributeFlags.Hidden | FileAttributeFlags.System);

            NtfsStream recordsStream = _self.CreateStream(AttributeType.Data, null, firstRecordsCluster, numRecordsClusters, (uint)bpb.BytesPerCluster);
            _recordStream = recordsStream.Open(FileAccess.ReadWrite);
            Wipe(_recordStream);

            NtfsStream bitmapStream = _self.CreateStream(AttributeType.Bitmap, null, firstBitmapCluster, numBitmapClusters, (uint)bpb.BytesPerCluster);
            using (Stream s = bitmapStream.Open(FileAccess.ReadWrite))
            {
                Wipe(s);
                s.SetLength(8);
                _bitmap = new Bitmap(s, long.MaxValue);
            }

            _recordLength = context.BiosParameterBlock.MftRecordSize;
            _bytesPerSector = context.BiosParameterBlock.BytesPerSector;

            _bitmap.MarkPresentRange(0, 1);

            // Write the MFT's own record to itself
            byte[] buffer = new byte[_recordLength];
            fileRec.ToBytes(buffer, 0);
            _recordStream.Position = 0;
            _recordStream.Write(buffer, 0, _recordLength);
            _recordStream.Flush();

            return _self;
        }

        public int RecordSize
        {
            get { return _recordLength; }
        }

        /// <summary>
        /// Reads the records directly from the MFT stream - bypassing the record cache.
        /// </summary>
        public IEnumerable<FileRecord> Records
        {
            get
            {
                using (Stream mftStream = _self.OpenStream(AttributeType.Data, null, FileAccess.Read))
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

        public FileRecord AllocateRecord(FileRecordFlags flags)
        {
            long index = _bitmap.AllocateFirstAvailable(FirstAvailableMftIndex);

            if (index * _recordLength >= _recordStream.Length)
            {
                // Note: 64 is significant, since bitmap extends by 8 bytes (=64 bits) at a time.
                long newEndIndex = Utilities.RoundUp(index + 1, 64);
                for (long i = index; i < newEndIndex; ++i)
                {
                    FileRecord record = new FileRecord(_bytesPerSector, _recordLength, (uint)i);
                    WriteRecord(record);
                }
            }

            FileRecord newRecord = GetRecord(index, true);
            newRecord.ReInitialize(_bytesPerSector, _recordLength, (uint)index);

            _recordCache[index] = newRecord;

            newRecord.Flags = FileRecordFlags.InUse | flags;

            WriteRecord(newRecord);
            _self.UpdateRecordInMft();
            return newRecord;
        }

        public FileRecord AllocateRecord(long index, FileRecordFlags flags)
        {
            _bitmap.MarkPresent(index);

            FileRecord newRecord = new FileRecord(_bytesPerSector, _recordLength, (uint)index);
            _recordCache[index] = newRecord;
            newRecord.Flags = FileRecordFlags.InUse | flags;

            WriteRecord(newRecord);
            _self.UpdateRecordInMft();
            return newRecord;
        }

        public void RemoveRecord(FileRecordReference fileRef)
        {
            FileRecord record = GetRecord(fileRef.MftIndex, false);
            record.Reset();
            WriteRecord(record);

            _recordCache.Remove(fileRef.MftIndex);
            _bitmap.MarkAbsent(fileRef.MftIndex);
            _self.UpdateRecordInMft();
        }

        public FileRecord GetRecord(FileRecordReference fileReference)
        {
            FileRecord result = GetRecord(fileReference.MftIndex, false);

            if (result != null)
            {
                if (fileReference.SequenceNumber != 0 && result.SequenceNumber != 0)
                {
                    if (fileReference.SequenceNumber != result.SequenceNumber)
                    {
                        throw new IOException("Attempt to get an MFT record with an old reference");
                    }
                }
            }

            return result;
        }

        public FileRecord GetRecord(long index, bool ignoreMagic)
        {
            if (_bitmap == null || _bitmap.IsPresent(index))
            {
                FileRecord result = _recordCache[index];
                if (result != null)
                {
                    return result;
                }

                if ((index + 1) * _recordLength <= _recordStream.Length)
                {
                    _recordStream.Position = index * _recordLength;
                    byte[] recordBuffer = Utilities.ReadFully(_recordStream, _recordLength);

                    result = new FileRecord(_bytesPerSector);
                    result.FromBytes(recordBuffer, 0, ignoreMagic);
                }
                else
                {
                    result = new FileRecord(_bytesPerSector, _recordLength, (uint)index);
                }

                _recordCache[index] = result;
                return result;
            }
            return null;
        }

        public void WriteRecord(FileRecord record)
        {
            int recordSize = record.Size;
            if (recordSize > _recordLength)
            {
                throw new IOException("Attempting to write over-sized MFT record");
            }

            byte[] buffer = new byte[_recordLength];
            record.ToBytes(buffer, 0);

            _recordStream.Position = record.MasterFileTableIndex * _recordLength;
            _recordStream.Write(buffer, 0, _recordLength);
            _recordStream.Flush();

            // We may have modified our own meta-data by extending the data stream, so
            // make sure our records are up-to-date.
            if(_self.MftRecordIsDirty)
            {
                DirectoryEntry dirEntry = _self.DirectoryEntry;
                if (dirEntry != null)
                {
                    dirEntry.UpdateFrom(_self);
                }
                _self.UpdateRecordInMft();
            }

            // Need to update Mirror.  OpenRaw is OK because this is short duration, and we don't
            // extend or otherwise modify any meta-data, just the content of the Data stream.
            if (record.MasterFileTableIndex < 4 && _self.Context.GetFileByIndex != null)
            {
                File mftMirror = _self.Context.GetFileByIndex(MftMirrorIndex);
                if (mftMirror != null)
                {
                    using (Stream s = mftMirror.OpenStream(AttributeType.Data, null, FileAccess.ReadWrite))
                    {
                        s.Position = record.MasterFileTableIndex * _recordLength;
                        s.Write(buffer, 0, _recordLength);
                    }
                }
            }
        }

        public long GetRecordOffset(FileRecordReference fileReference)
        {
            return fileReference.MftIndex * _recordLength;
        }

        public ClusterMap GetClusterMap()
        {
            int totalClusters = (int)Utilities.Ceil(_self.Context.BiosParameterBlock.TotalSectors64, _self.Context.BiosParameterBlock.SectorsPerCluster);

            ClusterRoles[] clusterToRole = new ClusterRoles[totalClusters];
            object[] clusterToFile = new object[totalClusters];
            Dictionary<object, string[]> fileToPaths = new Dictionary<object, string[]>();

            for (int i = 0; i < totalClusters; ++i)
            {
                clusterToRole[i] = ClusterRoles.Free;
            }

            foreach (FileRecord fr in Records)
            {
                if (fr.BaseFile.Value != 0)
                {
                    continue;
                }

                File f = new File(_self.Context, fr);

                foreach (var stream in f.AllStreams)
                {
                    string fileId;

                    if (stream.AttributeType == AttributeType.Data && !string.IsNullOrEmpty(stream.Name))
                    {
                        fileId = f.IndexInMft.ToString(CultureInfo.InvariantCulture) + ":" + stream.Name;
                        fileToPaths[fileId] = Utilities.Map(f.Names, n => n + ":" + stream.Name);
                    }
                    else
                    {
                        fileId = f.IndexInMft.ToString(CultureInfo.InvariantCulture);
                        fileToPaths[fileId] = f.Names.ToArray();
                    }

                    ClusterRoles roles = ClusterRoles.None;
                    if (f.IndexInMft < MasterFileTable.FirstAvailableMftIndex)
                    {
                        roles |= ClusterRoles.SystemFile;

                        if (f.IndexInMft == MasterFileTable.BootIndex)
                        {
                            roles |= ClusterRoles.BootArea;
                        }
                    }
                    else
                    {
                        roles |= ClusterRoles.DataFile;
                    }

                    if (stream.AttributeType != AttributeType.Data)
                    {
                        roles |= ClusterRoles.Metadata;
                    }

                    foreach (var range in stream.GetClusters())
                    {
                        for (long cluster = range.Offset; cluster < range.Offset + range.Count; ++cluster)
                        {
                            clusterToRole[cluster] = roles;
                            clusterToFile[cluster] = fileId;
                        }
                    }
                }
            }

            return new ClusterMap(clusterToRole, clusterToFile, fileToPaths);
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

        private static void Wipe(Stream s)
        {
            s.Position = 0;

            byte[] buffer = new byte[64 * Sizes.OneKiB];
            int numWiped = 0;
            while (numWiped < s.Length)
            {
                int toWrite = (int)Math.Min(buffer.Length, s.Length - s.Position);
                s.Write(buffer, 0, toWrite);
                numWiped += toWrite;
            }
        }
    }
}

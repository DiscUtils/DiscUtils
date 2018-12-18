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
using DiscUtils.Internal;
using DiscUtils.Streams;

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

        private Bitmap _bitmap;
        private int _bytesPerSector;
        private readonly ObjectCache<long, FileRecord> _recordCache;

        private Stream _recordStream;

        private File _self;

        public MasterFileTable(INtfsContext context)
        {
            BiosParameterBlock bpb = context.BiosParameterBlock;

            _recordCache = new ObjectCache<long, FileRecord>();
            RecordSize = bpb.MftRecordSize;
            _bytesPerSector = bpb.BytesPerSector;

            // Temporary record stream - until we've bootstrapped the MFT properly
            _recordStream = new SubStream(context.RawStream, bpb.MftCluster * bpb.SectorsPerCluster * bpb.BytesPerSector,
                24 * RecordSize);
        }

        /// <summary>
        /// Gets the MFT records directly from the MFT stream - bypassing the record cache.
        /// </summary>
        public IEnumerable<FileRecord> Records
        {
            get
            {
                using (Stream mftStream = _self.OpenStream(AttributeType.Data, null, FileAccess.Read))
                {
                    uint index = 0;
                    while (mftStream.Position < mftStream.Length)
                    {
                        byte[] recordData = StreamUtilities.ReadExact(mftStream, RecordSize);

                        if (EndianUtilities.BytesToString(recordData, 0, 4) != "FILE")
                        {
                            continue;
                        }

                        FileRecord record = new FileRecord(_bytesPerSector);
                        record.FromBytes(recordData, 0);
                        record.LoadedIndex = index;

                        yield return record;

                        index++;
                    }
                }
            }
        }

        public int RecordSize { get; private set; }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "MASTER FILE TABLE");
            writer.WriteLine(indent + "  Record Length: " + RecordSize);

            foreach (FileRecord record in Records)
            {
                record.Dump(writer, indent + "  ");

                foreach (AttributeRecord attr in record.Attributes)
                {
                    attr.Dump(writer, indent + "     ");
                }
            }
        }

        public void Dispose()
        {
            if (_recordStream != null)
            {
                _recordStream.Dispose();
                _recordStream = null;
            }

            if (_bitmap != null)
            {
                _bitmap.Dispose();
                _bitmap = null;
            }

            GC.SuppressFinalize(this);
        }

        public FileRecord GetBootstrapRecord()
        {
            _recordStream.Position = 0;
            byte[] mftSelfRecordData = StreamUtilities.ReadExact(_recordStream, RecordSize);
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

        public File InitializeNew(INtfsContext context, long firstBitmapCluster, ulong numBitmapClusters,
                                  long firstRecordsCluster, ulong numRecordsClusters)
        {
            BiosParameterBlock bpb = context.BiosParameterBlock;

            FileRecord fileRec = new FileRecord(bpb.BytesPerSector, bpb.MftRecordSize, (uint)MftIndex);
            fileRec.Flags = FileRecordFlags.InUse;
            fileRec.SequenceNumber = 1;
            _recordCache[MftIndex] = fileRec;

            _self = new File(context, fileRec);

            StandardInformation.InitializeNewFile(_self, FileAttributeFlags.Hidden | FileAttributeFlags.System);

            NtfsStream recordsStream = _self.CreateStream(AttributeType.Data, null, firstRecordsCluster,
                numRecordsClusters, (uint)bpb.BytesPerCluster);
            _recordStream = recordsStream.Open(FileAccess.ReadWrite);
            Wipe(_recordStream);

            NtfsStream bitmapStream = _self.CreateStream(AttributeType.Bitmap, null, firstBitmapCluster,
                numBitmapClusters, (uint)bpb.BytesPerCluster);
            using (Stream s = bitmapStream.Open(FileAccess.ReadWrite))
            {
                Wipe(s);
                s.SetLength(8);
                _bitmap = new Bitmap(s, long.MaxValue);
            }

            RecordSize = context.BiosParameterBlock.MftRecordSize;
            _bytesPerSector = context.BiosParameterBlock.BytesPerSector;

            _bitmap.MarkPresentRange(0, 1);

            // Write the MFT's own record to itself
            byte[] buffer = new byte[RecordSize];
            fileRec.ToBytes(buffer, 0);
            _recordStream.Position = 0;
            _recordStream.Write(buffer, 0, RecordSize);
            _recordStream.Flush();

            return _self;
        }

        public FileRecord AllocateRecord(FileRecordFlags flags, bool isMft)
        {
            long index;
            if (isMft)
            {
                // Have to take a lot of care extending the MFT itself, to ensure we never end up unable to
                // bootstrap the file system via the MFT itself - hence why special records are reserved
                // for MFT's own MFT record overflow.
                for (int i = 15; i > 11; --i)
                {
                    FileRecord r = GetRecord(i, false);
                    if (r.BaseFile.SequenceNumber == 0)
                    {
                        r.Reset();
                        r.Flags |= FileRecordFlags.InUse;
                        WriteRecord(r);
                        return r;
                    }
                }

                throw new IOException("MFT too fragmented - unable to allocate MFT overflow record");
            }
            index = _bitmap.AllocateFirstAvailable(FirstAvailableMftIndex);

            if (index * RecordSize >= _recordStream.Length)
            {
                // Note: 64 is significant, since bitmap extends by 8 bytes (=64 bits) at a time.
                long newEndIndex = MathUtilities.RoundUp(index + 1, 64);
                _recordStream.SetLength(newEndIndex * RecordSize);
                for (long i = index; i < newEndIndex; ++i)
                {
                    FileRecord record = new FileRecord(_bytesPerSector, RecordSize, (uint)i);
                    WriteRecord(record);
                }
            }

            FileRecord newRecord = GetRecord(index, true);
            newRecord.ReInitialize(_bytesPerSector, RecordSize, (uint)index);

            _recordCache[index] = newRecord;

            newRecord.Flags = FileRecordFlags.InUse | flags;

            WriteRecord(newRecord);
            _self.UpdateRecordInMft();

            return newRecord;
        }

        public FileRecord AllocateRecord(long index, FileRecordFlags flags)
        {
            _bitmap.MarkPresent(index);

            FileRecord newRecord = new FileRecord(_bytesPerSector, RecordSize, (uint)index);
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
            return GetRecord(index, ignoreMagic, false);
        }

        public FileRecord GetRecord(long index, bool ignoreMagic, bool ignoreBitmap)
        {
            if (ignoreBitmap || _bitmap == null || _bitmap.IsPresent(index))
            {
                FileRecord result = _recordCache[index];
                if (result != null)
                {
                    return result;
                }

                if ((index + 1) * RecordSize <= _recordStream.Length)
                {
                    _recordStream.Position = index * RecordSize;
                    byte[] recordBuffer = StreamUtilities.ReadExact(_recordStream, RecordSize);

                    result = new FileRecord(_bytesPerSector);
                    result.FromBytes(recordBuffer, 0, ignoreMagic);
                    result.LoadedIndex = (uint)index;
                }
                else
                {
                    result = new FileRecord(_bytesPerSector, RecordSize, (uint)index);
                }

                _recordCache[index] = result;
                return result;
            }

            return null;
        }

        public void WriteRecord(FileRecord record)
        {
            int recordSize = record.Size;
            if (recordSize > RecordSize)
            {
                throw new IOException("Attempting to write over-sized MFT record");
            }

            byte[] buffer = new byte[RecordSize];
            record.ToBytes(buffer, 0);

            _recordStream.Position = record.MasterFileTableIndex * RecordSize;
            _recordStream.Write(buffer, 0, RecordSize);
            _recordStream.Flush();

            // We may have modified our own meta-data by extending the data stream, so
            // make sure our records are up-to-date.
            if (_self.MftRecordIsDirty)
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
                        s.Position = record.MasterFileTableIndex * RecordSize;
                        s.Write(buffer, 0, RecordSize);
                    }
                }
            }
        }

        public long GetRecordOffset(FileRecordReference fileReference)
        {
            return fileReference.MftIndex * RecordSize;
        }

        public ClusterMap GetClusterMap()
        {
            int totalClusters =
                (int)
                MathUtilities.Ceil(_self.Context.BiosParameterBlock.TotalSectors64,
                    _self.Context.BiosParameterBlock.SectorsPerCluster);

            ClusterRoles[] clusterToRole = new ClusterRoles[totalClusters];
            object[] clusterToFile = new object[totalClusters];
            Dictionary<object, string[]> fileToPaths = new Dictionary<object, string[]>();

            for (int i = 0; i < totalClusters; ++i)
            {
                clusterToRole[i] = ClusterRoles.Free;
            }

            foreach (FileRecord fr in Records)
            {
                if (fr.BaseFile.Value != 0 || (fr.Flags & FileRecordFlags.InUse) == 0)
                {
                    continue;
                }

                File f = new File(_self.Context, fr);

                foreach (NtfsStream stream in f.AllStreams)
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
                    if (f.IndexInMft < FirstAvailableMftIndex)
                    {
                        roles |= ClusterRoles.SystemFile;

                        if (f.IndexInMft == BootIndex)
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

                    foreach (Range<long, long> range in stream.GetClusters())
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
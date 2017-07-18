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
using System.Text;
using DiscUtils.Streams;

#if !NETCORE
using System.Runtime.Serialization;
#endif

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class that checks NTFS file system integrity.
    /// </summary>
    /// <remarks>Poor relation of chkdsk/fsck.</remarks>
    public sealed class NtfsFileSystemChecker : DiscFileSystemChecker
    {
        private readonly Stream _target;

        private NtfsContext _context;
        private TextWriter _report;
        private ReportLevels _reportLevels;

        private ReportLevels _levelsDetected;
        private readonly ReportLevels _levelsConsideredFail = ReportLevels.Errors;

        /// <summary>
        /// Initializes a new instance of the NtfsFileSystemChecker class.
        /// </summary>
        /// <param name="diskData">The file system to check.</param>
        public NtfsFileSystemChecker(Stream diskData)
        {
            SnapshotStream protectiveStream = new SnapshotStream(diskData, Ownership.None);
            protectiveStream.Snapshot();
            protectiveStream.Freeze();
            _target = protectiveStream;
        }

        /// <summary>
        /// Checks the integrity of an NTFS file system held in a stream.
        /// </summary>
        /// <param name="reportOutput">A report on issues found.</param>
        /// <param name="levels">The amount of detail to report.</param>
        /// <returns><c>true</c> if the file system appears valid, else <c>false</c>.</returns>
        public override bool Check(TextWriter reportOutput, ReportLevels levels)
        {
            _context = new NtfsContext();
            _context.RawStream = _target;
            _context.Options = new NtfsOptions();

            _report = reportOutput;
            _reportLevels = levels;
            _levelsDetected = ReportLevels.None;

            try
            {
                DoCheck();
            }
            catch (AbortException ae)
            {
                ReportError("File system check aborted: " + ae);
                return false;
            }
            catch (Exception e)
            {
                ReportError("File system check aborted with exception: " + e);
                return false;
            }

            return (_levelsDetected & _levelsConsideredFail) == 0;
        }

        /// <summary>
        /// Gets an object that can convert between clusters and files.
        /// </summary>
        /// <returns>The cluster map.</returns>
        public ClusterMap BuildClusterMap()
        {
            _context = new NtfsContext();
            _context.RawStream = _target;
            _context.Options = new NtfsOptions();

            _context.RawStream.Position = 0;
            byte[] bytes = StreamUtilities.ReadExact(_context.RawStream, 512);

            _context.BiosParameterBlock = BiosParameterBlock.FromBytes(bytes, 0);

            _context.Mft = new MasterFileTable(_context);
            File mftFile = new File(_context, _context.Mft.GetBootstrapRecord());
            _context.Mft.Initialize(mftFile);
            return _context.Mft.GetClusterMap();
        }

        private static void Abort()
        {
            throw new AbortException();
        }

        private void DoCheck()
        {
            _context.RawStream.Position = 0;
            byte[] bytes = StreamUtilities.ReadExact(_context.RawStream, 512);

            _context.BiosParameterBlock = BiosParameterBlock.FromBytes(bytes, 0);

            //-----------------------------------------------------------------------
            // MASTER FILE TABLE
            //

            // Bootstrap the Master File Table
            _context.Mft = new MasterFileTable(_context);
            File mftFile = new File(_context, _context.Mft.GetBootstrapRecord());

            // Verify basic MFT records before initializing the Master File Table
            PreVerifyMft(mftFile);
            _context.Mft.Initialize(mftFile);

            // Now the MFT is up and running, do more detailed analysis of it's contents - double-accounted clusters, etc
            VerifyMft();
            _context.Mft.Dump(_report, "INFO: ");

            //-----------------------------------------------------------------------
            // INDEXES
            //

            // Need UpperCase in order to verify some indexes (i.e. directories).
            File ucFile = new File(_context, _context.Mft.GetRecord(MasterFileTable.UpCaseIndex, false));
            _context.UpperCase = new UpperCase(ucFile);

            SelfCheckIndexes();

            //-----------------------------------------------------------------------
            // DIRECTORIES
            //
            VerifyDirectories();

            //-----------------------------------------------------------------------
            // WELL KNOWN FILES
            //
            VerifyWellKnownFilesExist();

            //-----------------------------------------------------------------------
            // OBJECT IDS
            //
            VerifyObjectIds();

            //-----------------------------------------------------------------------
            // FINISHED
            //

            // Temporary...
            using (NtfsFileSystem fs = new NtfsFileSystem(_context.RawStream))
            {
                if ((_reportLevels & ReportLevels.Information) != 0)
                {
                    ReportDump(fs);
                }
            }
        }

        private void VerifyWellKnownFilesExist()
        {
            Directory rootDir = new Directory(_context, _context.Mft.GetRecord(MasterFileTable.RootDirIndex, false));

            DirectoryEntry extendDirEntry = rootDir.GetEntryByName("$Extend");
            if (extendDirEntry == null)
            {
                ReportError("$Extend does not exist in root directory");
                Abort();
            }

            Directory extendDir = new Directory(_context, _context.Mft.GetRecord(extendDirEntry.Reference));

            DirectoryEntry objIdDirEntry = extendDir.GetEntryByName("$ObjId");
            if (objIdDirEntry == null)
            {
                ReportError("$ObjId does not exist in $Extend directory");
                Abort();
            }

            // Stash ObjectIds
            _context.ObjectIds = new ObjectIds(new File(_context, _context.Mft.GetRecord(objIdDirEntry.Reference)));

            DirectoryEntry sysVolInfDirEntry = rootDir.GetEntryByName("System Volume Information");
            if (sysVolInfDirEntry == null)
            {
                ReportError("'System Volume Information' does not exist in root directory");
                Abort();
            }
            ////Directory sysVolInfDir = new Directory(_context, _context.Mft.GetRecord(sysVolInfDirEntry.Reference));
        }

        private void VerifyObjectIds()
        {
            foreach (FileRecord fr in _context.Mft.Records)
            {
                if (fr.BaseFile.Value != 0)
                {
                    File f = new File(_context, fr);
                    foreach (NtfsStream stream in f.AllStreams)
                    {
                        if (stream.AttributeType == AttributeType.ObjectId)
                        {
                            ObjectId objId = stream.GetContent<ObjectId>();
                            ObjectIdRecord objIdRec;
                            if (!_context.ObjectIds.TryGetValue(objId.Id, out objIdRec))
                            {
                                ReportError("ObjectId {0} for file {1} is not indexed", objId.Id, f.BestName);
                            }
                            else if (objIdRec.MftReference != f.MftReference)
                            {
                                ReportError("ObjectId {0} for file {1} points to {2}", objId.Id, f.BestName,
                                    objIdRec.MftReference);
                            }
                        }
                    }
                }
            }

            foreach (KeyValuePair<Guid, ObjectIdRecord> objIdRec in _context.ObjectIds.All)
            {
                if (_context.Mft.GetRecord(objIdRec.Value.MftReference) == null)
                {
                    ReportError("ObjectId {0} refers to non-existant file {1}", objIdRec.Key,
                        objIdRec.Value.MftReference);
                }
            }
        }

        private void VerifyDirectories()
        {
            foreach (FileRecord fr in _context.Mft.Records)
            {
                if (fr.BaseFile.Value != 0)
                {
                    continue;
                }

                File f = new File(_context, fr);
                foreach (NtfsStream stream in f.AllStreams)
                {
                    if (stream.AttributeType == AttributeType.IndexRoot && stream.Name == "$I30")
                    {
                        IndexView<FileNameRecord, FileRecordReference> dir =
                            new IndexView<FileNameRecord, FileRecordReference>(f.GetIndex("$I30"));
                        foreach (KeyValuePair<FileNameRecord, FileRecordReference> entry in dir.Entries)
                        {
                            FileRecord refFile = _context.Mft.GetRecord(entry.Value);

                            // Make sure each referenced file actually exists...
                            if (refFile == null)
                            {
                                ReportError("Directory {0} references non-existent file {1}", f, entry.Key);
                            }

                            File referencedFile = new File(_context, refFile);
                            StandardInformation si = referencedFile.StandardInformation;
                            if (si.CreationTime != entry.Key.CreationTime ||
                                si.MftChangedTime != entry.Key.MftChangedTime
                                || si.ModificationTime != entry.Key.ModificationTime)
                            {
                                ReportInfo("Directory entry {0} in {1} is out of date", entry.Key, f);
                            }
                        }
                    }
                }
            }
        }

        private void SelfCheckIndexes()
        {
            foreach (FileRecord fr in _context.Mft.Records)
            {
                File f = new File(_context, fr);
                foreach (NtfsStream stream in f.AllStreams)
                {
                    if (stream.AttributeType == AttributeType.IndexRoot)
                    {
                        SelfCheckIndex(f, stream.Name);
                    }
                }
            }
        }

        private void SelfCheckIndex(File file, string name)
        {
            ReportInfo("About to self-check index {0} in file {1} (MFT:{2})", name, file.BestName, file.IndexInMft);

            IndexRoot root = file.GetStream(AttributeType.IndexRoot, name).GetContent<IndexRoot>();

            byte[] rootBuffer;
            using (Stream s = file.OpenStream(AttributeType.IndexRoot, name, FileAccess.Read))
            {
                rootBuffer = StreamUtilities.ReadExact(s, (int)s.Length);
            }

            Bitmap indexBitmap = null;
            if (file.GetStream(AttributeType.Bitmap, name) != null)
            {
                indexBitmap = new Bitmap(file.OpenStream(AttributeType.Bitmap, name, FileAccess.Read), long.MaxValue);
            }

            if (!SelfCheckIndexNode(rootBuffer, IndexRoot.HeaderOffset, indexBitmap, root, file.BestName, name))
            {
                ReportError("Index {0} in file {1} (MFT:{2}) has corrupt IndexRoot attribute", name, file.BestName,
                    file.IndexInMft);
            }
            else
            {
                ReportInfo("Self-check of index {0} in file {1} (MFT:{2}) complete", name, file.BestName,
                    file.IndexInMft);
            }
        }

        private bool SelfCheckIndexNode(byte[] buffer, int offset, Bitmap bitmap, IndexRoot root, string fileName,
                                        string indexName)
        {
            bool ok = true;

            IndexHeader header = new IndexHeader(buffer, offset);

            IndexEntry lastEntry = null;

            IComparer<byte[]> collator = root.GetCollator(_context.UpperCase);

            int pos = (int)header.OffsetToFirstEntry;
            while (pos < header.TotalSizeOfEntries)
            {
                IndexEntry entry = new IndexEntry(indexName == "$I30");
                entry.Read(buffer, offset + pos);
                pos += entry.Size;

                if ((entry.Flags & IndexEntryFlags.Node) != 0)
                {
                    long bitmapIdx = entry.ChildrenVirtualCluster /
                                     MathUtilities.Ceil(root.IndexAllocationSize,
                                         _context.BiosParameterBlock.SectorsPerCluster *
                                         _context.BiosParameterBlock.BytesPerSector);
                    if (!bitmap.IsPresent(bitmapIdx))
                    {
                        ReportError("Index entry {0} is non-leaf, but child vcn {1} is not in bitmap at index {2}",
                            Index.EntryAsString(entry, fileName, indexName), entry.ChildrenVirtualCluster, bitmapIdx);
                    }
                }

                if ((entry.Flags & IndexEntryFlags.End) != 0)
                {
                    if (pos != header.TotalSizeOfEntries)
                    {
                        ReportError("Found END index entry {0}, but not at end of node",
                            Index.EntryAsString(entry, fileName, indexName));
                        ok = false;
                    }
                }

                if (lastEntry != null && collator.Compare(lastEntry.KeyBuffer, entry.KeyBuffer) >= 0)
                {
                    ReportError("Found entries out of order {0} was before {1}",
                        Index.EntryAsString(lastEntry, fileName, indexName),
                        Index.EntryAsString(entry, fileName, indexName));
                    ok = false;
                }

                lastEntry = entry;
            }

            return ok;
        }

        private void PreVerifyMft(File file)
        {
            int recordLength = _context.BiosParameterBlock.MftRecordSize;
            int bytesPerSector = _context.BiosParameterBlock.BytesPerSector;

            // Check out the MFT's clusters
            foreach (Range<long, long> range in file.GetAttribute(AttributeType.Data, null).GetClusters())
            {
                if (!VerifyClusterRange(range))
                {
                    ReportError("Corrupt cluster range in MFT data attribute {0}", range.ToString());
                    Abort();
                }
            }

            foreach (Range<long, long> range in file.GetAttribute(AttributeType.Bitmap, null).GetClusters())
            {
                if (!VerifyClusterRange(range))
                {
                    ReportError("Corrupt cluster range in MFT bitmap attribute {0}", range.ToString());
                    Abort();
                }
            }

            using (Stream mftStream = file.OpenStream(AttributeType.Data, null, FileAccess.Read))
            using (Stream bitmapStream = file.OpenStream(AttributeType.Bitmap, null, FileAccess.Read))
            {
                Bitmap bitmap = new Bitmap(bitmapStream, long.MaxValue);

                long index = 0;
                while (mftStream.Position < mftStream.Length)
                {
                    byte[] recordData = StreamUtilities.ReadExact(mftStream, recordLength);

                    string magic = EndianUtilities.BytesToString(recordData, 0, 4);
                    if (magic != "FILE")
                    {
                        if (bitmap.IsPresent(index))
                        {
                            ReportError("Invalid MFT record magic at index {0} - was ({2},{3},{4},{5}) \"{1}\"", index,
                                magic.Trim('\0'), (int)magic[0], (int)magic[1], (int)magic[2], (int)magic[3]);
                        }
                    }
                    else
                    {
                        if (!VerifyMftRecord(recordData, bitmap.IsPresent(index), bytesPerSector))
                        {
                            ReportError("Invalid MFT record at index {0}", index);
                            StringBuilder bldr = new StringBuilder();
                            for (int i = 0; i < recordData.Length; ++i)
                            {
                                bldr.Append(string.Format(CultureInfo.InvariantCulture, " {0:X2}", recordData[i]));
                            }

                            ReportInfo("MFT record binary data for index {0}:{1}", index, bldr.ToString());
                        }
                    }

                    index++;
                }
            }
        }

        private void VerifyMft()
        {
            // Cluster allocation check - check for double allocations
            Dictionary<long, string> clusterMap = new Dictionary<long, string>();
            foreach (FileRecord fr in _context.Mft.Records)
            {
                if ((fr.Flags & FileRecordFlags.InUse) != 0)
                {
                    File f = new File(_context, fr);
                    foreach (NtfsAttribute attr in f.AllAttributes)
                    {
                        string attrKey = fr.MasterFileTableIndex + ":" + attr.Id;

                        foreach (Range<long, long> range in attr.GetClusters())
                        {
                            if (!VerifyClusterRange(range))
                            {
                                ReportError("Attribute {0} contains bad cluster range {1}", attrKey, range);
                            }

                            for (long cluster = range.Offset; cluster < range.Offset + range.Count; ++cluster)
                            {
                                string existingKey;
                                if (clusterMap.TryGetValue(cluster, out existingKey))
                                {
                                    ReportError(
                                        "Two attributes referencing cluster {0} (0x{0:X16}) - {1} and {2} (as MftIndex:AttrId)",
                                        cluster, existingKey, attrKey);
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool VerifyMftRecord(byte[] recordData, bool presentInBitmap, int bytesPerSector)
        {
            bool ok = true;

            //
            // Verify the attributes seem OK...
            //
            byte[] tempBuffer = new byte[recordData.Length];
            Array.Copy(recordData, tempBuffer, tempBuffer.Length);
            GenericFixupRecord genericRecord = new GenericFixupRecord(bytesPerSector);
            genericRecord.FromBytes(tempBuffer, 0);

            int pos = EndianUtilities.ToUInt16LittleEndian(genericRecord.Content, 0x14);
            while (EndianUtilities.ToUInt32LittleEndian(genericRecord.Content, pos) != 0xFFFFFFFF)
            {
                int attrLen;
                try
                {
                    AttributeRecord ar = AttributeRecord.FromBytes(genericRecord.Content, pos, out attrLen);
                    if (attrLen != ar.Size)
                    {
                        ReportError("Attribute size is different to calculated size.  AttrId={0}", ar.AttributeId);
                        ok = false;
                    }

                    if (ar.IsNonResident)
                    {
                        NonResidentAttributeRecord nrr = (NonResidentAttributeRecord)ar;
                        if (nrr.DataRuns.Count > 0)
                        {
                            long totalVcn = 0;
                            foreach (DataRun run in nrr.DataRuns)
                            {
                                totalVcn += run.RunLength;
                            }

                            if (totalVcn != nrr.LastVcn - nrr.StartVcn + 1)
                            {
                                ReportError("Declared VCNs doesn't match data runs.  AttrId={0}", ar.AttributeId);
                                ok = false;
                            }
                        }
                    }
                }
                catch
                {
                    ReportError("Failure parsing attribute at pos={0}", pos);
                    return false;
                }

                pos += attrLen;
            }

            //
            // Now consider record as a whole
            //
            FileRecord record = new FileRecord(bytesPerSector);
            record.FromBytes(recordData, 0);

            bool inUse = (record.Flags & FileRecordFlags.InUse) != 0;
            if (inUse != presentInBitmap)
            {
                ReportError("MFT bitmap and record in-use flag don't agree.  Mft={0}, Record={1}",
                    presentInBitmap ? "InUse" : "Free", inUse ? "InUse" : "Free");
                ok = false;
            }

            if (record.Size != record.RealSize)
            {
                ReportError("MFT record real size is different to calculated size.  Stored in MFT={0}, Calculated={1}",
                    record.RealSize, record.Size);
                ok = false;
            }

            if (EndianUtilities.ToUInt32LittleEndian(recordData, (int)record.RealSize - 8) != uint.MaxValue)
            {
                ReportError("MFT record is not correctly terminated with 0xFFFFFFFF");
                ok = false;
            }

            return ok;
        }

        private bool VerifyClusterRange(Range<long, long> range)
        {
            bool ok = true;
            if (range.Offset < 0)
            {
                ReportError("Invalid cluster range {0} - negative start", range);
                ok = false;
            }

            if (range.Count <= 0)
            {
                ReportError("Invalid cluster range {0} - negative/zero count", range);
                ok = false;
            }

            if ((range.Offset + range.Count) * _context.BiosParameterBlock.BytesPerCluster > _context.RawStream.Length)
            {
                ReportError("Invalid cluster range {0} - beyond end of disk", range);
                ok = false;
            }

            return ok;
        }

        private void ReportDump(IDiagnosticTraceable toDump)
        {
            _levelsDetected |= ReportLevels.Information;
            if ((_reportLevels & ReportLevels.Information) != 0)
            {
                toDump.Dump(_report, "INFO: ");
            }
        }

        private void ReportInfo(string str, params object[] args)
        {
            _levelsDetected |= ReportLevels.Information;
            if ((_reportLevels & ReportLevels.Information) != 0)
            {
                _report.WriteLine("INFO: " + str, args);
            }
        }

        private void ReportError(string str, params object[] args)
        {
            _levelsDetected |= ReportLevels.Errors;
            if ((_reportLevels & ReportLevels.Errors) != 0)
            {
                _report.WriteLine("ERROR: " + str, args);
            }
        }

#if !NETCORE
        [Serializable]
#endif
        private sealed class AbortException : InvalidFileSystemException
        {

        }
    }
}
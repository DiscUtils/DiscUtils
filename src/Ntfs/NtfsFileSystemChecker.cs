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
using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace DiscUtils.Ntfs
{
    /// <summary>
    /// Class that checks NTFS file system integrity.
    /// </summary>
    /// <remarks>Poor relation of chkdsk/fsck.</remarks>
    public sealed class NtfsFileSystemChecker : DiscFileSystemChecker
    {
        private Stream _target;

        private NtfsContext _context;
        private TextWriter _report;
        private ReportLevels _reportLevels;

        private ReportLevels _levelsDetected;
        private ReportLevels _levelsConsideredFail = ReportLevels.Errors;

        /// <summary>
        /// Creates a new instance to check a particular stream.
        /// </summary>
        /// <param name="diskData">The file system to check</param>
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
        /// <param name="reportOutput">A report on issues found</param>
        /// <param name="levels">The amount of detail to report</param>
        /// <returns><c>true</c> if the file system appears valid, else <c>false</c></returns>
        public override bool Check(TextWriter reportOutput, ReportLevels levels)
        {
            _context = new NtfsContext();
            _context.RawStream = _target;

            _report = reportOutput;
            _reportLevels = levels;
            _levelsDetected = ReportLevels.None;

            try
            {
                DoCheck();
            }
            catch (AbortException ae)
            {
                ReportError("File system check aborted: " + ae.ToString());
                return false;
            }
            catch (Exception e)
            {
                ReportError("File system check aborted with exception: " + e.ToString());
                return false;
            }

            return (_levelsDetected & _levelsConsideredFail) == 0;
        }

        public ClusterMap GetClusterMap()
        {
            _context = new NtfsContext();
            _context.RawStream = _target;

            _context.RawStream.Position = 0;
            byte[] bytes = Utilities.ReadFully(_context.RawStream, 512);


            _context.BiosParameterBlock = BiosParameterBlock.FromBytes(bytes, 0);

            _context.Mft = new MasterFileTable();
            File mftFile = new File(_context, MasterFileTable.GetBootstrapRecord(_context.RawStream, _context.BiosParameterBlock));
            _context.Mft.Initialize(mftFile);
            return _context.Mft.GetClusterMap();
        }

        private void DoCheck()
        {
            _context.RawStream.Position = 0;
            byte[] bytes = Utilities.ReadFully(_context.RawStream, 512);


            _context.BiosParameterBlock = BiosParameterBlock.FromBytes(bytes, 0);

            //-----------------------------------------------------------------------
            // MASTER FILE TABLE
            //

            // Bootstrap the Master File Table
            _context.Mft = new MasterFileTable();
            File mftFile = new File(_context, MasterFileTable.GetBootstrapRecord(_context.RawStream, _context.BiosParameterBlock));

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
            // WELL KNOWN FILES
            //
            VerifyWellKnownFiles();

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

        private void VerifyWellKnownFiles()
        {
            Directory rootDir = new Directory(_context, _context.Mft, _context.Mft.GetRecord(MasterFileTable.RootDirIndex, false));

            DirectoryEntry extendDirEntry = rootDir.GetEntryByName("$Extend");
            if (extendDirEntry == null)
            {
                ReportError("$Extend does not exist in root directory");
                Abort();
            }
            Directory extendDir = new Directory(_context, _context.Mft, _context.Mft.GetRecord(extendDirEntry.Reference));

            DirectoryEntry objIdDirEntry = extendDir.GetEntryByName("$ObjId");
            if (objIdDirEntry == null)
            {
                ReportError("$ObjId does not exist in $Extend directory");
                Abort();
            }
        }

        private void SelfCheckIndexes()
        {
            foreach (FileRecord fr in _context.Mft.Records)
            {
                File f = new File(_context, fr);
                foreach (var attr in f.AllAttributes)
                {
                    if (attr.Record.AttributeType == AttributeType.IndexRoot)
                    {
                        SelfCheckIndex(f, attr.Name);
                    }
                }
            }
        }

        private void SelfCheckIndex(File file, string name)
        {
            ReportInfo("About to self-check index {0} in file {1} (MFT:{2})", name, file.BestName, file.IndexInMft);

            IndexRoot root = file.GetAttributeContent<IndexRoot>(AttributeType.IndexRoot, name);

            byte[] rootBuffer;
            using (Stream s = file.OpenAttribute(AttributeType.IndexRoot, name, FileAccess.Read))
            {
                rootBuffer = Utilities.ReadFully(s, (int)s.Length);
            }

            Bitmap indexBitmap = null;
            if (file.GetAttribute(AttributeType.Bitmap, name) != null)
            {
                indexBitmap = new Bitmap(file.OpenAttribute(AttributeType.Bitmap, name, FileAccess.Read), long.MaxValue);
            }

            if (!SelfCheckIndexNode(rootBuffer, IndexRoot.HeaderOffset, indexBitmap, root, file.BestName, name))
            {
                ReportError("Index {0} in file {1} (MFT:{2}) has corrupt IndexRoot attribute", name, file.BestName, file.IndexInMft);
            }
            else
            {
                ReportInfo("Self-check of index {0} in file {1} (MFT:{2}) complete", name, file.BestName, file.IndexInMft);
            }
        }

        private bool SelfCheckIndexNode(byte[] buffer, int offset, Bitmap bitmap, IndexRoot root, string fileName, string indexName)
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

                if((entry.Flags & IndexEntryFlags.Node) != 0)
                {
                    long bitmapIdx = entry.ChildrenVirtualCluster / root.IndexAllocationSize;
                    if(!bitmap.IsPresent(bitmapIdx))
                    {
                        ReportError("Index entry {0} is non-leaf, but child vcn {1} is not in bitmap at index {2}", IndexEntryToString(entry, fileName, indexName), entry.ChildrenVirtualCluster, bitmapIdx);
                    }
                }

                if ((entry.Flags & IndexEntryFlags.End) != 0)
                {
                    if (pos != header.TotalSizeOfEntries)
                    {
                        ReportError("Found END index entry {0}, but not at end of node", IndexEntryToString(entry, fileName, indexName));
                        ok = false;
                    }
                }

                if (lastEntry != null && collator.Compare(lastEntry.KeyBuffer, entry.KeyBuffer) >= 0)
                {
                    ReportError("Found entries out of order {0} was before {1}", IndexEntryToString(lastEntry, fileName, indexName), IndexEntryToString(entry, fileName, indexName));
                    ok = false;
                }


                lastEntry = entry;
            }

            return ok;
        }

        private String IndexEntryToString(IndexEntry entry, string fileName, string indexName)
        {
            IByteArraySerializable keyValue = null;
            IByteArraySerializable dataValue = null;

            // Try to guess the type of data in the key and data fields from the filename and index name
            if (indexName == "$I30")
            {
                keyValue = new FileNameRecord();
                dataValue = new FileReference();
            }
            else if (fileName == "$ObjId" && indexName == "$O")
            {
                keyValue = new ObjectIds.IndexKey();
                dataValue = new ObjectIds.IndexData();
            }
            else if (fileName == "$Secure")
            {
                if (indexName == "$SII")
                {
                    keyValue = new SecurityDescriptors.IdIndexKey();
                    dataValue = new SecurityDescriptors.IndexData();
                }
                else if (indexName == "$SDH")
                {
                    keyValue = new SecurityDescriptors.HashIndexKey();
                    dataValue = new SecurityDescriptors.IndexData();
                }
            }

            try
            {
                if (keyValue != null && dataValue != null)
                {
                    keyValue.ReadFrom(entry.KeyBuffer, 0);
                    dataValue.ReadFrom(entry.DataBuffer, 0);
                }

                return "{" + keyValue.ToString() + "-->" + dataValue.ToString() + "}";
            }
            catch
            {
            }

            return "{Unknown-Index-Type}";
        }

        private void PreVerifyMft(File file)
        {
            int recordLength = _context.BiosParameterBlock.MftRecordSize;
            int bytesPerSector = _context.BiosParameterBlock.BytesPerSector;

            // Check out the MFT's clusters
            foreach (var range in file.GetAttribute(AttributeType.Data).GetClusters())
            {
                if (!VerifyClusterRange(range))
                {
                    ReportError("Corrupt cluster range in MFT data attribute {0}", range.ToString());
                    Abort();
                }
            }
            foreach (var range in file.GetAttribute(AttributeType.Bitmap).GetClusters())
            {
                if (!VerifyClusterRange(range))
                {
                    ReportError("Corrupt cluster range in MFT bitmap attribute {0}", range.ToString());
                    Abort();
                }
            }


            using (Stream mftStream = file.OpenAttribute(AttributeType.Data, FileAccess.Read))
            using (Stream bitmapStream = file.OpenAttribute(AttributeType.Bitmap, FileAccess.Read))
            {

                Bitmap bitmap = new Bitmap(bitmapStream, long.MaxValue);

                long index = 0;
                while (mftStream.Position < mftStream.Length)
                {
                    byte[] recordData = Utilities.ReadFully(mftStream, recordLength);

                    string magic = Utilities.BytesToString(recordData, 0, 4);
                    if (magic != "FILE")
                    {
                        if (bitmap.IsPresent(index))
                        {
                            ReportError("Invalid MFT record magic at index {0} - was ({2},{3},{4},{5}) \"{1}\"", index, magic.Trim('\0'), (int)magic[0], (int)magic[1], (int)magic[2], (int)magic[3]);
                        }
                    }
                    else
                    {
                        FileRecord record = new FileRecord(bytesPerSector);
                        record.FromBytes(recordData, 0);
                        VerifyMftRecord(record, bitmap.IsPresent(index));
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
                File f = new File(_context, fr);
                foreach (var attr in f.AllAttributes)
                {
                    string attrKey = fr.MasterFileTableIndex + ":" + attr.Id;

                    foreach (var range in attr.GetClusters())
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
                                ReportError("Two attributes referencing cluster {0} (0x{0:X16}) - {1} and {2} (as MftIndex:AttrId)", cluster, existingKey, attrKey);
                            }
                        }
                    }
                }
            }
        }

        private void VerifyMftRecord(FileRecord record, bool presentInBitmap)
        {
            bool inUse = (record.Flags & FileRecordFlags.InUse) != 0;
            if (inUse != presentInBitmap)
            {
                ReportError("MFT bitmap and record in-use flag don't agree.  Mft={0}, Record={1}", presentInBitmap ? "InUse" : "Free", inUse ? "InUse" : "Free");
            }
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


        private void Abort()
        {
            throw new AbortException();
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

        private sealed class NullTextWriter : TextWriter
        {
            public NullTextWriter()
                : base(CultureInfo.InvariantCulture)
            {
            }

            public override Encoding Encoding
            {
                get { return Encoding.Unicode; }
            }
        }

        private sealed class AbortException : InvalidFileSystemException
        {
        }
    }
}

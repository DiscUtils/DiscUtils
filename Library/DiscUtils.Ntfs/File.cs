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
using System.IO;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class File
    {
        private readonly List<NtfsAttribute> _attributes;
        protected INtfsContext _context;

        private readonly ObjectCache<string, Index> _indexCache;

        private readonly MasterFileTable _mft;
        private readonly List<FileRecord> _records;

        public File(INtfsContext context, FileRecord baseRecord)
        {
            _context = context;
            _mft = _context.Mft;
            _records = new List<FileRecord>();
            _records.Add(baseRecord);
            _indexCache = new ObjectCache<string, Index>();
            _attributes = new List<NtfsAttribute>();

            LoadAttributes();
        }

        /// <summary>
        /// Gets an enumeration of all the attributes.
        /// </summary>
        internal IEnumerable<NtfsAttribute> AllAttributes
        {
            get { return _attributes; }
        }

        public IEnumerable<NtfsStream> AllStreams
        {
            get
            {
                foreach (NtfsAttribute attr in _attributes)
                {
                    yield return new NtfsStream(this, attr);
                }
            }
        }

        public string BestName
        {
            get
            {
                NtfsAttribute[] attrs = GetAttributes(AttributeType.FileName);

                string bestName = null;

                if (attrs != null && attrs.Length != 0)
                {
                    bestName = attrs[0].ToString();

                    for (int i = 1; i < attrs.Length; ++i)
                    {
                        string name = attrs[i].ToString();

                        if (Utilities.Is8Dot3(bestName))
                        {
                            bestName = name;
                        }
                    }
                }

                return bestName;
            }
        }

        internal INtfsContext Context
        {
            get { return _context; }
        }

        public DirectoryEntry DirectoryEntry
        {
            get
            {
                if (_context.GetDirectoryByRef == null)
                {
                    return null;
                }

                NtfsStream stream = GetStream(AttributeType.FileName, null);
                if (stream == null)
                {
                    return null;
                }

                FileNameRecord record = stream.GetContent<FileNameRecord>();

                // Root dir is stored without root directory flag set in FileNameRecord, simulate it.
                if (_records[0].MasterFileTableIndex == MasterFileTable.RootDirIndex)
                {
                    record.Flags |= FileAttributeFlags.Directory;
                }

                return new DirectoryEntry(_context.GetDirectoryByRef(record.ParentDirectory), MftReference, record);
            }
        }

        public ushort HardLinkCount
        {
            get { return _records[0].HardLinkCount; }
            set { _records[0].HardLinkCount = value; }
        }

        public bool HasWin32OrDosName
        {
            get
            {
                foreach (StructuredNtfsAttribute<FileNameRecord> attr in GetAttributes(AttributeType.FileName))
                {
                    FileNameRecord fnr = attr.Content;
                    if (fnr.FileNameNamespace != FileNameNamespace.Posix)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public uint IndexInMft
        {
            get { return _records[0].MasterFileTableIndex; }
        }

        public bool IsDirectory
        {
            get { return (_records[0].Flags & FileRecordFlags.IsDirectory) != 0; }
        }

        public uint MaxMftRecordSize
        {
            get { return _records[0].AllocatedSize; }
        }

        public bool MftRecordIsDirty { get; private set; }

        public FileRecordReference MftReference
        {
            get { return _records[0].Reference; }
        }

        public List<string> Names
        {
            get
            {
                List<string> result = new List<string>();

                if (IndexInMft == MasterFileTable.RootDirIndex)
                {
                    result.Add(string.Empty);
                }
                else
                {
                    foreach (StructuredNtfsAttribute<FileNameRecord> attr in GetAttributes(AttributeType.FileName))
                    {
                        string name = attr.Content.FileName;

                        Directory parentDir = _context.GetDirectoryByRef(attr.Content.ParentDirectory);
                        if (parentDir != null)
                        {
                            foreach (string dirName in parentDir.Names)
                            {
                                result.Add(Utilities.CombinePaths(dirName, name));
                            }
                        }
                    }
                }

                return result;
            }
        }

        public StandardInformation StandardInformation
        {
            get { return GetStream(AttributeType.StandardInformation, null).GetContent<StandardInformation>(); }
        }

        public static File CreateNew(INtfsContext context, FileAttributeFlags dirFlags)
        {
            return CreateNew(context, FileRecordFlags.None, dirFlags);
        }

        public static File CreateNew(INtfsContext context, FileRecordFlags flags, FileAttributeFlags dirFlags)
        {
            File newFile = context.AllocateFile(flags);

            FileAttributeFlags fileFlags =
                FileAttributeFlags.Archive
                | FileRecord.ConvertFlags(flags)
                | (dirFlags & FileAttributeFlags.Compressed);

            AttributeFlags dataAttrFlags = AttributeFlags.None;
            if ((dirFlags & FileAttributeFlags.Compressed) != 0)
            {
                dataAttrFlags |= AttributeFlags.Compressed;
            }

            StandardInformation.InitializeNewFile(newFile, fileFlags);

            if (context.ObjectIds != null)
            {
                Guid newId = CreateNewGuid(context);
                NtfsStream stream = newFile.CreateStream(AttributeType.ObjectId, null);
                ObjectId objId = new ObjectId();
                objId.Id = newId;
                stream.SetContent(objId);
                context.ObjectIds.Add(newId, newFile.MftReference, newId, Guid.Empty, Guid.Empty);
            }

            newFile.CreateAttribute(AttributeType.Data, dataAttrFlags);

            newFile.UpdateRecordInMft();

            return newFile;
        }

        public int MftRecordFreeSpace(AttributeType attrType, string attrName)
        {
            foreach (FileRecord record in _records)
            {
                if (record.GetAttribute(attrType, attrName) != null)
                {
                    return _mft.RecordSize - record.Size;
                }
            }

            throw new IOException("Attempt to determine free space for non-existent attribute");
        }

        public void Modified()
        {
            DateTime now = DateTime.UtcNow;

            NtfsStream siStream = GetStream(AttributeType.StandardInformation, null);
            StandardInformation si = siStream.GetContent<StandardInformation>();
            si.LastAccessTime = now;
            si.ModificationTime = now;
            siStream.SetContent(si);

            MarkMftRecordDirty();
        }

        public void Accessed()
        {
            DateTime now = DateTime.UtcNow;

            NtfsStream siStream = GetStream(AttributeType.StandardInformation, null);
            StandardInformation si = siStream.GetContent<StandardInformation>();
            si.LastAccessTime = now;
            siStream.SetContent(si);

            MarkMftRecordDirty();
        }

        public void MarkMftRecordDirty()
        {
            MftRecordIsDirty = true;
        }

        public void UpdateRecordInMft()
        {
            if (MftRecordIsDirty)
            {
                if (NtfsTransaction.Current != null)
                {
                    NtfsStream stream = GetStream(AttributeType.StandardInformation, null);
                    StandardInformation si = stream.GetContent<StandardInformation>();
                    si.MftChangedTime = NtfsTransaction.Current.Timestamp;
                    stream.SetContent(si);
                }

                bool fixesApplied = true;
                while (fixesApplied)
                {
                    fixesApplied = false;

                    for (int i = 0; i < _records.Count; ++i)
                    {
                        FileRecord record = _records[i];

                        bool fixedAttribute = true;
                        while (record.Size > _mft.RecordSize && fixedAttribute)
                        {
                            fixedAttribute = false;

                            if (!fixedAttribute && !record.IsMftRecord)
                            {
                                foreach (AttributeRecord attr in record.Attributes)
                                {
                                    if (!attr.IsNonResident &&
                                        !_context.AttributeDefinitions.MustBeResident(attr.AttributeType))
                                    {
                                        MakeAttributeNonResident(
                                            new AttributeReference(record.Reference, attr.AttributeId),
                                            (int)attr.DataLength);
                                        fixedAttribute = true;
                                        break;
                                    }
                                }
                            }

                            if (!fixedAttribute)
                            {
                                foreach (AttributeRecord attr in record.Attributes)
                                {
                                    if (attr.AttributeType == AttributeType.IndexRoot
                                        && ShrinkIndexRoot(attr.Name))
                                    {
                                        fixedAttribute = true;
                                        break;
                                    }
                                }
                            }

                            if (!fixedAttribute)
                            {
                                if (record.Attributes.Count == 1)
                                {
                                    fixedAttribute = SplitAttribute(record);
                                }
                                else
                                {
                                    if (_records.Count == 1)
                                    {
                                        CreateAttributeList();
                                    }

                                    fixedAttribute = ExpelAttribute(record);
                                }
                            }

                            fixesApplied |= fixedAttribute;
                        }
                    }
                }

                MftRecordIsDirty = false;
                foreach (FileRecord record in _records)
                {
                    _mft.WriteRecord(record);
                }
            }
        }

        public Index CreateIndex(string name, AttributeType attrType, AttributeCollationRule collRule)
        {
            Index.Create(attrType, collRule, this, name);
            return GetIndex(name);
        }

        public Index GetIndex(string name)
        {
            Index idx = _indexCache[name];

            if (idx == null)
            {
                idx = new Index(this, name, _context.BiosParameterBlock, _context.UpperCase);
                _indexCache[name] = idx;
            }

            return idx;
        }

        public void Delete()
        {
            if (_records[0].HardLinkCount != 0)
            {
                throw new InvalidOperationException("Attempt to delete in-use file: " + ToString());
            }

            _context.ForgetFile(this);

            NtfsStream objIdStream = GetStream(AttributeType.ObjectId, null);
            if (objIdStream != null)
            {
                ObjectId objId = objIdStream.GetContent<ObjectId>();
                Context.ObjectIds.Remove(objId.Id);
            }

            // Truncate attributes, allowing for truncation silently removing the AttributeList attribute
            // in some cases (large file with all attributes first extent in the first MFT record).  This
            // releases all allocated clusters in most cases.
            List<NtfsAttribute> truncateAttrs = new List<NtfsAttribute>(_attributes.Count);
            foreach (NtfsAttribute attr in _attributes)
            {
                if (attr.Type != AttributeType.AttributeList)
                {
                    truncateAttrs.Add(attr);
                }
            }

            foreach (NtfsAttribute attr in truncateAttrs)
            {
                attr.GetDataBuffer().SetCapacity(0);
            }

            // If the attribute list record remains, free any possible clusters it owns.  We've now freed
            // all clusters.
            NtfsAttribute attrList = GetAttribute(AttributeType.AttributeList, null);
            if (attrList != null)
            {
                attrList.GetDataBuffer().SetCapacity(0);
            }

            // Now go through the MFT records, freeing them up
            foreach (FileRecord mftRecord in _records)
            {
                _context.Mft.RemoveRecord(mftRecord.Reference);
            }

            _attributes.Clear();
            _records.Clear();
        }

        public bool StreamExists(AttributeType attrType, string name)
        {
            return GetStream(attrType, name) != null;
        }

        public NtfsStream GetStream(AttributeType attrType, string name)
        {
            foreach (NtfsStream stream in GetStreams(attrType, name))
            {
                return stream;
            }

            return null;
        }

        public IEnumerable<NtfsStream> GetStreams(AttributeType attrType, string name)
        {
            foreach (NtfsAttribute attr in _attributes)
            {
                if (attr.Type == attrType && attr.Name == name)
                {
                    yield return new NtfsStream(this, attr);
                }
            }
        }

        public NtfsStream CreateStream(AttributeType attrType, string name)
        {
            return new NtfsStream(this, CreateAttribute(attrType, name, AttributeFlags.None));
        }

        public NtfsStream CreateStream(AttributeType attrType, string name, long firstCluster, ulong numClusters,
                                       uint bytesPerCluster)
        {
            return new NtfsStream(this,
                CreateAttribute(attrType, name, AttributeFlags.None, firstCluster, numClusters, bytesPerCluster));
        }

        public SparseStream OpenStream(AttributeType attrType, string name, FileAccess access)
        {
            NtfsAttribute attr = GetAttribute(attrType, name);
            if (attr != null)
            {
                return new FileStream(this, attr, access);
            }

            return null;
        }

        public void RemoveStream(NtfsStream stream)
        {
            RemoveAttribute(stream.Attribute);
        }

        public FileNameRecord GetFileNameRecord(string name, bool freshened)
        {
            NtfsAttribute[] attrs = GetAttributes(AttributeType.FileName);
            StructuredNtfsAttribute<FileNameRecord> attr = null;
            if (string.IsNullOrEmpty(name))
            {
                if (attrs.Length != 0)
                {
                    attr = (StructuredNtfsAttribute<FileNameRecord>)attrs[0];
                }
            }
            else
            {
                foreach (StructuredNtfsAttribute<FileNameRecord> a in attrs)
                {
                    if (_context.UpperCase.Compare(a.Content.FileName, name) == 0)
                    {
                        attr = a;
                    }
                }

                if (attr == null)
                {
                    throw new FileNotFoundException("File name not found on file", name);
                }
            }

            FileNameRecord fnr = attr == null ? new FileNameRecord() : new FileNameRecord(attr.Content);

            if (freshened)
            {
                FreshenFileName(fnr, false);
            }

            return fnr;
        }

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE (" + ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + _records[0].MasterFileTableIndex);

            _records[0].Dump(writer, indent + "  ");

            foreach (AttributeRecord attrRec in _records[0].Attributes)
            {
                NtfsAttribute.FromRecord(this, MftReference, attrRec).Dump(writer, indent + "  ");
            }
        }

        public override string ToString()
        {
            string bestName = BestName;
            if (bestName == null)
            {
                return "?????";
            }
            return bestName;
        }

        internal void RemoveAttributeExtents(NtfsAttribute attr)
        {
            attr.GetDataBuffer().SetCapacity(0);

            foreach (AttributeReference extentRef in attr.Extents.Keys)
            {
                RemoveAttributeExtent(extentRef);
            }
        }

        internal void RemoveAttributeExtent(AttributeReference extentRef)
        {
            FileRecord fileRec = GetFileRecord(extentRef.File);
            if (fileRec != null)
            {
                fileRec.RemoveAttribute(extentRef.AttributeId);

                // Remove empty non-primary MFT records
                if (fileRec.Attributes.Count == 0 && fileRec.BaseFile.Value != 0)
                {
                    RemoveFileRecord(extentRef.File);
                }
            }
        }

        /// <summary>
        /// Gets an attribute by reference.
        /// </summary>
        /// <param name="attrRef">Reference to the attribute.</param>
        /// <returns>The attribute.</returns>
        internal NtfsAttribute GetAttribute(AttributeReference attrRef)
        {
            foreach (NtfsAttribute attr in _attributes)
            {
                if (attr.Reference.Equals(attrRef))
                {
                    return attr;
                }
            }

            return null;
        }

        /// <summary>
        ///  Gets the first (if more than one) instance of a named attribute.
        /// </summary>
        /// <param name="type">The attribute type.</param>
        /// <param name="name">The attribute's name.</param>
        /// <returns>The attribute of <c>null</c>.</returns>
        internal NtfsAttribute GetAttribute(AttributeType type, string name)
        {
            foreach (NtfsAttribute attr in _attributes)
            {
                if (attr.PrimaryRecord.AttributeType == type && attr.Name == name)
                {
                    return attr;
                }
            }

            return null;
        }

        /// <summary>
        ///  Gets all instances of an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type.</param>
        /// <returns>The attributes.</returns>
        internal NtfsAttribute[] GetAttributes(AttributeType type)
        {
            List<NtfsAttribute> matches = new List<NtfsAttribute>();

            foreach (NtfsAttribute attr in _attributes)
            {
                if (attr.PrimaryRecord.AttributeType == type && string.IsNullOrEmpty(attr.Name))
                {
                    matches.Add(attr);
                }
            }

            return matches.ToArray();
        }

        internal void MakeAttributeNonResident(AttributeReference attrRef, int maxData)
        {
            NtfsAttribute attr = GetAttribute(attrRef);
            if (attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already non-resident");
            }

            ushort id = _records[0].CreateNonResidentAttribute(attr.Type, attr.Name, attr.Flags);
            AttributeRecord newAttrRecord = _records[0].GetAttribute(id);

            IBuffer attrBuffer = attr.GetDataBuffer();
            byte[] tempData = StreamUtilities.ReadExact(attrBuffer, 0, (int)Math.Min(maxData, attrBuffer.Capacity));

            RemoveAttributeExtents(attr);
            attr.SetExtent(_records[0].Reference, newAttrRecord);

            attr.GetDataBuffer().Write(0, tempData, 0, tempData.Length);

            UpdateAttributeList();
        }

        internal void FreshenFileName(FileNameRecord fileName, bool updateMftRecord)
        {
            //
            // Freshen the record from the definitive info in the other attributes
            //
            StandardInformation si = StandardInformation;
            NtfsAttribute anonDataAttr = GetAttribute(AttributeType.Data, null);

            fileName.CreationTime = si.CreationTime;
            fileName.ModificationTime = si.ModificationTime;
            fileName.MftChangedTime = si.MftChangedTime;
            fileName.LastAccessTime = si.LastAccessTime;
            fileName.Flags = si.FileAttributes;

            if (MftRecordIsDirty && NtfsTransaction.Current != null)
            {
                fileName.MftChangedTime = NtfsTransaction.Current.Timestamp;
            }

            // Directories don't have directory flag set in StandardInformation, so set from MFT record
            if ((_records[0].Flags & FileRecordFlags.IsDirectory) != 0)
            {
                fileName.Flags |= FileAttributeFlags.Directory;
            }

            if (anonDataAttr != null)
            {
                fileName.RealSize = (ulong)anonDataAttr.PrimaryRecord.DataLength;
                fileName.AllocatedSize = (ulong)anonDataAttr.PrimaryRecord.AllocatedLength;
            }

            if (updateMftRecord)
            {
                foreach (NtfsStream stream in GetStreams(AttributeType.FileName, null))
                {
                    FileNameRecord fnr = stream.GetContent<FileNameRecord>();
                    if (fnr.Equals(fileName))
                    {
                        fnr = new FileNameRecord(fileName);
                        fnr.Flags &= ~FileAttributeFlags.ReparsePoint;
                        stream.SetContent(fnr);
                    }
                }
            }
        }

        internal long GetAttributeOffset(AttributeReference attrRef)
        {
            long recordOffset = _mft.GetRecordOffset(attrRef.File);

            FileRecord frs = GetFileRecord(attrRef.File);
            return recordOffset + frs.GetAttributeOffset(attrRef.AttributeId);
        }

        private static Guid CreateNewGuid(INtfsContext context)
        {
            Random rng = context.Options.RandomNumberGenerator;
            if (rng != null)
            {
                byte[] buffer = new byte[16];
                rng.NextBytes(buffer);
                return new Guid(buffer);
            }
            return Guid.NewGuid();
        }

        private void LoadAttributes()
        {
            Dictionary<long, FileRecord> extraFileRecords = new Dictionary<long, FileRecord>();

            AttributeRecord attrListRec = _records[0].GetAttribute(AttributeType.AttributeList);
            if (attrListRec != null)
            {
                NtfsAttribute lastAttr = null;

                StructuredNtfsAttribute<AttributeList> attrListAttr =
                    (StructuredNtfsAttribute<AttributeList>)NtfsAttribute.FromRecord(this, MftReference, attrListRec);
                AttributeList attrList = attrListAttr.Content;
                _attributes.Add(attrListAttr);

                foreach (AttributeListRecord record in attrList)
                {
                    FileRecord attrFileRecord = _records[0];
                    if (record.BaseFileReference.MftIndex != _records[0].MasterFileTableIndex)
                    {
                        if (!extraFileRecords.TryGetValue(record.BaseFileReference.MftIndex, out attrFileRecord))
                        {
                            attrFileRecord = _context.Mft.GetRecord(record.BaseFileReference);
                            if (attrFileRecord != null)
                            {
                                extraFileRecords[attrFileRecord.MasterFileTableIndex] = attrFileRecord;
                            }
                        }
                    }

                    if (attrFileRecord != null)
                    {
                        AttributeRecord attrRec = attrFileRecord.GetAttribute(record.AttributeId);

                        if (attrRec != null)
                        {
                            if (record.StartVcn == 0)
                            {
                                lastAttr = NtfsAttribute.FromRecord(this, record.BaseFileReference, attrRec);
                                _attributes.Add(lastAttr);
                            }
                            else
                            {
                                lastAttr.AddExtent(record.BaseFileReference, attrRec);
                            }
                        }
                    }
                }

                foreach (KeyValuePair<long, FileRecord> extraFileRecord in extraFileRecords)
                {
                    _records.Add(extraFileRecord.Value);
                }
            }
            else
            {
                foreach (AttributeRecord record in _records[0].Attributes)
                {
                    _attributes.Add(NtfsAttribute.FromRecord(this, MftReference, record));
                }
            }
        }

        private bool SplitAttribute(FileRecord record)
        {
            if (record.Attributes.Count != 1)
            {
                throw new InvalidOperationException(
                    "Attempting to split attribute in MFT record containing multiple attributes");
            }

            return SplitAttribute(record, (NonResidentAttributeRecord)record.FirstAttribute, false);
        }

        private bool SplitAttribute(FileRecord record, NonResidentAttributeRecord targetAttr, bool atStart)
        {
            if (targetAttr.DataRuns.Count <= 1)
            {
                return false;
            }

            int splitIndex = 1;
            if (!atStart)
            {
                List<DataRun> runs = targetAttr.DataRuns;

                splitIndex = runs.Count - 1;
                int saved = runs[splitIndex].Size;
                while (splitIndex > 1 && record.Size - saved > record.AllocatedSize)
                {
                    --splitIndex;
                    saved += runs[splitIndex].Size;
                }
            }

            AttributeRecord newAttr = targetAttr.Split(splitIndex);

            // Find a home for the new attribute record
            FileRecord newAttrHome = null;
            foreach (FileRecord targetRecord in _records)
            {
                if (!targetRecord.IsMftRecord && _mft.RecordSize - targetRecord.Size >= newAttr.Size)
                {
                    targetRecord.AddAttribute(newAttr);
                    newAttrHome = targetRecord;
                }
            }

            if (newAttrHome == null)
            {
                newAttrHome = _mft.AllocateRecord(_records[0].Flags & ~FileRecordFlags.InUse, record.IsMftRecord);
                newAttrHome.BaseFile = record.BaseFile.IsNull ? record.Reference : record.BaseFile;
                _records.Add(newAttrHome);
                newAttrHome.AddAttribute(newAttr);
            }

            // Add the new attribute record as an extent on the attribute it split from
            bool added = false;
            foreach (NtfsAttribute attr in _attributes)
            {
                foreach (KeyValuePair<AttributeReference, AttributeRecord> existingRecord in attr.Extents)
                {
                    if (existingRecord.Key.File == record.Reference &&
                        existingRecord.Key.AttributeId == targetAttr.AttributeId)
                    {
                        attr.AddExtent(newAttrHome.Reference, newAttr);
                        added = true;
                        break;
                    }
                }

                if (added)
                {
                    break;
                }
            }

            UpdateAttributeList();

            return true;
        }

        private bool ExpelAttribute(FileRecord record)
        {
            if (record.MasterFileTableIndex == MasterFileTable.MftIndex)
            {
                // Special case for MFT - can't fully expel attributes, instead split most of the data runs off.
                List<AttributeRecord> attrs = record.Attributes;
                for (int i = attrs.Count - 1; i >= 0; --i)
                {
                    AttributeRecord attr = attrs[i];
                    if (attr.AttributeType == AttributeType.Data)
                    {
                        if (SplitAttribute(record, (NonResidentAttributeRecord)attr, true))
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                List<AttributeRecord> attrs = record.Attributes;
                for (int i = attrs.Count - 1; i >= 0; --i)
                {
                    AttributeRecord attr = attrs[i];
                    if (attr.AttributeType > AttributeType.AttributeList)
                    {
                        foreach (FileRecord targetRecord in _records)
                        {
                            if (_mft.RecordSize - targetRecord.Size >= attr.Size)
                            {
                                MoveAttribute(record, attr, targetRecord);
                                return true;
                            }
                        }

                        FileRecord newFileRecord = _mft.AllocateRecord(FileRecordFlags.None, record.IsMftRecord);
                        newFileRecord.BaseFile = record.Reference;
                        _records.Add(newFileRecord);
                        MoveAttribute(record, attr, newFileRecord);
                        return true;
                    }
                }
            }

            return false;
        }

        private void MoveAttribute(FileRecord record, AttributeRecord attrRec, FileRecord targetRecord)
        {
            AttributeReference oldRef = new AttributeReference(record.Reference, attrRec.AttributeId);

            record.RemoveAttribute(attrRec.AttributeId);
            targetRecord.AddAttribute(attrRec);

            AttributeReference newRef = new AttributeReference(targetRecord.Reference, attrRec.AttributeId);

            foreach (NtfsAttribute attr in _attributes)
            {
                attr.ReplaceExtent(oldRef, newRef, attrRec);
            }

            UpdateAttributeList();
        }

        private void CreateAttributeList()
        {
            ushort id = _records[0].CreateAttribute(AttributeType.AttributeList, null, false, AttributeFlags.None);

            StructuredNtfsAttribute<AttributeList> newAttr =
                (StructuredNtfsAttribute<AttributeList>)
                NtfsAttribute.FromRecord(this, MftReference, _records[0].GetAttribute(id));
            _attributes.Add(newAttr);
            UpdateAttributeList();
        }

        private void UpdateAttributeList()
        {
            if (_records.Count > 1)
            {
                AttributeList attrList = new AttributeList();

                foreach (NtfsAttribute attr in _attributes)
                {
                    if (attr.Type != AttributeType.AttributeList)
                    {
                        foreach (KeyValuePair<AttributeReference, AttributeRecord> extent in attr.Extents)
                        {
                            attrList.Add(AttributeListRecord.FromAttribute(extent.Value, extent.Key.File));
                        }
                    }
                }

                StructuredNtfsAttribute<AttributeList> alAttr;
                alAttr = (StructuredNtfsAttribute<AttributeList>)GetAttribute(AttributeType.AttributeList, null);
                alAttr.Content = attrList;
                alAttr.Save();
            }
        }

        /// <summary>
        /// Creates a new unnamed attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute.</param>
        /// <param name="flags">The flags of the new attribute.</param>
        /// <returns>The new attribute.</returns>
        private NtfsAttribute CreateAttribute(AttributeType type, AttributeFlags flags)
        {
            return CreateAttribute(type, null, flags);
        }

        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute.</param>
        /// <param name="name">The name of the new attribute.</param>
        /// <param name="flags">The flags of the new attribute.</param>
        /// <returns>The new attribute.</returns>
        private NtfsAttribute CreateAttribute(AttributeType type, string name, AttributeFlags flags)
        {
            bool indexed = _context.AttributeDefinitions.IsIndexed(type);
            ushort id = _records[0].CreateAttribute(type, name, indexed, flags);

            AttributeRecord newAttrRecord = _records[0].GetAttribute(id);
            NtfsAttribute newAttr = NtfsAttribute.FromRecord(this, MftReference, newAttrRecord);

            _attributes.Add(newAttr);
            UpdateAttributeList();

            MarkMftRecordDirty();

            return newAttr;
        }

        /// <summary>
        /// Creates a new attribute at a fixed cluster.
        /// </summary>
        /// <param name="type">The type of the new attribute.</param>
        /// <param name="name">The name of the new attribute.</param>
        /// <param name="flags">The flags of the new attribute.</param>
        /// <param name="firstCluster">The first cluster to assign to the attribute.</param>
        /// <param name="numClusters">The number of sequential clusters to assign to the attribute.</param>
        /// <param name="bytesPerCluster">The number of bytes in each cluster.</param>
        /// <returns>The new attribute.</returns>
        private NtfsAttribute CreateAttribute(AttributeType type, string name, AttributeFlags flags, long firstCluster,
                                              ulong numClusters, uint bytesPerCluster)
        {
            bool indexed = _context.AttributeDefinitions.IsIndexed(type);
            ushort id = _records[0].CreateNonResidentAttribute(type, name, flags, firstCluster, numClusters,
                bytesPerCluster);

            AttributeRecord newAttrRecord = _records[0].GetAttribute(id);
            NtfsAttribute newAttr = NtfsAttribute.FromRecord(this, MftReference, newAttrRecord);

            _attributes.Add(newAttr);
            UpdateAttributeList();
            MarkMftRecordDirty();
            return newAttr;
        }

        private void RemoveAttribute(NtfsAttribute attr)
        {
            if (attr != null)
            {
                if (attr.PrimaryRecord.AttributeType == AttributeType.IndexRoot)
                {
                    _indexCache.Remove(attr.PrimaryRecord.Name);
                }

                RemoveAttributeExtents(attr);

                _attributes.Remove(attr);

                UpdateAttributeList();
            }
        }

        private bool ShrinkIndexRoot(string indexName)
        {
            NtfsAttribute attr = GetAttribute(AttributeType.IndexRoot, indexName);

            // Nothing to win, can't make IndexRoot smaller than this
            // 8 = min size of entry that points to IndexAllocation...
            if (attr.Length <= IndexRoot.HeaderOffset + IndexHeader.Size + 8)
            {
                return false;
            }

            Index idx = GetIndex(indexName);
            return idx.ShrinkRoot();
        }

        private void MakeAttributeResident(AttributeReference attrRef, int maxData)
        {
            NtfsAttribute attr = GetAttribute(attrRef);
            if (!attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already resident");
            }

            ushort id = _records[0].CreateAttribute(attr.Type, attr.Name,
                _context.AttributeDefinitions.IsIndexed(attr.Type), attr.Flags);
            AttributeRecord newAttrRecord = _records[0].GetAttribute(id);

            IBuffer attrBuffer = attr.GetDataBuffer();
            byte[] tempData = StreamUtilities.ReadExact(attrBuffer, 0, (int)Math.Min(maxData, attrBuffer.Capacity));

            RemoveAttributeExtents(attr);
            attr.SetExtent(_records[0].Reference, newAttrRecord);

            attr.GetDataBuffer().Write(0, tempData, 0, tempData.Length);

            UpdateAttributeList();
        }

        private FileRecord GetFileRecord(FileRecordReference fileReference)
        {
            foreach (FileRecord record in _records)
            {
                if (record.MasterFileTableIndex == fileReference.MftIndex)
                {
                    return record;
                }
            }

            return null;
        }

        private void RemoveFileRecord(FileRecordReference fileReference)
        {
            for (int i = 0; i < _records.Count; ++i)
            {
                if (_records[i].MasterFileTableIndex == fileReference.MftIndex)
                {
                    FileRecord record = _records[i];

                    if (record.Attributes.Count > 0)
                    {
                        throw new IOException("Attempting to remove non-empty MFT record");
                    }

                    _context.Mft.RemoveRecord(fileReference);
                    _records.Remove(record);

                    if (_records.Count == 1)
                    {
                        NtfsAttribute attrListAttr = GetAttribute(AttributeType.AttributeList, null);
                        if (attrListAttr != null)
                        {
                            RemoveAttribute(attrListAttr);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Wrapper for Resident/Non-Resident attribute streams, that remains valid
        /// despite the attribute oscillating between resident and not.
        /// </summary>
        private class FileStream : SparseStream
        {
            private readonly NtfsAttribute _attr;
            private readonly File _file;
            private readonly SparseStream _wrapped;

            public FileStream(File file, NtfsAttribute attr, FileAccess access)
            {
                _file = file;
                _attr = attr;
                _wrapped = attr.Open(access);
            }

            public override bool CanRead
            {
                get { return _wrapped.CanRead; }
            }

            public override bool CanSeek
            {
                get { return _wrapped.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return _wrapped.CanWrite; }
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get { return _wrapped.Extents; }
            }

            public override long Length
            {
                get { return _wrapped.Length; }
            }

            public override long Position
            {
                get { return _wrapped.Position; }

                set { _wrapped.Position = value; }
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                _wrapped.Dispose();
            }

            public override void Flush()
            {
                _wrapped.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _wrapped.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _wrapped.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                ChangeAttributeResidencyByLength(value);
                _wrapped.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (_wrapped.Position + count > Length)
                {
                    ChangeAttributeResidencyByLength(_wrapped.Position + count);
                }

                _wrapped.Write(buffer, offset, count);
            }

            public override void Clear(int count)
            {
                if (_wrapped.Position + count > Length)
                {
                    ChangeAttributeResidencyByLength(_wrapped.Position + count);
                }

                _wrapped.Clear(count);
            }

            public override string ToString()
            {
                return _file + ".attr[" + _attr.Id + "]";
            }

            /// <summary>
            /// Change attribute residency if it gets too big (or small).
            /// </summary>
            /// <param name="value">The new (anticipated) length of the stream.</param>
            /// <remarks>Has hysteresis - the decision is based on the input and the current
            /// state, not the current state alone.</remarks>
            private void ChangeAttributeResidencyByLength(long value)
            {
                // This is a bit of a hack - but it's really important the bitmap file remains non-resident
                if (_file._records[0].MasterFileTableIndex == MasterFileTable.BitmapIndex)
                {
                    return;
                }

                if (!_attr.IsNonResident && value >= _file.MaxMftRecordSize)
                {
                    _file.MakeAttributeNonResident(_attr.Reference, (int)Math.Min(value, _wrapped.Length));
                }
                else if (_attr.IsNonResident && value <= _file.MaxMftRecordSize / 4)
                {
                    // Use of 1/4 of record size here is just a heuristic - the important thing is not to end up with
                    // zero-length non-resident attributes
                    _file.MakeAttributeResident(_attr.Reference, (int)value);
                }
            }
        }
    }
}
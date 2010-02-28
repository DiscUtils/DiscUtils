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
using System.Globalization;

namespace DiscUtils.Ntfs
{
    internal class File
    {
        protected INtfsContext _context;

        private MasterFileTable _mft;
        private FileRecord _baseRecord;
        private ObjectCache<string, Index> _indexCache;
        private List<NtfsAttribute> _attributes;

        private bool _baseRecordDirty;

        public File(INtfsContext context, FileRecord baseRecord)
        {
            _context = context;
            _mft = _context.Mft;
            _baseRecord = baseRecord;
            _indexCache = new ObjectCache<string, Index>();
            _attributes = new List<NtfsAttribute>();

            LoadAttributes();
        }

        public uint IndexInMft
        {
            get { return _baseRecord.MasterFileTableIndex; }
        }

        public uint MaxMftRecordSize
        {
            get { return _baseRecord.AllocatedSize; }
        }

        public FileRecordReference MftReference
        {
            get { return new FileRecordReference(_baseRecord.MasterFileTableIndex, _baseRecord.SequenceNumber); }
        }

        public int MftRecordFreeSpace
        {
            get { return _mft.RecordSize - _baseRecord.Size; }
        }

        public List<string> Names
        {
            get
            {
                List<string> result = new List<string>();

                if (IndexInMft == MasterFileTable.RootDirIndex)
                {
                    result.Add("");
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
                                result.Add(Path.Combine(dirName, name));
                            }
                        }
                    }
                }

                return result;
            }
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
            _baseRecordDirty = true;
        }

        public bool MftRecordIsDirty
        {
            get
            {
                return _baseRecordDirty;
            }
        }

        public void UpdateRecordInMft()
        {
            if(_baseRecordDirty)
            {
                if (NtfsTransaction.Current != null)
                {
                    NtfsStream stream = GetStream(AttributeType.StandardInformation, null);
                    StandardInformation si = stream.GetContent<StandardInformation>();
                    si.MftChangedTime = NtfsTransaction.Current.Timestamp;
                    stream.SetContent(si);
                }

                // Make attributes non-resident until the data in the record fits, start with DATA attributes
                // by default, then kick other 'can-be' attributes out, then try to defrag any non-resident attributes
                // then finally move indexes.
                bool fixedAttribute = true;
                while (_baseRecord.Size > _mft.RecordSize && fixedAttribute)
                {
                    fixedAttribute = false;

                    foreach (var attr in _baseRecord.Attributes)
                    {
                        if (!attr.IsNonResident && attr.AttributeType == AttributeType.Data)
                        {
                            MakeAttributeNonResident(new AttributeReference(MftReference, attr.AttributeId), (int)attr.DataLength);
                            fixedAttribute = true;
                            break;
                        }
                    }

                    if (!fixedAttribute)
                    {
                        foreach (var attr in _baseRecord.Attributes)
                        {
                            if (!attr.IsNonResident && !_context.AttributeDefinitions.MustBeResident(attr.AttributeType))
                            {
                                MakeAttributeNonResident(new AttributeReference(MftReference, attr.AttributeId), (int)attr.DataLength);
                                fixedAttribute = true;
                                break;
                            }
                        }
                    }

                    if (!fixedAttribute)
                    {
                        foreach (var attr in _baseRecord.Attributes)
                        {
                            if (attr.AttributeType == AttributeType.IndexRoot
                                && ShrinkIndexRoot(attr.Name))
                            {
                                fixedAttribute = true;
                                break;
                            }
                        }
                    }

                }

                // Still too large?  Error.
                if (_baseRecord.Size > _mft.RecordSize)
                {
                    throw new NotSupportedException("Spanning over multiple FileRecord entries - TBD");
                }

                _baseRecordDirty = false;
                _mft.WriteRecord(_baseRecord);
            }
        }

        #region Indexes
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

        public ushort HardLinkCount
        {
            get { return _baseRecord.HardLinkCount; }
            set { _baseRecord.HardLinkCount = value; }
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
        #endregion

        #region Attributes

        /// <summary>
        /// Creates a new unnamed attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        private NtfsAttribute CreateAttribute(AttributeType type)
        {
            return CreateAttribute(type, null);
        }

        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        /// <param name="name">The name of the new attribute</param>
        private NtfsAttribute CreateAttribute(AttributeType type, string name)
        {
            bool indexed = _context.AttributeDefinitions.IsIndexed(type);
            ushort id = _baseRecord.CreateAttribute(type, name, indexed);

            NtfsAttribute newAttr = NtfsAttribute.FromRecord(this, MftReference, _baseRecord.GetAttribute(id));

            _attributes.Add(newAttr);
            MarkMftRecordDirty();
            return newAttr;
        }

        /// <summary>
        /// Creates a new attribute at a fixed cluster.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        /// <param name="name">The name of the new attribute</param>
        /// <param name="firstCluster">The first cluster to assign to the attribute</param>
        /// <param name="numClusters">The number of sequential clusters to assign to the attribute</param>
        /// <param name="bytesPerCluster">The number of bytes in each cluster</param>
        private NtfsAttribute CreateAttribute(AttributeType type, string name, long firstCluster, ulong numClusters, uint bytesPerCluster)
        {
            bool indexed = _context.AttributeDefinitions.IsIndexed(type);
            ushort id = _baseRecord.CreateAttribute(type, name, firstCluster, numClusters, bytesPerCluster);

            NtfsAttribute newAttr = NtfsAttribute.FromRecord(this, MftReference, _baseRecord.GetAttribute(id));

            _attributes.Add(newAttr);
            MarkMftRecordDirty();
            return newAttr;
        }

        private void RemoveAttribute(NtfsAttribute attr)
        {
            if (attr != null)
            {
                if (attr.Record.AttributeType == AttributeType.IndexRoot)
                {
                    _indexCache.Remove(attr.Record.Name);
                }

                attr.GetDataBuffer().SetCapacity(0);

                foreach(var extentRef in attr.ExtentRefs)
                {
                    FileRecord fileRec = _context.Mft.GetRecord(extentRef.File);
                    if (fileRec != null)
                    {
                        fileRec.RemoveAttribute(extentRef.AttributeId);
                    }

                    if (fileRec.Attributes.Count == 0)
                    {
                        _context.Mft.RemoveRecord(extentRef.File);
                    }
                }

                // TODO: remove entry from attribute list - also write new extension MFT records
            }
        }

        /// <summary>
        /// Gets an attribute by reference.
        /// </summary>
        /// <param name="attrRef">Reference to the attribute</param>
        /// <returns>The attribute</returns>
        internal NtfsAttribute GetAttribute(AttributeReference attrRef)
        {
            foreach (var attr in _attributes)
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
        /// <param name="type">The attribute type</param>
        /// <param name="name">The attribute's name</param>
        /// <returns>The attribute of <c>null</c>.</returns>
        internal NtfsAttribute GetAttribute(AttributeType type, string name)
        {
            foreach (var attr in _attributes)
            {
                if (attr.Record.AttributeType == type && attr.Name == name)
                {
                    return attr;
                }
            }
            return null;
        }

        private void LoadAttributes()
        {
            AttributeRecord attrListRec = _baseRecord.GetAttribute(AttributeType.AttributeList);
            if (attrListRec != null)
            {
                NtfsAttribute lastAttr = null;

                StructuredNtfsAttribute<AttributeList> attrList = (StructuredNtfsAttribute<AttributeList>)NtfsAttribute.FromRecord(this, MftReference, attrListRec);
                foreach (var record in attrList.Content)
                {
                    FileRecord attrFileRecord = _baseRecord;
                    if (record.BaseFileReference.MftIndex != _baseRecord.MasterFileTableIndex)
                    {
                        attrFileRecord = _context.Mft.GetRecord(record.BaseFileReference);
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
            }
            else
            {
                foreach (var record in _baseRecord.Attributes)
                {
                    _attributes.Add(NtfsAttribute.FromRecord(this, MftReference, record));
                }
            }
        }

        /// <summary>
        /// Enumerates through all attributes.
        /// </summary>
        internal IEnumerable<NtfsAttribute> AllAttributes
        {
            get { return _attributes; }
        }

        /// <summary>
        ///  Gets all instances of an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attributes.</returns>
        private NtfsAttribute[] GetAttributes(AttributeType type)
        {
            List<NtfsAttribute> matches = new List<NtfsAttribute>();

            foreach (var attr in _attributes)
            {
                if (attr.Record.AttributeType == type && string.IsNullOrEmpty(attr.Name))
                {
                    matches.Add(attr);
                }
            }

            return matches.ToArray();
        }

        internal void MakeAttributeNonResident(AttributeReference attrRef, int maxData)
        {
            if (attrRef.File != MftReference)
            {
                throw new NotImplementedException("Changing residency of attributes in extended file records");
            }

            NtfsAttribute attr = GetAttribute(attrRef);
            if (attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already non-resident");
            }

            attr.SetNonResident(true, maxData);
            _baseRecord.SetAttribute(attr.Record);
        }

        internal void MakeAttributeResident(AttributeReference attrRef, int maxData)
        {
            if (attrRef.File != MftReference)
            {
                throw new NotImplementedException("Changing residency of attributes in extended file records");
            }

            NtfsAttribute attr = GetAttribute(attrRef);
            if (!attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already resident");
            }

            attr.SetNonResident(false, maxData);
            _baseRecord.SetAttribute(attr.Record); 
        }

        #endregion

        #region Streams
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
            foreach(var attr in _attributes)
            {
                if(attr.Type == attrType && attr.Name == name)
                {
                    yield return new NtfsStream(this, attr);
                }
            }
        }

        public IEnumerable<NtfsStream> AllStreams
        {
            get
            {
                foreach (var attr in _attributes)
                {
                    yield return new NtfsStream(this, attr);
                }
            }
        }

        public NtfsStream CreateStream(AttributeType attrType, string name)
        {
            return new NtfsStream(this, CreateAttribute(attrType, name));
        }

        public NtfsStream CreateStream(AttributeType attrType, string name, long firstCluster, ulong numClusters, uint bytesPerCluster)
        {
            return new NtfsStream(this, CreateAttribute(attrType, name, firstCluster, numClusters, bytesPerCluster));
        }

        public SparseStream OpenStream(AttributeType attrType, string name, FileAccess access)
        {
            return new FileStream(this, GetAttribute(attrType, name), access);
        }

        public void RemoveStream(NtfsStream stream)
        {
            RemoveAttribute(stream.Attribute);
        }

        #endregion

        public FileNameRecord GetFileNameRecord(string name, bool freshened)
        {
            NtfsAttribute[] attrs = GetAttributes(AttributeType.FileName);
            StructuredNtfsAttribute<FileNameRecord> attr = null;
            if (String.IsNullOrEmpty(name))
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

            FileNameRecord fnr = (attr == null ? new FileNameRecord() : new FileNameRecord(attr.Content));

            if (freshened)
            {
                FreshenFileName(fnr, false);
            }

            return fnr;
        }

        public void FreshenFileName(FileNameRecord fileName, bool updateMftRecord)
        {
            //
            // Freshen the record from the definitive info in the other attributes
            //
            StandardInformation si = GetStream(AttributeType.StandardInformation, null).GetContent<StandardInformation>();
            NtfsAttribute anonDataAttr = GetAttribute(AttributeType.Data, null);

            fileName.CreationTime = si.CreationTime;
            fileName.ModificationTime = si.ModificationTime;
            fileName.MftChangedTime = si.MftChangedTime;
            fileName.LastAccessTime = si.LastAccessTime;
            fileName.Flags = si.FileAttributes;

            if (_baseRecordDirty && NtfsTransaction.Current != null)
            {
                fileName.MftChangedTime = NtfsTransaction.Current.Timestamp;
            }

            // Directories don't have directory flag set in StandardInformation, so set from MFT record
            if ((_baseRecord.Flags & FileRecordFlags.IsDirectory) != 0)
            {
                fileName.Flags |= FileAttributeFlags.Directory;
            }

            if (anonDataAttr != null)
            {
                fileName.RealSize = (ulong)anonDataAttr.Record.DataLength;
                fileName.AllocatedSize = (ulong)anonDataAttr.Record.AllocatedLength;
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
                if (_baseRecord.MasterFileTableIndex == MasterFileTable.RootDirIndex)
                {
                    record.Flags |= FileAttributeFlags.Directory;
                }

                return new DirectoryEntry(_context.GetDirectoryByRef(record.ParentDirectory), MftReference, record);
            }
        }

        internal INtfsContext Context
        {
            get
            {
                return _context;
            }
        }

        internal long GetAttributeOffset(ushort id)
        {
            return _baseRecord.GetAttributeOffset(id);
        }

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE (" + ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + _baseRecord.MasterFileTableIndex);

            _baseRecord.Dump(writer, indent + "  ");

            foreach (AttributeRecord attrRec in _baseRecord.Attributes)
            {
                NtfsAttribute.FromRecord(this, MftReference, attrRec).Dump(writer, indent + "  ");
            }
        }

        public string BestName
        {
            get
            {
                NtfsAttribute[] attrs = GetAttributes(AttributeType.FileName);

                string longName = null;
                int longest = 0;

                if (attrs != null && attrs.Length != 0)
                {
                    longName = attrs[0].ToString();

                    for (int i = 1; i < attrs.Length; ++i)
                    {
                        string name = attrs[i].ToString();

                        if (Utilities.Is8Dot3(longName))
                        {
                            longest = i;
                            longName = name;
                        }
                    }
                }

                return longName;
            }
        }

        public override string ToString()
        {
            string bestName = BestName;
            if (bestName == null)
            {
                return "?????";
            }
            else
            {
                return bestName;
            }
        }

        internal static File CreateNew(INtfsContext context)
        {
            return CreateNew(context, FileRecordFlags.None);
        }

        internal static File CreateNew(INtfsContext context, FileRecordFlags flags)
        {
            DateTime now = DateTime.UtcNow;

            File newFile = context.AllocateFile(flags);

            StandardInformation.InitializeNewFile(newFile, FileAttributeFlags.Archive | FileRecord.ConvertFlags(flags));

            if (context.ObjectIds != null)
            {
                Guid newId = CreateNewGuid(context);
                NtfsStream stream = newFile.CreateStream(AttributeType.ObjectId, null);
                ObjectId objId = new ObjectId();
                objId.Id = newId;
                stream.SetContent(objId);
                context.ObjectIds.Add(newId, newFile.MftReference, newId, Guid.Empty, Guid.Empty);
            }

            newFile.CreateAttribute(AttributeType.Data);

            newFile.UpdateRecordInMft();

            return newFile;
        }

        internal void Delete()
        {
            if (_baseRecord.HardLinkCount != 0)
            {
                throw new InvalidOperationException("Attempt to delete in-use file: " + ToString());
            }

            NtfsStream objIdStream = GetStream(AttributeType.ObjectId, null);
            if (objIdStream != null)
            {
                ObjectId objId = objIdStream.GetContent<ObjectId>();
                Context.ObjectIds.Remove(objId.Id);
            }

            foreach (var attr in _attributes)
            {
                attr.GetDataBuffer().SetCapacity(0);
            }

            AttributeRecord attrListRec = _baseRecord.GetAttribute(AttributeType.AttributeList);
            if (attrListRec != null)
            {
                StructuredNtfsAttribute<AttributeList> attrList = (StructuredNtfsAttribute<AttributeList>)GetAttribute(new AttributeReference(MftReference, attrListRec.AttributeId));
                foreach (var record in attrList.Content)
                {
                    FileRecord attrFileRecord = _baseRecord;
                    if (record.BaseFileReference.MftIndex != _baseRecord.MasterFileTableIndex)
                    {
                        attrFileRecord = _context.Mft.GetRecord(record.BaseFileReference);
                    }

                    if (attrFileRecord != null)
                    {
                        attrFileRecord.RemoveAttribute(record.AttributeId);
                        if (attrFileRecord.Attributes.Count == 0)
                        {
                            _context.Mft.RemoveRecord(record.BaseFileReference);
                        }
                    }
                }
            }

            List<AttributeRecord> records = new List<AttributeRecord>(_baseRecord.Attributes);
            foreach (var record in records)
            {
                _baseRecord.RemoveAttribute(record.AttributeId);
            }

            _attributes.Clear();

            _context.Mft.RemoveRecord(MftReference);
            _context.ForgetFile(this);
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
            else
            {
                return Guid.NewGuid();
            }
        }


        /// <summary>
        /// Wrapper for Resident/Non-Resident attribute streams, that remains valid
        /// despite the attribute oscillating between resident and not.
        /// </summary>
        private class FileStream : SparseStream
        {
            private File _file;
            private NtfsAttribute _attr;
            private SparseStream _wrapped;

            public FileStream(File file, NtfsAttribute attr, FileAccess access)
            {
                _file = file;
                _attr = attr;
                _wrapped = attr.Open(access);
            }

            public override void Close()
            {
                base.Close();
                _wrapped.Close();
            }

            public override IEnumerable<StreamExtent> Extents
            {
                get
                {
                    return _wrapped.Extents;
                }
            }

            public override bool CanRead
            {
                get
                {
                    return _wrapped.CanRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return _wrapped.CanSeek;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return _wrapped.CanWrite;
                }
            }

            public override void Flush()
            {
                _wrapped.Flush();
            }

            public override long Length
            {
                get
                {
                    return _wrapped.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return _wrapped.Position;
                }
                set
                {
                    _wrapped.Position = value;
                }
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
                long newLength = Math.Max(_wrapped.Position + count, Length);

                ChangeAttributeResidencyByLength(newLength);
                _wrapped.Write(buffer, offset, count);
            }

            public override string ToString()
            {
                return _file.ToString() + ".attr[" + _attr.Id + "]";
            }

            /// <summary>
            /// Change attribute residency if it gets too big (or small).
            /// </summary>
            /// <param name="value">The new (anticipated) length of the stream</param>
            /// <remarks>Has hysteresis - the decision is based on the input and the current
            /// state, not the current state alone</remarks>
            private void ChangeAttributeResidencyByLength(long value)
            {
                // This is a bit of a hack - but it's really important the bitmap file remains non-resident
                if (_file._baseRecord.MasterFileTableIndex == MasterFileTable.BitmapIndex)
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

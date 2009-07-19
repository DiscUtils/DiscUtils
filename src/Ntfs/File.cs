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
    internal class File
    {
        protected INtfsContext _context;

        private MasterFileTable _mft;
        private FileRecord _baseRecord;
        private ObjectCache<ushort, NtfsAttribute> _attributeCache;
        private ObjectCache<string, Index> _indexCache;

        private bool _baseRecordDirty;

        private uint _attrStreamChangeId; // Indicates when AttributeStreams are invalid, and must be re-opened

        public File(INtfsContext context, FileRecord baseRecord)
        {
            _context = context;
            _mft = _context.Mft;
            _baseRecord = baseRecord;
            _attributeCache = new ObjectCache<ushort, NtfsAttribute>();
            _indexCache = new ObjectCache<string, Index>();
        }

        public uint IndexInMft
        {
            get { return _baseRecord.MasterFileTableIndex; }
        }

        public uint MaxMftRecordSize
        {
            get { return _baseRecord.AllocatedSize; }
        }

        public FileReference MftReference
        {
            get { return new FileReference(_baseRecord.MasterFileTableIndex, _baseRecord.SequenceNumber); }
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

                        foreach (string dirName in _context.GetDirectoryByRef(attr.Content.ParentDirectory).Names)
                        {
                            result.Add(Path.Combine(dirName, name));
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
                            MakeAttributeNonResident(attr.AttributeId, (int)attr.DataLength);
                            fixedAttribute = true;
                            break;
                        }
                    }

                    if (!fixedAttribute)
                    {
                        foreach (var attr in _baseRecord.Attributes)
                        {
                            if (!attr.IsNonResident && _context.AttributeDefinitions.CanBeNonResident(attr.AttributeType))
                            {
                                MakeAttributeNonResident(attr.AttributeId, (int)attr.DataLength);
                                fixedAttribute = true;
                                break;
                            }
                        }
                    }

                    if (!fixedAttribute)
                    {
                        foreach (var attr in _baseRecord.Attributes)
                        {
                            if (attr.IsNonResident && ((NonResidentAttributeRecord)attr).DataRuns.Count > 4)
                            {
                                DefragAttribute(attr.AttributeId);
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

        private SparseStream InnerOpenAttribute(AttributeReference id, FileAccess access)
        {
            NtfsAttribute attr = GetAttribute(id);

            if (attr == null)
            {
                throw new IOException("No such attribute: " + id);
            }

            return attr.Open(access);
        }

        /// <summary>
        /// Creates a new unnamed attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        private AttributeReference CreateAttribute(AttributeType type)
        {
            bool indexed = _context.AttributeDefinitions.IsIndexed(type);
            ushort id = _baseRecord.CreateAttribute(type, null, indexed);
            MarkMftRecordDirty();
            return new AttributeReference(MftReference, id);
        }

        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        /// <param name="name">The name of the new attribute</param>
        private AttributeReference CreateAttribute(AttributeType type, string name)
        {
            bool indexed = _context.AttributeDefinitions.IsIndexed(type);
            ushort id = _baseRecord.CreateAttribute(type, name, indexed);
            MarkMftRecordDirty();
            return new AttributeReference(MftReference, id);
        }

        private void RemoveAttribute(AttributeReference id)
        {
            if (id.File.MftIndex != _baseRecord.MasterFileTableIndex)
            {
                _context.GetFileByRef(id.File).RemoveAttribute(id);
                return;
            }

            NtfsAttribute attr = GetAttribute(id);
            if (attr != null)
            {
                if (attr.Record.AttributeType == AttributeType.IndexRoot)
                {
                    _indexCache.Remove(attr.Record.Name);
                }

                using (Stream s = new FileStream(this, id, FileAccess.Write))
                {
                    s.SetLength(0);
                }

                _baseRecord.RemoveAttribute(id.AttributeId);
                _attributeCache.Remove(id.AttributeId);
            }
        }

        /// <summary>
        /// Gets an attribute by reference.
        /// </summary>
        /// <param name="attrRef">Reference to the attribute</param>
        /// <returns>The attribute</returns>
        internal NtfsAttribute GetAttribute(AttributeReference attrRef)
        {
            if (attrRef.File.MftIndex != _baseRecord.MasterFileTableIndex)
            {
                return _context.GetFileByRef(attrRef.File).InnerGetAttribute(attrRef.AttributeId);
            }
            return InnerGetAttribute(attrRef.AttributeId);
        }

        /// <summary>
        ///  Gets the first (if more than one) instance of a named attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <param name="name">The attribute's name</param>
        /// <returns>The attribute of <c>null</c>.</returns>
        internal NtfsAttribute GetAttribute(AttributeType type, string name)
        {
            foreach (var attr in AllAttributes)
            {
                if (attr.Record.AttributeType == type && attr.Name == name)
                {
                    return attr;
                }
            }
            return null;
        }

        /// <summary>
        /// Enumerates through all attributes.
        /// </summary>
        internal IEnumerable<NtfsAttribute> AllAttributes
        {
            get
            {
                AttributeRecord attrListRec = _baseRecord.GetAttribute(AttributeType.AttributeList);
                if (attrListRec != null)
                {
                    StructuredNtfsAttribute<AttributeList> attrList = new StructuredNtfsAttribute<AttributeList>(this, MftReference, attrListRec);
                    foreach (var record in attrList.Content)
                    {
                        yield return GetAttribute(new AttributeReference(record.BaseFileReference, record.AttributeId));
                    }
                }
                else
                {
                    foreach (var record in _baseRecord.Attributes)
                    {
                        yield return InnerGetAttribute(record.AttributeId);
                    }
                }
            }
        }

        /// <summary>
        ///  Gets all instances of an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attributes.</returns>
        private NtfsAttribute[] GetAttributes(AttributeType type)
        {
            List<NtfsAttribute> matches = new List<NtfsAttribute>();

            foreach (var attr in AllAttributes)
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
            MakeAttributeNonResident(attrRef.AttributeId, maxData);
        }

        private void MakeAttributeNonResident(ushort attrId, int maxData)
        {
            NtfsAttribute attr = InnerGetAttribute(attrId);

            if(attr.IsNonResident)
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
            MakeAttributeResident(attrRef.AttributeId, maxData);
        }

        internal void MakeAttributeResident(ushort attrId, int maxData)
        {
            NtfsAttribute attr = InnerGetAttribute(attrId);

            if (!attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already resident");
            }

            attr.SetNonResident(false, maxData);
            _baseRecord.SetAttribute(attr.Record);
        }

        internal void DefragAttribute(ushort attrId)
        {
            NtfsAttribute attr = InnerGetAttribute(attrId);

            if (!attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is resident");
            }

            attr.Defrag();
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
            AttributeRecord attrListRec = _baseRecord.GetAttribute(AttributeType.AttributeList);
            if (attrListRec != null)
            {
                AttributeListRecord lastRecord = null;
                List<AttributeReference> attrRefs = new List<AttributeReference>();

                StructuredNtfsAttribute<AttributeList> attrList = new StructuredNtfsAttribute<AttributeList>(this, MftReference, attrListRec);
                foreach (var record in attrList.Content)
                {
                    if (record.Type == attrType && record.Name == name)
                    {
                        if (lastRecord != null && record.StartVcn == 0)
                        {
                            yield return new NtfsStream(this, lastRecord.Type, lastRecord.Name, attrRefs);
                            attrRefs = new List<AttributeReference>();
                        }

                        lastRecord = record;
                        attrRefs.Add(new AttributeReference(record.BaseFileReference, record.AttributeId));
                    }
                }

                if (lastRecord != null)
                {
                    yield return new NtfsStream(this, lastRecord.Type, lastRecord.Name, attrRefs);
                }
            }
            else
            {
                foreach (var record in _baseRecord.Attributes)
                {
                    if (record.AttributeType == attrType && record.Name == name)
                    {
                        yield return new NtfsStream(this, record.AttributeType, record.Name, new AttributeReference(MftReference, record.AttributeId));
                    }
                }
            }
        }

        public IEnumerable<NtfsStream> AllStreams
        {
            get
            {
                AttributeRecord attrListRec = _baseRecord.GetAttribute(AttributeType.AttributeList);
                if (attrListRec != null)
                {
                    AttributeListRecord lastRecord = null;
                    List<AttributeReference> attrRefs = new List<AttributeReference>();

                    StructuredNtfsAttribute<AttributeList> attrList = new StructuredNtfsAttribute<AttributeList>(this, MftReference, attrListRec);
                    foreach (var record in attrList.Content)
                    {
                        if (lastRecord != null && (record.StartVcn == 0 || record.Type != lastRecord.Type || record.Name != lastRecord.Name))
                        {
                            yield return new NtfsStream(this, lastRecord.Type, lastRecord.Name, attrRefs);
                            attrRefs = new List<AttributeReference>();
                        }

                        lastRecord = record;
                        attrRefs.Add(new AttributeReference(record.BaseFileReference, record.AttributeId));
                    }

                    if (lastRecord != null)
                    {
                        yield return new NtfsStream(this, lastRecord.Type, lastRecord.Name, attrRefs);
                    }
                }
                else
                {
                    foreach (var record in _baseRecord.Attributes)
                    {
                        yield return new NtfsStream(this, record.AttributeType, record.Name, new AttributeReference(MftReference, record.AttributeId));
                    }
                }
            }
        }

        public NtfsStream CreateStream(AttributeType attrType, string name)
        {
            AttributeReference attrRef = CreateAttribute(attrType, name);
            return new NtfsStream(this, attrType, name, attrRef);
        }

        public SparseStream OpenStream(AttributeType attrType, string name, FileAccess access)
        {
            return new FileStream(this, attrType, name, access);
        }

        internal SparseStream InnerOpenStream(AttributeType attrType, string name, FileAccess access)
        {
            NtfsStream stream = GetStream(attrType, name);
            if (stream == null)
            {
                return null;
            }

            return stream.Open(access);
        }

        public void RemoveStream(NtfsStream stream)
        {
            foreach (var attr in stream.GetAttributes())
            {
                RemoveAttribute(attr);
            }
        }

        #endregion

        internal uint AttributeStreamChangeId
        {
            get { return _attrStreamChangeId; }
        }

        internal void InvalidateAttributeStreams()
        {
            _attrStreamChangeId++;
        }

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
                        stream.SetContent(fileName);
                    }
                }
            }
        }

        public DirectoryEntry DirectoryEntry
        {
            get
            {
                FileNameRecord record = GetStream(AttributeType.FileName, null).GetContent<FileNameRecord>();

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

        private NtfsAttribute InnerGetAttribute(ushort id)
        {
            NtfsAttribute result = _attributeCache[id];
            if (result == null)
            {
                AttributeRecord attrRec = _baseRecord.GetAttribute(id);
                result = NtfsAttribute.FromRecord(this, MftReference, attrRec);
                _attributeCache[id] = result;
            }
            return result;
        }


        internal static File CreateNew(INtfsContext context)
        {
            DateTime now = DateTime.UtcNow;

            File newFile = context.AllocateFile(FileRecordFlags.None);

            NtfsStream stream = newFile.CreateStream(AttributeType.StandardInformation, null);
            StandardInformation si = new StandardInformation();
            si.CreationTime = now;
            si.ModificationTime = now;
            si.MftChangedTime = now;
            si.LastAccessTime = now;
            si.FileAttributes = FileAttributeFlags.Archive;
            stream.SetContent(si);

            Guid newId = CreateNewGuid(context);
            stream = newFile.CreateStream(AttributeType.ObjectId, null);
            ObjectId objId = new ObjectId();
            objId.Id = newId;
            stream.SetContent(objId);
            context.ObjectIds.Add(newId, newFile.MftReference, newId, Guid.Empty, Guid.Empty);

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

            List<NtfsAttribute> attrs = new List<NtfsAttribute>(AllAttributes);
            foreach (var attr in attrs)
            {
                RemoveAttribute(attr.Reference);
            }

            _context.DeleteFile(this);
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

            private AttributeReference _attrId;
            private AttributeType _attrType;
            private string _attrName;
            private bool _openByName;

            private FileAccess _access;

            private SparseStream _wrapped;
            private long _length;
            private uint _lastKnownStreamChangeSeqNum;

            public FileStream(File file, AttributeReference attrId, FileAccess access)
            {
                _file = file;
                _attrId = attrId;
                _access = access;

                ReopenWrapped();
            }

            public FileStream(File file, AttributeType attrType, string attrName, FileAccess access)
            {
                _file = file;
                _attrType = attrType;
                _attrName = attrName;
                _access = access;
                _openByName = true;

                ReopenWrapped();
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
                    CheckStreamValid();
                    return _wrapped.Extents;
                }
            }

            public override bool CanRead
            {
                get
                {
                    CheckStreamValid();
                    return _wrapped.CanRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    CheckStreamValid();
                    return _wrapped.CanSeek;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    CheckStreamValid();
                    return _wrapped.CanWrite;
                }
            }

            public override void Flush()
            {
                CheckStreamValid();
                _wrapped.Flush();
            }

            public override long Length
            {
                get
                {
                    CheckStreamValid();
                    return _length;
                }
            }

            public override long Position
            {
                get
                {
                    CheckStreamValid();
                    return _wrapped.Position;
                }
                set
                {
                    CheckStreamValid();
                    _wrapped.Position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                CheckStreamValid();
                return _wrapped.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                CheckStreamValid();
                return _wrapped.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                CheckStreamValid();
                _wrapped.SetLength(value);
                _length = value;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                long newLength = Math.Max(_wrapped.Position + count, _length);

                ChangeAttributeResidencyByLength(newLength);
                CheckStreamValid();
                _wrapped.Write(buffer, offset, count);

                if (newLength > _length)
                {
                    _length = newLength;
                }
            }

            public override string ToString()
            {
                return _file.ToString() + ".attr[" + _attrId + "]";
            }

            private void CheckStreamValid()
            {
                if (_lastKnownStreamChangeSeqNum != _file.AttributeStreamChangeId)
                {
                    ReopenWrapped();
                }
            }

            private void ReopenWrapped()
            {
                long pos = 0;
                if (_wrapped != null)
                {
                    pos = _wrapped.Position;
                    _wrapped.Dispose();
                }

                SparseStream newStream;
                if (_openByName)
                {
                    newStream = _file.InnerOpenStream(_attrType, _attrName, _access);
                }
                else
                {
                    newStream = _file.InnerOpenAttribute(_attrId, _access);
                }

                _length = newStream.Length;
                _lastKnownStreamChangeSeqNum = _file.AttributeStreamChangeId;
                _wrapped = newStream;
                _wrapped.Position = pos;
            }

            /// <summary>
            /// Change attribute residency if it gets too big (or small).
            /// </summary>
            /// <param name="value">The new (anticipated) length of the stream</param>
            /// <remarks>Has hysteresis - the decision is based on the input and the current
            /// state, not the current state alone</remarks>
            private void ChangeAttributeResidencyByLength(long value)
            {
                NtfsAttribute attr = _openByName ? _file.GetAttribute(_attrType, _attrName) : _file.GetAttribute(_attrId);
                if (!attr.IsNonResident && value >= _file.MaxMftRecordSize)
                {
                    _file.MakeAttributeNonResident(attr.Reference, (int)Math.Min(value, _wrapped.Length));
                    ReopenWrapped();
                }
                else if (attr.IsNonResident && value <= _file.MaxMftRecordSize / 4)
                {
                    // Use of 1/4 of record size here is just a heuristic - the important thing is not to end up with
                    // zero-length non-resident attributes
                    _file.MakeAttributeResident(attr.Reference, (int)value);
                    ReopenWrapped();
                }
            }
        }
    }
}

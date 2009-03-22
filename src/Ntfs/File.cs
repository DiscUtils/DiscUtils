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
        protected NtfsFileSystem _fileSystem;
        protected FileRecord _baseRecord;

        public File(NtfsFileSystem fileSystem, FileRecord baseRecord)
        {
            _fileSystem = fileSystem;
            _baseRecord = baseRecord;
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

        public ushort UpdateSequenceNumber
        {
            get { return _baseRecord.UpdateSequenceNumber; }
        }

        public uint MftRecordFreeSpace
        {
            get { return _baseRecord.AllocatedSize - _baseRecord.RealSize; }
        }

        public void UpdateRecordInMft()
        {
            // Make attributes non-resident until the data in the record fits, start with DATA attributes
            // by default, then kick other 'can-be' attributes out, finally move indexes.
            bool fixedAttribute = true;
            while (_baseRecord.Size > _fileSystem.MasterFileTable.RecordSize && fixedAttribute)
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
                        if (!attr.IsNonResident && _fileSystem.AttributeDefinitions.CanBeNonResident(attr.AttributeType))
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
                        if (attr.AttributeType == AttributeType.IndexRoot && GetAttribute(AttributeType.IndexAllocation, attr.Name) == null)
                        {
                            if (MakeIndexNonResident(attr.Name))
                            {
                                fixedAttribute = true;
                                break;
                            }
                        }
                    }
                }
            }

            // Still too large?  Error.
            if (_baseRecord.Size > _fileSystem.MasterFileTable.RecordSize)
            {
                throw new NotSupportedException("Spanning over multiple FileRecord entries - TBD");
            }

            _fileSystem.MasterFileTable.WriteRecord(_baseRecord);
        }

        private bool MakeIndexNonResident(string name)
        {
            NtfsAttribute attr = GetAttribute(AttributeType.IndexAllocation, name);

            // Nothing to win, can't make IndexRoot smaller than this
            // 8 = min size of entry that points to IndexAllocation...
            if (attr.Length <= IndexRoot.HeaderOffset + IndexHeader.Size + 8)
            {
                return false;
            }

            throw new NotImplementedException();
        }

        public ushort HardLinkCount
        {
            get { return _baseRecord.HardLinkCount; }
            set { _baseRecord.HardLinkCount = value; }
        }

        /// <summary>
        /// Creates a new unnamed attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        public ushort CreateAttribute(AttributeType type)
        {
            bool indexed = _fileSystem.AttributeDefinitions.IsIndexed(type);
            ushort id = _baseRecord.CreateAttribute(type, null, indexed);
            UpdateRecordInMft();
            return id;
        }

        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="type">The type of the new attribute</param>
        /// <param name="name">The name of the new attribute</param>
        public ushort CreateAttribute(AttributeType type, string name)
        {
            bool indexed = _fileSystem.AttributeDefinitions.IsIndexed(type);
            ushort id = _baseRecord.CreateAttribute(type, name, indexed);
            UpdateRecordInMft();
            return id;
        }

        /// <summary>
        /// Gets an attribute by it's id.
        /// </summary>
        /// <param name="id">The id of the attribute</param>
        /// <returns>The attribute</returns>
        public NtfsAttribute GetAttribute(ushort id)
        {
            foreach (var attr in AllAttributeRecords)
            {
                if (attr.AttributeId == id)
                {
                    return NtfsAttribute.FromRecord(this, attr);
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the first (if more than one) instance of an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public NtfsAttribute GetAttribute(AttributeType type)
        {
            return GetAttribute(type, null);
        }

        /// <summary>
        /// Gets the content of the first (if more than one) instance of an unnamed attribute.
        /// </summary>
        /// <typeparam name="T">The attribute's content structure</typeparam>
        /// <param name="type">The attribute type</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public T GetAttributeContent<T>(AttributeType type)
            where T : IByteArraySerializable, IDiagnosticTracer, new()
        {
            return new StructuredNtfsAttribute<T>(this, GetAttribute(type).Record).Content;
        }

        /// <summary>
        /// Gets the content of the first (if more than one) instance of an unnamed attribute.
        /// </summary>
        /// <typeparam name="T">The attribute's content structure</typeparam>
        /// <param name="type">The attribute type</param>
        /// <param name="name">The attribute's name</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public T GetAttributeContent<T>(AttributeType type, string name)
            where T : IByteArraySerializable, IDiagnosticTracer, new()
        {
            return new StructuredNtfsAttribute<T>(this, GetAttribute(type, name).Record).Content;
        }

        /// <summary>
        /// Sets the content of an attribute.
        /// </summary>
        /// <typeparam name="T">The attribute's content structure</typeparam>
        /// <param name="id">The attribute's id</param>
        /// <param name="value">The new value for the attribute</param>
        public void SetAttributeContent<T>(ushort id, T value)
            where T : IByteArraySerializable, IDiagnosticTracer, new()
        {
            StructuredNtfsAttribute<T> attr = new StructuredNtfsAttribute<T>(this, GetAttribute(id).Record);
            attr.Content = value;
            attr.Save();
        }

        /// <summary>
        ///  Gets the first (if more than one) instance of a named attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <param name="name">The attribute's name</param>
        /// <returns>The attribute of <c>null</c>.</returns>
        public NtfsAttribute GetAttribute(AttributeType type, string name)
        {
            foreach (var attr in AllAttributeRecords)
            {
                if (attr.AttributeType == type && attr.Name == name)
                {
                    return NtfsAttribute.FromRecord(this, attr);
                }
            }
            return null;
        }

        /// <summary>
        ///  Gets all instances of an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public NtfsAttribute[] GetAttributes(AttributeType type)
        {
            List<NtfsAttribute> matches = new List<NtfsAttribute>();

            foreach (var attr in AllAttributeRecords)
            {
                if (attr.AttributeType == type && string.IsNullOrEmpty(attr.Name))
                {
                    matches.Add(NtfsAttribute.FromRecord(this, attr));
                }
            }

            return matches.ToArray();
        }

        public SparseStream OpenAttribute(ushort id, FileAccess access)
        {
            NtfsAttribute attr = GetAttribute(id);

            if (attr == null)
            {
                throw new IOException("No such attribute: " + id);
            }

            return new FileAttributeStream(this, id, access);
        }

        public SparseStream OpenAttribute(AttributeType type, FileAccess access)
        {
            NtfsAttribute attr = GetAttribute(type);

            if (attr == null)
            {
                throw new IOException("No such attribute: " + type);
            }

            return new FileAttributeStream(this, attr.Id, access);
        }

        public SparseStream OpenAttribute(AttributeType type, string name, FileAccess access)
        {
            NtfsAttribute attr = GetAttribute(type, name);

            if (attr == null)
            {
                throw new IOException("No such attribute: " + type + "(" + name + ")");
            }

            return new FileAttributeStream(this, attr.Id, access);
        }

        public void MakeAttributeNonResident(ushort attrId, int maxData)
        {
            NtfsAttribute attr = GetAttribute(attrId);

            if(attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already non-resident");
            }

            attr.SetNonResident(true, maxData);
            _baseRecord.SetAttribute(attr.Record);
        }

        internal void MakeAttributeResident(ushort attrId, int maxData)
        {
            NtfsAttribute attr = GetAttribute(attrId);

            if (!attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already resident");
            }

            attr.SetNonResident(false, maxData);
            _baseRecord.SetAttribute(attr.Record);
        }

        public FileAttributes FileAttributes
        {
            get
            {
                return GetAttributeContent<FileNameRecord>(AttributeType.FileName).FileAttributes;
            }
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
                    if (_fileSystem.UpperCase.Compare(a.Content.FileName, name) == 0)
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
                FreshenFileName(fnr);
            }

            return fnr;
        }

        public void FreshenFileName(FileNameRecord fileName)
        {
            //
            // Freshen the record from the definitive info in the other attributes
            //
            StandardInformation si = GetAttributeContent<StandardInformation>(AttributeType.StandardInformation);
            NtfsAttribute anonDataAttr = GetAttribute(AttributeType.Data);

            fileName.CreationTime = si.CreationTime;
            fileName.ModificationTime = si.ModificationTime;
            fileName.MftChangedTime = si.MftChangedTime;
            fileName.LastAccessTime = si.LastAccessTime;
            fileName.Flags = si.FileAttributes;
            fileName.RealSize = (ulong)anonDataAttr.Record.DataLength;
            fileName.AllocatedSize = (ulong)anonDataAttr.Record.AllocatedLength;
        }

        public DirectoryEntry DirectoryEntry
        {
            get
            {
                FileNameRecord record = GetAttributeContent<FileNameRecord>(AttributeType.FileName);
                return new DirectoryEntry(_fileSystem.MasterFileTable.GetDirectory(record.ParentDirectory), MftReference, record);
            }
        }

        internal NtfsFileSystem FileSystem
        {
            get
            {
                return _fileSystem;
            }
        }

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE (" + _baseRecord.ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + _baseRecord.MasterFileTableIndex);

            foreach (AttributeRecord attrRec in _baseRecord.Attributes)
            {
                NtfsAttribute.FromRecord(this, attrRec).Dump(writer, indent + "  ");
            }
        }

        public override string ToString()
        {
            NtfsAttribute[] attrs = GetAttributes(AttributeType.FileName);

            int longest = 0;
            string longName = attrs[0].ToString();

            for (int i = 1; i < attrs.Length; ++i)
            {
                string name = attrs[i].ToString();

                if (Utilities.Is8Dot3(longName))
                {
                    longest = i;
                    longName = name;
                }
            }

            return longName;
        }

        private IEnumerable<AttributeRecord> AllAttributeRecords
        {
            get
            {
                AttributeRecord attrListRec = _baseRecord.GetAttribute(AttributeType.AttributeList);
                if (attrListRec != null)
                {
                    StructuredNtfsAttribute<AttributeList> attrList = new StructuredNtfsAttribute<AttributeList>(this, attrListRec);
                    foreach (var record in attrList.Content)
                    {
                        FileRecord fileRec = _fileSystem.MasterFileTable.GetRecord(record.BaseFileReference);
                        yield return fileRec.GetAttribute(record.AttributeId);
                    }
                }
                else
                {
                    foreach (var record in _baseRecord.Attributes)
                    {
                        yield return record;
                    }
                }
            }
        }


        internal static File CreateNew(NtfsFileSystem fileSystem)
        {
            DateTime now = DateTime.UtcNow;

            File newFile = fileSystem.MasterFileTable.AllocateFile();

            ushort attrId = newFile.CreateAttribute(AttributeType.StandardInformation);
            StandardInformation si = new StandardInformation();
            si.CreationTime = now;
            si.ModificationTime = now;
            si.MftChangedTime = now;
            si.LastAccessTime = now;
            si.FileAttributes = FileAttributeFlags.Archive;
            newFile.SetAttributeContent(attrId, si);

            Guid newId = Guid.NewGuid();
            attrId = newFile.CreateAttribute(AttributeType.ObjectId);
            ObjectId objId = new ObjectId();
            objId.Id = newId;
            newFile.SetAttributeContent(attrId, objId);
            fileSystem.ObjectIds.Add(newId, newFile.MftReference, newId, Guid.Empty, Guid.Empty);

            newFile.CreateAttribute(AttributeType.Data);

            newFile.UpdateRecordInMft();

            return newFile;
        }
    }
}

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
using DiscUtils.Ntfs.Attributes;

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
            get { return _baseRecord.MaxSize; }
        }

        public void UpdateRecordInMft()
        {
            // Make attributes non-resident until the data in the record fits, start with DATA attributes
            // by default.
            bool fixedAttribute = true;
            while (_baseRecord.Size > _fileSystem.MasterFileTable.RecordSize && fixedAttribute)
            {
                fixedAttribute = false;
                foreach (var attr in _baseRecord.Attributes)
                {
                    if (!attr.IsNonResident && attr.AttributeType == AttributeType.Data)
                    {
                        MakeAttributeNonResident(attr.AttributeType, attr.Name, (int)attr.DataLength);
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
                            MakeAttributeNonResident(attr.AttributeType, attr.Name, (int)attr.DataLength);
                            fixedAttribute = true;
                            break;
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
        public BaseAttribute GetAttribute(ushort id)
        {
            foreach (var attr in AllAttributeRecords)
            {
                if (attr.AttributeId == id)
                {
                    return BaseAttribute.FromRecord(_fileSystem, attr);
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the first (if more than one) instance of an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public BaseAttribute GetAttribute(AttributeType type)
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
            return new StructuredAttribute<T>(_fileSystem, GetAttribute(type).Record).Content;
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
            return new StructuredAttribute<T>(_fileSystem, GetAttribute(type, name).Record).Content;
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
            StructuredAttribute<T> attr = new StructuredAttribute<T>(_fileSystem, GetAttribute(id).Record);
            attr.Content = value;
            attr.Save();
        }

        /// <summary>
        ///  Gets the first (if more than one) instance of a named attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <param name="name">The attribute's name</param>
        /// <returns>The attribute of <c>null</c>.</returns>
        public BaseAttribute GetAttribute(AttributeType type, string name)
        {
            foreach (var attr in AllAttributeRecords)
            {
                if (attr.AttributeType == type && attr.Name == name)
                {
                    return BaseAttribute.FromRecord(_fileSystem, attr);
                }
            }
            return null;
        }

        /// <summary>
        ///  Gets all instances of an unnamed attribute.
        /// </summary>
        /// <param name="type">The attribute type</param>
        /// <returns>The attribute, or <c>null</c>.</returns>
        public BaseAttribute[] GetAttributes(AttributeType type)
        {
            List<BaseAttribute> matches = new List<BaseAttribute>();

            foreach (var attr in AllAttributeRecords)
            {
                if (attr.AttributeType == type && string.IsNullOrEmpty(attr.Name))
                {
                    matches.Add(BaseAttribute.FromRecord(_fileSystem, attr));
                }
            }

            return matches.ToArray();
        }

        public Stream OpenAttribute(AttributeType type, FileAccess access)
        {
            BaseAttribute attr = GetAttribute(type);

            if (attr == null)
            {
                throw new IOException("No such attribute: " + type);
            }

            return attr.Open(access);
        }

        public SparseStream OpenAttribute(AttributeType type, string name, FileAccess access)
        {
            BaseAttribute attr = GetAttribute(type, name);

            if (attr == null)
            {
                throw new IOException("No such attribute: " + type + "(" + name + ")");
            }

            return attr.Open(access);
        }

        public void MakeAttributeNonResident(AttributeType attributeType, string name, int maxData)
        {
            BaseAttribute attr = GetAttribute(attributeType, name);

            if(attr.IsNonResident)
            {
                throw new InvalidOperationException("Attribute is already non-resident");
            }

            attr.SetNonResident(true, maxData);
            _baseRecord.SetAttribute(attr.Record);
        }

        internal void MakeAttributeResident(AttributeType attributeType, string name, int maxData)
        {
            BaseAttribute attr = GetAttribute(attributeType, name);

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

        public DirectoryEntry DirectoryEntry
        {
            get
            {
                FileNameRecord record = GetAttributeContent<FileNameRecord>(AttributeType.FileName);
                return new DirectoryEntry(_fileSystem.MasterFileTable.GetDirectory(record.ParentDirectory), new FileReference(_baseRecord.MasterFileTableIndex), record);
            }
        }

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE (" + _baseRecord.ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + _baseRecord.MasterFileTableIndex);

            foreach (AttributeRecord attrRec in _baseRecord.Attributes)
            {
                BaseAttribute.FromRecord(_fileSystem, attrRec).Dump(writer, indent + "  ");
            }
        }

        public override string ToString()
        {
            BaseAttribute[] attrs = GetAttributes(AttributeType.FileName);

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
                    StructuredAttribute<AttributeList> attrList = new StructuredAttribute<AttributeList>(_fileSystem, attrListRec);
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

    }
}

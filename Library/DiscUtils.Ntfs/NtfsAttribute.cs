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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class NtfsAttribute : IDiagnosticTraceable
    {
        private IBuffer _cachedRawBuffer;
        protected FileRecordReference _containingFile;
        protected Dictionary<AttributeReference, AttributeRecord> _extents;
        protected File _file;
        protected AttributeRecord _primaryRecord;

        protected NtfsAttribute(File file, FileRecordReference containingFile, AttributeRecord record)
        {
            _file = file;
            _containingFile = containingFile;
            _primaryRecord = record;
            _extents = new Dictionary<AttributeReference, AttributeRecord>();
            _extents.Add(new AttributeReference(containingFile, record.AttributeId), _primaryRecord);
        }

        protected string AttributeTypeName
        {
            get
            {
                switch (_primaryRecord.AttributeType)
                {
                    case AttributeType.StandardInformation:
                        return "STANDARD INFORMATION";
                    case AttributeType.FileName:
                        return "FILE NAME";
                    case AttributeType.SecurityDescriptor:
                        return "SECURITY DESCRIPTOR";
                    case AttributeType.Data:
                        return "DATA";
                    case AttributeType.Bitmap:
                        return "BITMAP";
                    case AttributeType.VolumeName:
                        return "VOLUME NAME";
                    case AttributeType.VolumeInformation:
                        return "VOLUME INFORMATION";
                    case AttributeType.IndexRoot:
                        return "INDEX ROOT";
                    case AttributeType.IndexAllocation:
                        return "INDEX ALLOCATION";
                    case AttributeType.ObjectId:
                        return "OBJECT ID";
                    case AttributeType.ReparsePoint:
                        return "REPARSE POINT";
                    case AttributeType.AttributeList:
                        return "ATTRIBUTE LIST";
                    default:
                        return "UNKNOWN";
                }
            }
        }

        public long CompressedDataSize
        {
            get
            {
                NonResidentAttributeRecord firstExtent = FirstExtent as NonResidentAttributeRecord;
                if (firstExtent == null)
                {
                    return FirstExtent.AllocatedLength;
                }
                return firstExtent.CompressedDataSize;
            }

            set
            {
                NonResidentAttributeRecord firstExtent = FirstExtent as NonResidentAttributeRecord;
                if (firstExtent != null)
                {
                    firstExtent.CompressedDataSize = value;
                }
            }
        }

        public int CompressionUnitSize
        {
            get
            {
                NonResidentAttributeRecord firstExtent = FirstExtent as NonResidentAttributeRecord;
                if (firstExtent == null)
                {
                    return 0;
                }
                return firstExtent.CompressionUnitSize;
            }

            set
            {
                NonResidentAttributeRecord firstExtent = FirstExtent as NonResidentAttributeRecord;
                if (firstExtent != null)
                {
                    firstExtent.CompressionUnitSize = value;
                }
            }
        }

        public IDictionary<AttributeReference, AttributeRecord> Extents
        {
            get { return _extents; }
        }

        public AttributeRecord FirstExtent
        {
            get
            {
                if (_extents != null)
                {
                    foreach (KeyValuePair<AttributeReference, AttributeRecord> extent in _extents)
                    {
                        NonResidentAttributeRecord nonResident = extent.Value as NonResidentAttributeRecord;
                        if (nonResident == null)
                        {
                            // Resident attribute, so there can only be one...
                            return extent.Value;
                        }
                        if (nonResident.StartVcn == 0)
                        {
                            return extent.Value;
                        }
                    }
                }

                throw new InvalidDataException("Attribute with no initial extent");
            }
        }

        public AttributeFlags Flags
        {
            get { return _primaryRecord.Flags; }

            set
            {
                _primaryRecord.Flags = value;
                _cachedRawBuffer = null;
            }
        }

        public ushort Id
        {
            get { return _primaryRecord.AttributeId; }
        }

        public bool IsNonResident
        {
            get { return _primaryRecord.IsNonResident; }
        }

        public AttributeRecord LastExtent
        {
            get
            {
                AttributeRecord last = null;

                if (_extents != null)
                {
                    long lastVcn = 0;
                    foreach (KeyValuePair<AttributeReference, AttributeRecord> extent in _extents)
                    {
                        NonResidentAttributeRecord nonResident = extent.Value as NonResidentAttributeRecord;
                        if (nonResident == null)
                        {
                            // Resident attribute, so there can only be one...
                            return extent.Value;
                        }

                        if (nonResident.LastVcn >= lastVcn)
                        {
                            last = extent.Value;
                            lastVcn = nonResident.LastVcn;
                        }
                    }
                }

                return last;
            }
        }

        public long Length
        {
            get { return _primaryRecord.DataLength; }
        }

        public string Name
        {
            get { return _primaryRecord.Name; }
        }

        public AttributeRecord PrimaryRecord
        {
            get { return _primaryRecord; }
        }

        public IBuffer RawBuffer
        {
            get
            {
                if (_cachedRawBuffer == null)
                {
                    if (_primaryRecord.IsNonResident)
                    {
                        _cachedRawBuffer = new NonResidentAttributeBuffer(_file, this);
                    }
                    else
                    {
                        _cachedRawBuffer = ((ResidentAttributeRecord)_primaryRecord).DataBuffer;
                    }
                }

                return _cachedRawBuffer;
            }
        }

        public List<AttributeRecord> Records
        {
            get
            {
                List<AttributeRecord> records = new List<AttributeRecord>(_extents.Values);
                records.Sort(AttributeRecord.CompareStartVcns);
                return records;
            }
        }

        public AttributeReference Reference
        {
            get { return new AttributeReference(_containingFile, _primaryRecord.AttributeId); }
        }

        public AttributeType Type
        {
            get { return _primaryRecord.AttributeType; }
        }

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + AttributeTypeName + " ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");

            writer.WriteLine(indent + "  Length: " + _primaryRecord.DataLength + " bytes");
            if (_primaryRecord.DataLength == 0)
            {
                writer.WriteLine(indent + "    Data: <none>");
            }
            else
            {
                try
                {
                    using (Stream s = Open(FileAccess.Read))
                    {
                        string hex = string.Empty;
                        byte[] buffer = new byte[32];
                        int numBytes = s.Read(buffer, 0, buffer.Length);
                        for (int i = 0; i < numBytes; ++i)
                        {
                            hex = hex + string.Format(CultureInfo.InvariantCulture, " {0:X2}", buffer[i]);
                        }

                        writer.WriteLine(indent + "    Data: " + hex + (numBytes < s.Length ? "..." : string.Empty));
                    }
                }
                catch
                {
                    writer.WriteLine(indent + "    Data: <can't read>");
                }
            }

            _primaryRecord.Dump(writer, indent + "  ");
        }

        public static NtfsAttribute FromRecord(File file, FileRecordReference recordFile, AttributeRecord record)
        {
            switch (record.AttributeType)
            {
                case AttributeType.StandardInformation:
                    return new StructuredNtfsAttribute<StandardInformation>(file, recordFile, record);
                case AttributeType.FileName:
                    return new StructuredNtfsAttribute<FileNameRecord>(file, recordFile, record);
                case AttributeType.SecurityDescriptor:
                    return new StructuredNtfsAttribute<SecurityDescriptor>(file, recordFile, record);
                case AttributeType.Data:
                    return new NtfsAttribute(file, recordFile, record);
                case AttributeType.Bitmap:
                    return new NtfsAttribute(file, recordFile, record);
                case AttributeType.VolumeName:
                    return new StructuredNtfsAttribute<VolumeName>(file, recordFile, record);
                case AttributeType.VolumeInformation:
                    return new StructuredNtfsAttribute<VolumeInformation>(file, recordFile, record);
                case AttributeType.IndexRoot:
                    return new NtfsAttribute(file, recordFile, record);
                case AttributeType.IndexAllocation:
                    return new NtfsAttribute(file, recordFile, record);
                case AttributeType.ObjectId:
                    return new StructuredNtfsAttribute<ObjectId>(file, recordFile, record);
                case AttributeType.ReparsePoint:
                    return new StructuredNtfsAttribute<ReparsePointRecord>(file, recordFile, record);
                case AttributeType.AttributeList:
                    return new StructuredNtfsAttribute<AttributeList>(file, recordFile, record);
                default:
                    return new NtfsAttribute(file, recordFile, record);
            }
        }

        public void SetExtent(FileRecordReference containingFile, AttributeRecord record)
        {
            _cachedRawBuffer = null;
            _containingFile = containingFile;
            _primaryRecord = record;
            _extents.Clear();
            _extents.Add(new AttributeReference(containingFile, record.AttributeId), record);
        }

        public void AddExtent(FileRecordReference containingFile, AttributeRecord record)
        {
            _cachedRawBuffer = null;
            _extents.Add(new AttributeReference(containingFile, record.AttributeId), record);
        }

        public void RemoveExtentCacheSafe(AttributeReference reference)
        {
            _extents.Remove(reference);
        }

        public bool ReplaceExtent(AttributeReference oldRef, AttributeReference newRef, AttributeRecord record)
        {
            _cachedRawBuffer = null;

            if (!_extents.Remove(oldRef))
            {
                return false;
            }
            if (oldRef.Equals(Reference) || _extents.Count == 0)
            {
                _primaryRecord = record;
                _containingFile = newRef.File;
            }

            _extents.Add(newRef, record);
            return true;
        }

        public Range<long, long>[] GetClusters()
        {
            List<Range<long, long>> result = new List<Range<long, long>>();

            foreach (KeyValuePair<AttributeReference, AttributeRecord> extent in _extents)
            {
                result.AddRange(extent.Value.GetClusters());
            }

            return result.ToArray();
        }

        internal SparseStream Open(FileAccess access)
        {
            return new BufferStream(GetDataBuffer(), access);
        }

        internal IMappedBuffer GetDataBuffer()
        {
            return new NtfsAttributeBuffer(_file, this);
        }

        internal long OffsetToAbsolutePos(long offset)
        {
            return GetDataBuffer().MapPosition(offset);
        }
    }
}
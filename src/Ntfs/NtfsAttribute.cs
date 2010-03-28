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
    internal class NtfsAttribute : IDiagnosticTraceable
    {
        protected File _file;
        protected FileRecordReference _containingFile;
        protected AttributeRecord _record;
        protected Dictionary<AttributeReference, AttributeRecord> _extents;

        protected NtfsAttribute(File file, FileRecordReference containingFile, AttributeRecord record)
        {
            _file = file;
            _containingFile = containingFile;
            _record = record;
            _extents = new Dictionary<AttributeReference, AttributeRecord>();
            _extents.Add(new AttributeReference(containingFile, record.AttributeId), _record);
        }

        public void SetExtent(FileRecordReference containingFile, AttributeRecord record)
        {
            _containingFile = containingFile;
            _record = record;
            _extents.Clear();
            _extents.Add(new AttributeReference(containingFile, record.AttributeId), record);
        }

        public void AddExtent(FileRecordReference containingFile, AttributeRecord record)
        {
            _extents.Add(new AttributeReference(containingFile, record.AttributeId), record);
        }

        public void RemoveExtent(AttributeReference reference)
        {
            _extents.Remove(reference);
        }

        public bool ReplaceExtent(AttributeReference oldRef, AttributeReference newRef, AttributeRecord record)
        {
            if (!_extents.Remove(oldRef))
            {
                return false;
            }
            else
            {
                if (oldRef.Equals(Reference) || _extents.Count == 0)
                {
                    _record = record;
                    _containingFile = newRef.File;
                }

                _extents.Add(newRef, record);
                return true;
            }
        }

        public AttributeReference Reference
        {
            get
            {
                return new AttributeReference(_containingFile, _record.AttributeId);
            }
        }

        public AttributeType Type
        {
            get { return _record.AttributeType; }
        }

        public string Name
        {
            get { return _record.Name; }
        }

        public AttributeFlags Flags
        {
            get { return _record.Flags; }
        }

        public ushort Id
        {
            get { return _record.AttributeId; }
        }

        public long Length
        {
            get { return _record.DataLength; }
        }

        public AttributeRecord Record
        {
            get
            {
                return _record;
            }
        }


        public IDictionary<AttributeReference, AttributeRecord> Extents
        {
            get { return _extents; }
        }

        public AttributeRecord LastExtent
        {
            get
            {
                AttributeRecord last = null;

                if (_extents != null)
                {
                    long lastVcn = 0;
                    foreach (var extent in _extents)
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

        public NonResidentAttributeRecord GetNonResidentExtent(long targetVcn)
        {
            foreach (var extent in _extents)
            {
                NonResidentAttributeRecord nonResident = extent.Value as NonResidentAttributeRecord;
                if (nonResident == null)
                {
                    throw new IOException("Attempt to get non-resident extent from resident attribute");
                }

                if (nonResident.StartVcn <= targetVcn && nonResident.LastVcn >= targetVcn)
                {
                    return nonResident;
                }
            }

            throw new IOException("Attempt to access position outside of a known extent");
        }

        public bool IsNonResident
        {
            get { return _record.IsNonResident; }
        }

        public Range<long, long>[] GetClusters()
        {
            return _record.GetClusters();
        }

        internal long OffsetToAbsolutePos(long offset)
        {
            if (_record.IsNonResident)
            {
                return _record.OffsetToAbsolutePos(offset, 0, _file.Context.BiosParameterBlock.BytesPerCluster);
            }
            else
            {
                long attrStart = _file.GetAttributeOffset(new AttributeReference(_containingFile, _record.AttributeId));
                long attrPos = _file.Context.GetFileByIndex(MasterFileTable.MftIndex).GetAttribute(AttributeType.Data, null).OffsetToAbsolutePos(attrStart);

                return _record.OffsetToAbsolutePos(offset, attrPos, 0);
            }
        }

        //public void SetNonResident(bool nonResident, int maxData)
        //{
        //    if (nonResident == _record.IsNonResident)
        //    {
        //        return;
        //    }

        //    IBuffer attrBuffer = GetDataBuffer();
        //    byte[] tempBuffer = Utilities.ReadFully(attrBuffer, 0, Math.Min((int)attrBuffer.Capacity, maxData));

        //    AttributeReference baseReference = Reference;
        //    foreach(var extent in _extents)
        //    {
        //        extent.Value.GetDataBuffer(_file).SetCapacity(0);
        //        _file.RemoveAttributeExtent(extent.Key);
        //    }

        //    if (nonResident)
        //    {
        //        _record = 
        //        _record = new NonResidentAttributeRecord(_record);
        //    }
        //    else
        //    {
        //        _record = new ResidentAttributeRecord(_record);
        //    }

        //    _extents.Clear();
        //    _extents.Add(new AttributeReference(_file.MftReference, _record.AttributeId), _record);

        //    attrBuffer = GetDataBuffer();
        //    attrBuffer.Write(0, tempBuffer, 0, tempBuffer.Length);
        //}

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

        public virtual void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + AttributeTypeName + " ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");

            writer.WriteLine(indent + "  Length: " + _record.DataLength + " bytes");
            if (_record.DataLength == 0)
            {
                writer.WriteLine(indent + "    Data: <none>");
            }
            else
            {
                using (Stream s = Open(FileAccess.Read))
                {
                    string hex = "";
                    byte[] buffer = new byte[32];
                    int numBytes = s.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < numBytes; ++i)
                    {
                        hex = hex + string.Format(CultureInfo.InvariantCulture, " {0:X2}", buffer[i]);
                    }

                    writer.WriteLine(indent + "    Data: " + hex + ((numBytes < s.Length) ? "..." : ""));
                }
            }

            _record.Dump(writer, indent + "  ");
        }

        internal SparseStream Open(FileAccess access)
        {
            return new BufferStream(GetDataBuffer(), access);
        }

        internal IBuffer GetDataBuffer()
        {
            return new NtfsAttributeBuffer(_file, this);
        }

        protected string AttributeTypeName
        {
            get
            {
                switch (_record.AttributeType)
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
    }

}

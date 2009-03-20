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
using System.IO;

namespace DiscUtils.Ntfs.Attributes
{
    internal abstract class BaseAttribute
    {
        protected NtfsFileSystem _fileSystem;
        protected AttributeRecord _record;

        public BaseAttribute(NtfsFileSystem fileSystem, AttributeRecord record)
        {
            _fileSystem = fileSystem;
            _record = record;
        }

        public string Name
        {
            get { return _record.Name; }
        }

        public long Length
        {
            get { return _record.DataLength; }
        }

        public AttributeRecord Record
        {
            get { return _record; }
        }

        public bool IsNonResident
        {
            get { return _record.IsNonResident; }
        }

        public void SetNonResident(bool nonResident, int maxData)
        {
            if (nonResident == _record.IsNonResident)
            {
                return;
            }

            byte[] buffer;
            using (Stream attrStream = Open(FileAccess.Read))
            {
                buffer = Utilities.ReadFully(attrStream, Math.Min((int)attrStream.Length, maxData));
                attrStream.SetLength(0);
            }

            _record = nonResident ? (AttributeRecord)new NonResidentFileAttributeRecord(_record)
                : (AttributeRecord)new ResidentFileAttributeRecord(_record);

            using (Stream attrStream = Open(FileAccess.Write))
            {
                attrStream.Write(buffer, 0, buffer.Length);
            }
        }

        public static BaseAttribute FromRecord(NtfsFileSystem fileSystem, AttributeRecord record)
        {
            switch (record.AttributeType)
            {
                case AttributeType.StandardInformation:
                    return new StructuredAttribute<StandardInformation>(fileSystem, record);
                case AttributeType.FileName:
                    return new StructuredAttribute<FileNameRecord>(fileSystem, record);
                case AttributeType.SecurityDescriptor:
                    return new StructuredAttribute<SecurityDescriptor>(fileSystem, record);
                case AttributeType.Data:
                    return new StreamAttribute(fileSystem, record);
                case AttributeType.Bitmap:
                    return new StreamAttribute(fileSystem, record);
                case AttributeType.VolumeName:
                    return new StructuredAttribute<VolumeName>(fileSystem, record);
                case AttributeType.VolumeInformation:
                    return new StructuredAttribute<VolumeInformation>(fileSystem, record);
                case AttributeType.IndexRoot:
                    return new StreamAttribute(fileSystem, record);
                case AttributeType.IndexAllocation:
                    return new StreamAttribute(fileSystem, record);
                case AttributeType.ObjectId:
                    return new StructuredAttribute<ObjectId>(fileSystem, record);
                case AttributeType.AttributeList:
                    return new StructuredAttribute<AttributeList>(fileSystem, record);
                default:
                    return new StreamAttribute(fileSystem, record);
            }
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
                    case AttributeType.AttributeList:
                        return "ATTRIBUTE LIST";
                    default:
                        return "UNKNOWN";
                }
            }
        }

        internal SparseStream Open(FileAccess access)
        {
            if (_fileSystem != null)
            {
                return _record.Open(_fileSystem.ClusterBitmap, _fileSystem.RawStream, _fileSystem.BytesPerCluster, access);
            }
            else
            {
                return _record.Open(null, null, 0, access);
            }
        }

        public abstract void Save();
        public abstract void Dump(TextWriter writer, string indent);
    }

}

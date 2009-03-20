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

namespace DiscUtils.Ntfs
{
    internal class NtfsAttribute : IDiagnosticTracer
    {
        protected NtfsFileSystem _fileSystem;
        protected AttributeRecord _record;

        public NtfsAttribute(NtfsFileSystem fileSystem, AttributeRecord record)
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

        public static NtfsAttribute FromRecord(NtfsFileSystem fileSystem, AttributeRecord record)
        {
            switch (record.AttributeType)
            {
                case AttributeType.StandardInformation:
                    return new StructuredNtfsAttribute<StandardInformation>(fileSystem, record);
                case AttributeType.FileName:
                    return new StructuredNtfsAttribute<FileNameRecord>(fileSystem, record);
                case AttributeType.SecurityDescriptor:
                    return new StructuredNtfsAttribute<SecurityDescriptor>(fileSystem, record);
                case AttributeType.Data:
                    return new NtfsAttribute(fileSystem, record);
                case AttributeType.Bitmap:
                    return new NtfsAttribute(fileSystem, record);
                case AttributeType.VolumeName:
                    return new StructuredNtfsAttribute<VolumeName>(fileSystem, record);
                case AttributeType.VolumeInformation:
                    return new StructuredNtfsAttribute<VolumeInformation>(fileSystem, record);
                case AttributeType.IndexRoot:
                    return new NtfsAttribute(fileSystem, record);
                case AttributeType.IndexAllocation:
                    return new NtfsAttribute(fileSystem, record);
                case AttributeType.ObjectId:
                    return new StructuredNtfsAttribute<ObjectId>(fileSystem, record);
                case AttributeType.AttributeList:
                    return new StructuredNtfsAttribute<AttributeList>(fileSystem, record);
                default:
                    return new NtfsAttribute(fileSystem, record);
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
                    byte[] buffer = new byte[5];
                    int numBytes = s.Read(buffer, 0, 5);
                    for (int i = 0; i < numBytes; ++i)
                    {
                        hex = hex + string.Format(CultureInfo.InvariantCulture, " {0:X2}", buffer[i]);
                    }

                    writer.WriteLine(indent + "    Data: " + hex + "...");
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

    }

}

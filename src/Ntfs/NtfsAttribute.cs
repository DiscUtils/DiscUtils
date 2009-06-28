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
    internal class NtfsAttribute : IDiagnosticTraceable
    {
        protected File _file;
        protected AttributeRecord _record;

        protected NtfsAttribute(File file, AttributeRecord record)
        {
            _file = file;
            _record = record;
        }

        public string Name
        {
            get { return _record.Name; }
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
            get { return _record; }
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
                long recordStart = _file.Context.Mft.GetRecordOffset(_file.MftReference);
                long attrStart = recordStart + _file.GetAttributeOffset(_record.AttributeId);
                long attrPos = _file.Context.GetFileByIndex(MasterFileTable.MftIndex).GetAttribute(AttributeType.Data).OffsetToAbsolutePos(attrStart);

                return _record.OffsetToAbsolutePos(offset, attrPos, 0);
            }
        }

        public void SetNonResident(bool nonResident, int maxData)
        {
            if (nonResident == _record.IsNonResident)
            {
                return;
            }

            byte[] buffer;
            using (Stream attrStream = OpenRaw(FileAccess.ReadWrite))
            {
                buffer = Utilities.ReadFully(attrStream, Math.Min((int)attrStream.Length, maxData));
                attrStream.SetLength(0);
            }

            _record = nonResident
                ? (AttributeRecord)new NonResidentAttributeRecord(_record)
                : (AttributeRecord)new ResidentAttributeRecord(_record);

            using (Stream attrStream = OpenRaw(FileAccess.Write))
            {
                attrStream.Write(buffer, 0, buffer.Length);
            }

            _file.InvalidateAttributeStreams();
        }

        public void Defrag()
        {
            using (Stream oldStream = OpenRaw(FileAccess.ReadWrite))
            {
                AttributeRecord newRecord = new NonResidentAttributeRecord(_record);

                using (Stream newStream = newRecord.OpenRaw(_file, FileAccess.Write))
                {
                    newStream.SetLength(oldStream.Length);

                    byte[] buffer = new byte[_file.Context.BiosParameterBlock.BytesPerCluster * 2];
                    int numRead = oldStream.Read(buffer, 0, buffer.Length);
                    while (numRead > 0)
                    {
                        newStream.Write(buffer, 0, numRead);
                        numRead = oldStream.Read(buffer, 0, buffer.Length);
                    }
                }

                oldStream.SetLength(0);
                _record = newRecord;
            }

            _file.InvalidateAttributeStreams();
        }

        public static NtfsAttribute FromRecord(File file, AttributeRecord record)
        {
            switch (record.AttributeType)
            {
                case AttributeType.StandardInformation:
                    return new StructuredNtfsAttribute<StandardInformation>(file, record);
                case AttributeType.FileName:
                    return new StructuredNtfsAttribute<FileNameRecord>(file, record);
                case AttributeType.SecurityDescriptor:
                    return new StructuredNtfsAttribute<SecurityDescriptor>(file, record);
                case AttributeType.Data:
                    return new NtfsAttribute(file, record);
                case AttributeType.Bitmap:
                    return new NtfsAttribute(file, record);
                case AttributeType.VolumeName:
                    return new StructuredNtfsAttribute<VolumeName>(file, record);
                case AttributeType.VolumeInformation:
                    return new StructuredNtfsAttribute<VolumeInformation>(file, record);
                case AttributeType.IndexRoot:
                    return new NtfsAttribute(file, record);
                case AttributeType.IndexAllocation:
                    return new NtfsAttribute(file, record);
                case AttributeType.ObjectId:
                    return new StructuredNtfsAttribute<ObjectId>(file, record);
                case AttributeType.AttributeList:
                    return new StructuredNtfsAttribute<AttributeList>(file, record);
                default:
                    return new NtfsAttribute(file, record);
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
                using (Stream s = OpenRaw(FileAccess.Read))
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

        internal SparseStream OpenRaw(FileAccess access)
        {
            return _record.OpenRaw(_file, access);
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

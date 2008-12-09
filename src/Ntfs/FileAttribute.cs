//
// Copyright (c) 2008, Kenneth Bell
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
using System.Security.AccessControl;
using System.Text;

namespace DiscUtils.Ntfs
{
    internal abstract class FileAttribute
    {
        protected NtfsFileSystem _fileSystem;
        protected FileAttributeRecord _record;

        public FileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
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

        public static FileAttribute FromRecord(NtfsFileSystem fileSystem, FileAttributeRecord record)
        {
            switch (record.AttributeType)
            {
                case AttributeType.StandardInformation:
                    return new StandardInformationFileAttribute((ResidentFileAttributeRecord)record);
                case AttributeType.FileName:
                    return new FileNameFileAttribute((ResidentFileAttributeRecord)record);
                case AttributeType.SecurityDescriptor:
                    return new SecurityDescriptorFileAttribute(fileSystem, record);
                case AttributeType.Data:
                    return new DataFileAttribute(fileSystem, record);
                case AttributeType.Bitmap:
                    return new BitmapFileAttribute(fileSystem, record);
                case AttributeType.VolumeName:
                    return new VolumeNameFileAttribute(fileSystem, record);
                case AttributeType.VolumeInformation:
                    return new VolumeInformationFileAttribute(fileSystem, record);
                case AttributeType.IndexRoot:
                    return new IndexRootFileAttribute((ResidentFileAttributeRecord)record);
                case AttributeType.IndexAllocation:
                    return new IndexAllocationFileAttribute(fileSystem, record);
                default:
                    return new UnknownFileAttribute(fileSystem, record);
            }
        }

        internal Stream Open()
        {
            if (_fileSystem != null)
            {
                return _record.Open(_fileSystem.RawStream, _fileSystem.BytesPerCluster);
            }
            else
            {
                return _record.Open(null, 0);
            }
        }

        public abstract void Dump(TextWriter writer, string indent);
    }

    internal class FileNameFileAttribute : FileAttribute
    {
        private FileNameRecord _fileNameRecord;

        public FileNameFileAttribute(ResidentFileAttributeRecord record)
            : base(null, record)
        {
            _fileNameRecord = new FileNameRecord(record.Data, 0);
        }

        public FileAttributes Attributes
        {
            get { return ConvertFlags(_fileNameRecord.Flags); }
        }

        public override string ToString()
        {
            return _fileNameRecord.FileName;
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE NAME ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "   Parent Directory: " + _fileNameRecord.ParentDirectory);
            writer.WriteLine(indent + "      Creation Time: " + _fileNameRecord.CreationTime);
            writer.WriteLine(indent + "  Modification Time: " + _fileNameRecord.ModificationTime);
            writer.WriteLine(indent + "   MFT Changed Time: " + _fileNameRecord.MftChangedTime);
            writer.WriteLine(indent + "   Last Access Time: " + _fileNameRecord.LastAccessTime);
            writer.WriteLine(indent + "     Allocated Size: " + _fileNameRecord.AllocatedSize);
            writer.WriteLine(indent + "          Real Size: " + _fileNameRecord.RealSize);
            writer.WriteLine(indent + "              Flags: " + _fileNameRecord.Flags);
            writer.WriteLine(indent + "    File Attributes: " + ConvertFlags(_fileNameRecord.Flags));
            writer.WriteLine(indent + "            Unknown: " + _fileNameRecord.Unknown);
            writer.WriteLine(indent + "          File Name: " + _fileNameRecord.FileName);
        }

        private static FileAttributes ConvertFlags(uint flags)
        {
            FileAttributes result = (FileAttributes)(flags & 0xFFFF);
            if ((flags & 0x10000000) != 0)
            {
                result |= FileAttributes.Directory;
            }
            return result;
        }
    }


    internal class StandardInformationFileAttribute : FileAttribute
    {
        private DateTime _creationTime;
        private DateTime _modificationTime;
        private DateTime _mftChangedTime;
        private DateTime _lastAccessTime;
        private uint _filePermissions;
        private uint _maxVersions;
        private uint _version;
        private uint _classId;
        private uint _ownerId;
        private uint _securityId;
        private ulong _quotaCharged;
        private ulong _updateSequenceNumber;

        public StandardInformationFileAttribute(ResidentFileAttributeRecord record)
            : base(null, record)
        {
            byte[] data = record.Data;

            _creationTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x00));
            _modificationTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x08));
            _mftChangedTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x10));
            _lastAccessTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(data, 0x18));
            _filePermissions = Utilities.ToUInt32LittleEndian(data, 0x20);
            _maxVersions = Utilities.ToUInt32LittleEndian(data, 0x24);
            _version = Utilities.ToUInt32LittleEndian(data, 0x28);
            _classId = Utilities.ToUInt32LittleEndian(data, 0x2C);

            if (data.Length > 0x30)
            {
                _ownerId = Utilities.ToUInt32LittleEndian(data, 0x30);
                _securityId = Utilities.ToUInt32LittleEndian(data, 0x34);
                _quotaCharged = Utilities.ToUInt64LittleEndian(data, 0x38);
                _updateSequenceNumber = Utilities.ToUInt64LittleEndian(data, 0x40);
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "STANDARD INFORMATION ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "      Creation Time: " + _creationTime);
            writer.WriteLine(indent + "  Modification Time: " + _modificationTime);
            writer.WriteLine(indent + "   MFT Changed Time: " + _mftChangedTime);
            writer.WriteLine(indent + "   Last Access Time: " + _lastAccessTime);
            writer.WriteLine(indent + "   File Permissions: " + (FileAttributes)_filePermissions);
            writer.WriteLine(indent + "       Max Versions: " + _maxVersions);
            writer.WriteLine(indent + "            Version: " + _version);
            writer.WriteLine(indent + "           Class Id: " + _classId);
            writer.WriteLine(indent + "        Security Id: " + _securityId);
            writer.WriteLine(indent + "      Quota Charged: " + _quotaCharged);
            writer.WriteLine(indent + "     Update Seq Num: " + _updateSequenceNumber);
        }
    }

    internal class SecurityDescriptorFileAttribute : FileAttribute
    {
        private ObjectSecurity _securityDescriptor;

        public SecurityDescriptorFileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
            _securityDescriptor = new FileSecurity();
            using (Stream s = Open())
            {
                _securityDescriptor.SetSecurityDescriptorBinaryForm(Utilities.ReadFully(s, (int)record.DataLength));
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "SECURITY DESCRIPTOR ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "  Descriptor: " + _securityDescriptor.GetSecurityDescriptorSddlForm(AccessControlSections.All));
        }
    }

    internal class DataFileAttribute : FileAttribute
    {
        public DataFileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "DATA ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");

            writer.WriteLine(indent + "  Length: " + _record.DataLength);
            if (_record.DataLength == 0)
            {
                writer.WriteLine(indent + "    Data: <none>");
            }
            else
            {
                using (Stream s = Open())
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
    }

    internal class BitmapFileAttribute : FileAttribute
    {
        public BitmapFileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "BITMAP ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");

            writer.WriteLine(indent + "  Length: " + _record.DataLength + " bytes");
            if (_record.DataLength == 0)
            {
                writer.WriteLine(indent + "    Data: <none>");
            }
            else
            {
                using (Stream s = Open())
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
    }

    internal class VolumeNameFileAttribute : FileAttribute
    {
        private string _volName;

        public VolumeNameFileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
            using (Stream s = Open())
            {
                byte[] nameBytes = Utilities.ReadFully(s, (int)record.DataLength);
                _volName = Encoding.Unicode.GetString(nameBytes);
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "VOLUME NAME ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "     Volume Name: " + _volName);
        }
    }

    internal class VolumeInformationFileAttribute : FileAttribute
    {
        private byte _majorVersion;
        private byte _minorVersion;
        private VolumeInformationFlags _flags;

        public VolumeInformationFileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
            using (Stream s = Open())
            {
                byte[] data = Utilities.ReadFully(s, (int)record.DataLength);
                _majorVersion = data[0x08];
                _minorVersion = data[0x09];
                _flags = (VolumeInformationFlags)Utilities.ToUInt16LittleEndian(data, 0x0A);
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "VOLUME INFORMATION ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "  Version: " + _majorVersion + "." + _minorVersion);
            writer.WriteLine(indent + "    Flags: " + _flags);
        }
    }

    internal class IndexRootFileAttribute : FileAttribute
    {
        private uint _rootAttrType;
        private uint _rootCollationRule;
        private uint _rootIndexAllocationEntrySize;
        private byte _rootClustersPerIndexRecord;

        private IndexEntryHeader _header;

        public IndexRootFileAttribute(ResidentFileAttributeRecord record)
            : base(null, record)
        {
            using (Stream s = Open())
            {
                byte[] data = record.Data;
                _rootAttrType = Utilities.ToUInt32LittleEndian(data, 0x00);
                _rootCollationRule = Utilities.ToUInt32LittleEndian(data, 0x04);
                _rootIndexAllocationEntrySize = Utilities.ToUInt32LittleEndian(data, 0x08);
                _rootClustersPerIndexRecord = data[0x0C];

                _header = new IndexEntryHeader(data, 0x10);
            }
        }

        public IndexEntryHeader Header
        {
            get { return _header; }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "INDEX ROOT ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            writer.WriteLine(indent + "            Root Attr Type: " + _rootAttrType);
            writer.WriteLine(indent + "       Root Collation Rule: " + _rootCollationRule);
            writer.WriteLine(indent + "     Root Index Alloc Size: " + _rootIndexAllocationEntrySize);
            writer.WriteLine(indent + "  Root Clusters Per Record: " + _rootClustersPerIndexRecord);

            writer.WriteLine(indent + "     Offset To First Entry: " + _header.OffsetToFirstEntry);
            writer.WriteLine(indent + "     Total Size Of Entries: " + _header.TotalSizeOfEntries);
            writer.WriteLine(indent + "     Alloc Size Of Entries: " + _header.AllocatedSizeOfEntries);
            writer.WriteLine(indent + "                     Flags: " + _header.Flags);
        }
    }

    internal class IndexAllocationFileAttribute : FileAttribute
    {
        public IndexAllocationFileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "INDEX ALLOCATION ATTRIBUTE (" + (Name == null ? "No Name" : Name) + ")");
            if (_record.DataLength == 0)
            {
                writer.WriteLine(indent + "  Data: <none>");
            }
            else
            {
                using (Stream s = Open())
                {
                    string hex = "";
                    byte[] buffer = new byte[5];
                    int numBytes = s.Read(buffer, 0, 5);
                    for (int i = 0; i < numBytes; ++i)
                    {
                        hex = hex + string.Format(CultureInfo.InvariantCulture, " {0:X2}", buffer[i]);
                    }

                    writer.WriteLine(indent + "  Data: " + hex + "...");
                }
            }
        }
    }

    internal class NonResidentFileAttribute : FileAttribute
    {
        public NonResidentFileAttribute(FileAttributeRecord record)
            : base(null, record)
        {
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "NON-RESIDENT ATTRIBUTE <" + _record.AttributeType + ">");
        }
    }

    internal class UnknownFileAttribute : FileAttribute
    {
        public UnknownFileAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
            : base(fileSystem, record)
        {
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "UNKNOWN ATTRIBUTE <" + _record.AttributeType + ">");
            writer.WriteLine(indent + "  Name: " + Name);
            if (_record.DataLength == 0)
            {
                writer.WriteLine(indent + "  Data: <none>");
            }
            else
            {
                using (Stream s = Open())
                {
                    string hex = "";
                    byte[] buffer = new byte[5];
                    int numBytes = s.Read(buffer, 0, 5);
                    for (int i = 0; i < numBytes; ++i)
                    {
                        hex = hex + string.Format(CultureInfo.InvariantCulture, " {0:X2}", buffer[i]);
                    }

                    writer.WriteLine(indent + "  Data: " + hex + "...");
                }
            }
        }
    }
}

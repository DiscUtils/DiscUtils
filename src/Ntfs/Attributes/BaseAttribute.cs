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
        protected FileAttributeRecord _record;

        public BaseAttribute(NtfsFileSystem fileSystem, FileAttributeRecord record)
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

        public FileAttributeRecord Record
        {
            get { return _record; }
        }

        public bool IsNonResident
        {
            get { return _record.IsNonResident; }
            set
            {
                if (value == _record.IsNonResident)
                {
                    return;
                }

                if (value == false)
                {
                    throw new NotImplementedException("Converting non-resident attribute to resident");
                }

                ResidentFileAttributeRecord oldRecord = (ResidentFileAttributeRecord)_record;
                byte[] buffer = oldRecord.GetData();

                _record = new NonResidentFileAttributeRecord(_record);

                using (Stream attrStream = Open(FileAccess.Write))
                {
                    attrStream.Write(buffer, 0, buffer.Length);
                }
            }
        }

        public static BaseAttribute FromRecord(NtfsFileSystem fileSystem, FileAttributeRecord record)
        {
            ResidentFileAttributeRecord asResident = record as ResidentFileAttributeRecord;

            switch (record.AttributeType)
            {
                case AttributeType.StandardInformation:
                    return new StandardInformationAttribute(asResident);
                case AttributeType.FileName:
                    return new FileNameAttribute(asResident);
                case AttributeType.SecurityDescriptor:
                    return new SecurityDescriptorAttribute(fileSystem, record);
                case AttributeType.Data:
                    return new DataAttribute(fileSystem, record);
                case AttributeType.Bitmap:
                    return new BitmapAttribute(fileSystem, record);
                case AttributeType.VolumeName:
                    return new VolumeNameAttribute(fileSystem, record);
                case AttributeType.VolumeInformation:
                    return new VolumeInformationAttribute(fileSystem, record);
                case AttributeType.IndexRoot:
                    return new IndexRootAttribute(asResident);
                case AttributeType.IndexAllocation:
                    return new IndexAllocationAttribute(fileSystem, record);
                case AttributeType.ObjectId:
                    return new ObjectIdAttribute(fileSystem, record);
                case AttributeType.AttributeList:
                    return new AttributeListAttribute(fileSystem, record);
                default:
                    return new UnknownAttribute(fileSystem, record);
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

        public abstract void Dump(TextWriter writer, string indent);
    }

}

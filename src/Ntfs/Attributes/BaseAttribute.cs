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

        public static BaseAttribute FromRecord(NtfsFileSystem fileSystem, FileAttributeRecord record)
        {
            switch (record.AttributeType)
            {
                case AttributeType.StandardInformation:
                    return new StandardInformationAttribute((ResidentFileAttributeRecord)record);
                case AttributeType.FileName:
                    return new FileNameAttribute((ResidentFileAttributeRecord)record);
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
                    return new IndexRootAttribute((ResidentFileAttributeRecord)record);
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

        internal Stream Open(FileAccess access)
        {
            if (_fileSystem != null)
            {
                return _record.Open(_fileSystem.RawStream, _fileSystem.BytesPerCluster, access);
            }
            else
            {
                return _record.Open(null, 0, access);
            }
        }

        public abstract void Dump(TextWriter writer, string indent);
    }

}

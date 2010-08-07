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
using System.Text;

namespace DiscUtils.Udf
{
    internal class FileEntry : IByteArraySerializable
    {
        public DescriptorTag DescriptorTag;
        public InformationControlBlock InformationControlBlock;
        public uint Uid;
        public uint Gid;
        public FilePermissions Permissions;
        public ushort FileLinkCount;
        public byte RecordFormat;
        public byte RecordDisplayAttributes;
        public uint RecordLength;
        public ulong InformationLength;
        public ulong LogicalBlocksRecorded;
        public DateTime AccessTime;
        public DateTime ModificationTime;
        public DateTime AttributeTime;
        public uint Checkpoint;
        public LongAllocationDescriptor ExtendedAttributeIcb;
        public ImplementationEntityIdentifier ImplementationIdentifier;
        public ulong UniqueId;
        public int ExtendedAttributesLength;
        public int AllocationDescriptorsLength;
        public byte[] ExtendedAttributes;
        public byte[] AllocationDescriptors;

        public virtual int ReadFrom(byte[] buffer, int offset)
        {
            DescriptorTag = Utilities.ToStruct<DescriptorTag>(buffer, offset);
            InformationControlBlock = Utilities.ToStruct<InformationControlBlock>(buffer, offset + 16);
            Uid = Utilities.ToUInt32LittleEndian(buffer, offset + 36);
            Gid = Utilities.ToUInt32LittleEndian(buffer, offset + 40);
            Permissions = (FilePermissions)Utilities.ToUInt32LittleEndian(buffer, offset + 44);
            FileLinkCount = Utilities.ToUInt16LittleEndian(buffer, offset + 48);
            RecordFormat = buffer[offset + 50];
            RecordDisplayAttributes = buffer[offset + 51];
            RecordLength = Utilities.ToUInt16LittleEndian(buffer, offset + 52);
            InformationLength = Utilities.ToUInt64LittleEndian(buffer, offset + 56);
            LogicalBlocksRecorded = Utilities.ToUInt64LittleEndian(buffer, offset + 64);
            AccessTime = UdfUtilities.ParseTimestamp(buffer, offset + 72);
            ModificationTime = UdfUtilities.ParseTimestamp(buffer, offset + 84);
            AttributeTime = UdfUtilities.ParseTimestamp(buffer, offset + 96);
            Checkpoint = Utilities.ToUInt32LittleEndian(buffer, offset + 108);
            ExtendedAttributeIcb = Utilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 112);
            ImplementationIdentifier = Utilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 128);
            UniqueId = Utilities.ToUInt64LittleEndian(buffer, offset + 160);
            ExtendedAttributesLength = Utilities.ToInt32LittleEndian(buffer, offset + 168);
            AllocationDescriptorsLength = Utilities.ToInt32LittleEndian(buffer, offset + 172);
            ExtendedAttributes = Utilities.ToByteArray(buffer, offset + 176, ExtendedAttributesLength);
            AllocationDescriptors = Utilities.ToByteArray(buffer, offset + 176 + ExtendedAttributesLength, AllocationDescriptorsLength);

            return (int)(176 + ExtendedAttributesLength + AllocationDescriptorsLength);
        }

        public virtual void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public virtual int Size
        {
            get { throw new NotImplementedException(); }
        }
    }
}

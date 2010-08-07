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
    internal class ExtendedFileEntry : FileEntry, IByteArraySerializable
    {
        public ulong ObjectSize;
        public DateTime CreationTime;
        public LongAllocationDescriptor StreamDirectoryIcb;

        public override int ReadFrom(byte[] buffer, int offset)
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
            ObjectSize = Utilities.ToUInt64LittleEndian(buffer, offset + 64);
            LogicalBlocksRecorded = Utilities.ToUInt64LittleEndian(buffer, offset + 72);
            AccessTime = UdfUtilities.ParseTimestamp(buffer, offset + 80);
            ModificationTime = UdfUtilities.ParseTimestamp(buffer, offset + 92);
            CreationTime = UdfUtilities.ParseTimestamp(buffer, offset + 104);
            AttributeTime = UdfUtilities.ParseTimestamp(buffer, offset + 116);
            Checkpoint = Utilities.ToUInt32LittleEndian(buffer, offset + 128);
            ExtendedAttributeIcb = Utilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 136);
            StreamDirectoryIcb = Utilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 152);
            ImplementationIdentifier = Utilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 168);
            UniqueId = Utilities.ToUInt64LittleEndian(buffer, offset + 200);
            ExtendedAttributesLength = Utilities.ToInt32LittleEndian(buffer, offset + 208);
            AllocationDescriptorsLength = Utilities.ToInt32LittleEndian(buffer, offset + 212);
            ExtendedAttributes = Utilities.ToByteArray(buffer, offset + 216, ExtendedAttributesLength);
            AllocationDescriptors = Utilities.ToByteArray(buffer, offset + 216 + ExtendedAttributesLength, AllocationDescriptorsLength);

            return (int)(216 + ExtendedAttributesLength + AllocationDescriptorsLength);
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public override int Size
        {
            get { throw new NotImplementedException(); }
        }
    }
}

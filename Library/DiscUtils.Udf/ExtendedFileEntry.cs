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

using System;
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal class ExtendedFileEntry : FileEntry, IByteArraySerializable
    {
        public DateTime CreationTime;
        public ulong ObjectSize;
        public LongAllocationDescriptor StreamDirectoryIcb;

        public override int Size
        {
            get { throw new NotImplementedException(); }
        }

        public override int ReadFrom(byte[] buffer, int offset)
        {
            DescriptorTag = EndianUtilities.ToStruct<DescriptorTag>(buffer, offset);
            InformationControlBlock = EndianUtilities.ToStruct<InformationControlBlock>(buffer, offset + 16);
            Uid = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 36);
            Gid = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 40);
            Permissions = (FilePermissions)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 44);
            FileLinkCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 48);
            RecordFormat = buffer[offset + 50];
            RecordDisplayAttributes = buffer[offset + 51];
            RecordLength = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 52);
            InformationLength = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 56);
            ObjectSize = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 64);
            LogicalBlocksRecorded = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 72);
            AccessTime = UdfUtilities.ParseTimestamp(buffer, offset + 80);
            ModificationTime = UdfUtilities.ParseTimestamp(buffer, offset + 92);
            CreationTime = UdfUtilities.ParseTimestamp(buffer, offset + 104);
            AttributeTime = UdfUtilities.ParseTimestamp(buffer, offset + 116);
            Checkpoint = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 128);
            ExtendedAttributeIcb = EndianUtilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 136);
            StreamDirectoryIcb = EndianUtilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 152);
            ImplementationIdentifier = EndianUtilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 168);
            UniqueId = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 200);
            ExtendedAttributesLength = EndianUtilities.ToInt32LittleEndian(buffer, offset + 208);
            AllocationDescriptorsLength = EndianUtilities.ToInt32LittleEndian(buffer, offset + 212);
            AllocationDescriptors = EndianUtilities.ToByteArray(buffer, offset + 216 + ExtendedAttributesLength,
                AllocationDescriptorsLength);

            byte[] eaData = EndianUtilities.ToByteArray(buffer, offset + 216, ExtendedAttributesLength);
            ExtendedAttributes = ReadExtendedAttributes(eaData);

            return 216 + ExtendedAttributesLength + AllocationDescriptorsLength;
        }

        public override void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
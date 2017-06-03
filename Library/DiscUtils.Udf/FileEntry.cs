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
using System.Collections.Generic;
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal class FileEntry : IByteArraySerializable
    {
        public DateTime AccessTime;
        public byte[] AllocationDescriptors;
        public int AllocationDescriptorsLength;
        public DateTime AttributeTime;
        public uint Checkpoint;
        public DescriptorTag DescriptorTag;
        public LongAllocationDescriptor ExtendedAttributeIcb;
        public List<ExtendedAttributeRecord> ExtendedAttributes;
        public int ExtendedAttributesLength;
        public ushort FileLinkCount;
        public uint Gid;
        public ImplementationEntityIdentifier ImplementationIdentifier;
        public InformationControlBlock InformationControlBlock;
        public ulong InformationLength;
        public ulong LogicalBlocksRecorded;
        public DateTime ModificationTime;
        public FilePermissions Permissions;
        public byte RecordDisplayAttributes;
        public byte RecordFormat;
        public uint RecordLength;
        public uint Uid;
        public ulong UniqueId;

        public virtual int Size
        {
            get { throw new NotImplementedException(); }
        }

        public virtual int ReadFrom(byte[] buffer, int offset)
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
            LogicalBlocksRecorded = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 64);
            AccessTime = UdfUtilities.ParseTimestamp(buffer, offset + 72);
            ModificationTime = UdfUtilities.ParseTimestamp(buffer, offset + 84);
            AttributeTime = UdfUtilities.ParseTimestamp(buffer, offset + 96);
            Checkpoint = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 108);
            ExtendedAttributeIcb = EndianUtilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 112);
            ImplementationIdentifier = EndianUtilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 128);
            UniqueId = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 160);
            ExtendedAttributesLength = EndianUtilities.ToInt32LittleEndian(buffer, offset + 168);
            AllocationDescriptorsLength = EndianUtilities.ToInt32LittleEndian(buffer, offset + 172);
            AllocationDescriptors = EndianUtilities.ToByteArray(buffer, offset + 176 + ExtendedAttributesLength,
                AllocationDescriptorsLength);

            byte[] eaData = EndianUtilities.ToByteArray(buffer, offset + 176, ExtendedAttributesLength);
            ExtendedAttributes = ReadExtendedAttributes(eaData);

            return 176 + ExtendedAttributesLength + AllocationDescriptorsLength;
        }

        public virtual void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        protected static List<ExtendedAttributeRecord> ReadExtendedAttributes(byte[] eaData)
        {
            if (eaData != null && eaData.Length != 0)
            {
                DescriptorTag eaTag = new DescriptorTag();
                eaTag.ReadFrom(eaData, 0);

                int implAttrLocation = EndianUtilities.ToInt32LittleEndian(eaData, 16);
                int appAttrLocation = EndianUtilities.ToInt32LittleEndian(eaData, 20);

                List<ExtendedAttributeRecord> extendedAttrs = new List<ExtendedAttributeRecord>();
                int pos = 24;
                while (pos < eaData.Length)
                {
                    ExtendedAttributeRecord ea;

                    if (pos >= implAttrLocation)
                    {
                        ea = new ImplementationUseExtendedAttributeRecord();
                    }
                    else
                    {
                        ea = new ExtendedAttributeRecord();
                    }

                    int numRead = ea.ReadFrom(eaData, pos);
                    extendedAttrs.Add(ea);

                    pos += numRead;
                }

                return extendedAttrs;
            }
            return null;
        }
    }
}
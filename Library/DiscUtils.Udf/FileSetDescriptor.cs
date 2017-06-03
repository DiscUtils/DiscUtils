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
    internal class FileSetDescriptor : IByteArraySerializable
    {
        public string AbstractFileIdentifier;
        public uint CharacterSetList;
        public string CopyrightFileIdentifier;
        public DescriptorTag DescriptorTag;
        public DomainEntityIdentifier DomainIdentifier;
        public CharacterSetSpecification FileSetCharset;
        public uint FileSetDescriptorNumber;
        public string FileSetIdentifier;
        public uint FileSetNumber;
        public ushort InterchangeLevel;
        public string LogicalVolumeIdentifier;
        public CharacterSetSpecification LogicalVolumeIdentifierCharset;
        public uint MaximumCharacterSetList;
        public ushort MaximumInterchangeLevel;
        public LongAllocationDescriptor NextExtent;
        public DateTime RecordingTime;
        public LongAllocationDescriptor RootDirectoryIcb;
        public LongAllocationDescriptor SystemStreamDirectoryIcb;

        public int Size
        {
            get { return 512; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            DescriptorTag = EndianUtilities.ToStruct<DescriptorTag>(buffer, offset);
            RecordingTime = UdfUtilities.ParseTimestamp(buffer, offset + 16);
            InterchangeLevel = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 28);
            MaximumInterchangeLevel = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 30);
            CharacterSetList = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 32);
            MaximumCharacterSetList = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 36);
            FileSetNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 40);
            FileSetDescriptorNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 44);
            LogicalVolumeIdentifierCharset = EndianUtilities.ToStruct<CharacterSetSpecification>(buffer, offset + 48);
            LogicalVolumeIdentifier = UdfUtilities.ReadDString(buffer, offset + 112, 128);
            FileSetCharset = EndianUtilities.ToStruct<CharacterSetSpecification>(buffer, offset + 240);
            FileSetIdentifier = UdfUtilities.ReadDString(buffer, offset + 304, 32);
            CopyrightFileIdentifier = UdfUtilities.ReadDString(buffer, offset + 336, 32);
            AbstractFileIdentifier = UdfUtilities.ReadDString(buffer, offset + 368, 32);
            RootDirectoryIcb = EndianUtilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 400);
            DomainIdentifier = EndianUtilities.ToStruct<DomainEntityIdentifier>(buffer, offset + 416);
            NextExtent = EndianUtilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 448);
            SystemStreamDirectoryIcb = EndianUtilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 464);

            return 512;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
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
    internal class FileSetDescriptor : IByteArraySerializable
    {
        public DescriptorTag DescriptorTag;
        public DateTime RecordingTime;
        public ushort InterchangeLevel;
        public ushort MaximumInterchangeLevel;
        public uint CharacterSetList;
        public uint MaximumCharacterSetList;
        public uint FileSetNumber;
        public uint FileSetDescriptorNumber;
        public CharacterSetSpecification LogicalVolumeIdentifierCharset;
        public string LogicalVolumeIdentifier;
        public CharacterSetSpecification FileSetCharset;
        public string FileSetIdentifier;
        public string CopyrightFileIdentifier;
        public string AbstractFileIdentifier;
        public LongAllocationDescriptor RootDirectoryIcb;
        public DomainEntityIdentifier DomainIdentifier;
        public LongAllocationDescriptor NextExtent;
        public LongAllocationDescriptor SystemStreamDirectoryIcb;

        int IByteArraySerializable.ReadFrom(byte[] buffer, int offset)
        {
            DescriptorTag = Utilities.ToStruct<DescriptorTag>(buffer, offset);
            RecordingTime = UdfUtilities.ParseTimestamp(buffer, offset + 16);
            InterchangeLevel = Utilities.ToUInt16LittleEndian(buffer, offset + 28);
            MaximumInterchangeLevel = Utilities.ToUInt16LittleEndian(buffer, offset + 30);
            CharacterSetList = Utilities.ToUInt32LittleEndian(buffer, offset + 32);
            MaximumCharacterSetList = Utilities.ToUInt32LittleEndian(buffer, offset + 36);
            FileSetNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 40);
            FileSetDescriptorNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 44);
            LogicalVolumeIdentifierCharset = Utilities.ToStruct<CharacterSetSpecification>(buffer, offset + 48);
            LogicalVolumeIdentifier = UdfUtilities.ReadDString(buffer, offset + 112, 128);
            FileSetCharset = Utilities.ToStruct<CharacterSetSpecification>(buffer, offset + 240);
            FileSetIdentifier = UdfUtilities.ReadDString(buffer, offset + 304, 32);
            CopyrightFileIdentifier = UdfUtilities.ReadDString(buffer, offset + 336, 32);
            AbstractFileIdentifier = UdfUtilities.ReadDString(buffer, offset + 368, 32);
            RootDirectoryIcb = Utilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 400);
            DomainIdentifier = Utilities.ToStruct<DomainEntityIdentifier>(buffer, offset + 416);
            NextExtent = Utilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 448);
            SystemStreamDirectoryIcb = Utilities.ToStruct<LongAllocationDescriptor>(buffer, offset + 464);

            return 512;
        }

        void IByteArraySerializable.WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        int IByteArraySerializable.Size
        {
            get { return 512; }
        }
    }
}

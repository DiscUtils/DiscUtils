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
    internal class PartitionDescriptor : BaseTaggedDescriptor<PartitionDescriptor>
    {
        public uint VolumeDescriptorSequenceNumber;
        public ushort PartitionFlags;
        public ushort PartitionNumber;
        public EntityIdentifier PartitionContents;
        public byte[] PartitionContentsUse;
        public uint AccessType;
        public uint PartitionStartingLocation;
        public uint PartitionLength;
        public EntityIdentifier ImplementationIdentifier;
        public byte[] ImplementationUse;

        public PartitionDescriptor()
            : base(TagIdentifier.PartitionDescriptor)
        {
        }

        public override int Parse(byte[] buffer, int offset)
        {
            VolumeDescriptorSequenceNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 16);
            PartitionFlags = Utilities.ToUInt16LittleEndian(buffer, offset + 20);
            PartitionNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 22);
            PartitionContents = Utilities.ToStruct<ApplicationEntityIdentifier>(buffer, offset + 24);
            PartitionContentsUse = Utilities.ToByteArray(buffer, offset + 56, 128);
            AccessType = Utilities.ToUInt32LittleEndian(buffer, offset + 184);
            PartitionStartingLocation = Utilities.ToUInt32LittleEndian(buffer, offset + 188);
            PartitionLength = Utilities.ToUInt32LittleEndian(buffer, offset + 192);
            ImplementationIdentifier = Utilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 196);
            ImplementationUse = Utilities.ToByteArray(buffer, offset + 228, 128);

            return 512;
        }
    }
}

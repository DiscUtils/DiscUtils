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

using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal class PartitionDescriptor : TaggedDescriptor<PartitionDescriptor>
    {
        public uint AccessType;
        public EntityIdentifier ImplementationIdentifier;
        public byte[] ImplementationUse;
        public EntityIdentifier PartitionContents;
        public byte[] PartitionContentsUse;
        public ushort PartitionFlags;
        public uint PartitionLength;
        public ushort PartitionNumber;
        public uint PartitionStartingLocation;
        public uint VolumeDescriptorSequenceNumber;

        public PartitionDescriptor()
            : base(TagIdentifier.PartitionDescriptor) {}

        public override int Parse(byte[] buffer, int offset)
        {
            VolumeDescriptorSequenceNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 16);
            PartitionFlags = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 20);
            PartitionNumber = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 22);
            PartitionContents = EndianUtilities.ToStruct<ApplicationEntityIdentifier>(buffer, offset + 24);
            PartitionContentsUse = EndianUtilities.ToByteArray(buffer, offset + 56, 128);
            AccessType = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 184);
            PartitionStartingLocation = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 188);
            PartitionLength = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 192);
            ImplementationIdentifier = EndianUtilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 196);
            ImplementationUse = EndianUtilities.ToByteArray(buffer, offset + 228, 128);

            return 512;
        }
    }
}
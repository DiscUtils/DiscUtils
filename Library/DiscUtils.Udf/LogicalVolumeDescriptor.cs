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
    internal sealed class LogicalVolumeDescriptor : TaggedDescriptor<LogicalVolumeDescriptor>
    {
        public byte[] DescriptorCharset;
        public EntityIdentifier DomainIdentifier;
        public EntityIdentifier ImplementationIdentifier;
        public byte[] ImplementationUse;
        public ExtentDescriptor IntegritySequenceExtent;
        public uint LogicalBlockSize;
        public byte[] LogicalVolumeContentsUse;
        public string LogicalVolumeIdentifier;
        public uint MapTableLength;
        public uint NumPartitionMaps;
        public PartitionMap[] PartitionMaps;
        public uint VolumeDescriptorSequenceNumber;

        public LogicalVolumeDescriptor()
            : base(TagIdentifier.LogicalVolumeDescriptor) {}

        public LongAllocationDescriptor FileSetDescriptorLocation
        {
            get
            {
                LongAllocationDescriptor lad = new LongAllocationDescriptor();
                lad.ReadFrom(LogicalVolumeContentsUse, 0);
                return lad;
            }
        }

        public override int Parse(byte[] buffer, int offset)
        {
            VolumeDescriptorSequenceNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 16);
            DescriptorCharset = EndianUtilities.ToByteArray(buffer, offset + 20, 64);
            LogicalVolumeIdentifier = UdfUtilities.ReadDString(buffer, offset + 84, 128);
            LogicalBlockSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 212);
            DomainIdentifier = EndianUtilities.ToStruct<DomainEntityIdentifier>(buffer, offset + 216);
            LogicalVolumeContentsUse = EndianUtilities.ToByteArray(buffer, offset + 248, 16);
            MapTableLength = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 264);
            NumPartitionMaps = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 268);
            ImplementationIdentifier = EndianUtilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 272);
            ImplementationUse = EndianUtilities.ToByteArray(buffer, offset + 304, 128);
            IntegritySequenceExtent = new ExtentDescriptor();
            IntegritySequenceExtent.ReadFrom(buffer, offset + 432);

            int pmOffset = 0;
            PartitionMaps = new PartitionMap[NumPartitionMaps];
            for (int i = 0; i < NumPartitionMaps; ++i)
            {
                PartitionMaps[i] = PartitionMap.CreateFrom(buffer, offset + 440 + pmOffset);
                pmOffset += PartitionMaps[i].Size;
            }

            return 440 + (int)MapTableLength;
        }
    }
}
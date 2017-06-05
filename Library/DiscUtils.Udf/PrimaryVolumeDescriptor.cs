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
    internal sealed class PrimaryVolumeDescriptor : TaggedDescriptor<PrimaryVolumeDescriptor>
    {
        public EntityIdentifier ApplicationIdentifier;
        public uint CharacterSetList;
        public CharacterSetSpecification DescriptorCharSet;
        public CharacterSetSpecification ExplanatoryCharSet;
        public ushort Flags;
        public EntityIdentifier ImplementationIdentifier;
        public byte[] ImplementationUse;
        public ushort InterchangeLevel;
        public uint MaxCharacterSetList;
        public ushort MaxInterchangeLevel;
        public ushort MaxVolumeSquenceNumber;
        public uint PredecessorVolumeDescriptorSequenceLocation;
        public uint PrimaryVolumeDescriptorNumber;
        public DateTime RecordingTime;
        public ExtentDescriptor VolumeAbstractExtent;
        public ExtentDescriptor VolumeCopyrightNoticeExtent;
        public uint VolumeDescriptorSequenceNumber;
        public string VolumeIdentifier;
        public ushort VolumeSequenceNumber;
        public string VolumeSetIdentifier;

        public PrimaryVolumeDescriptor() :
            base(TagIdentifier.PrimaryVolumeDescriptor) {}

        public override int Parse(byte[] buffer, int offset)
        {
            VolumeDescriptorSequenceNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 16);
            PrimaryVolumeDescriptorNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 20);
            VolumeIdentifier = UdfUtilities.ReadDString(buffer, offset + 24, 32);
            VolumeSequenceNumber = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 56);
            MaxVolumeSquenceNumber = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 58);
            InterchangeLevel = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 60);
            MaxInterchangeLevel = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 62);
            CharacterSetList = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 64);
            MaxCharacterSetList = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 68);
            VolumeSetIdentifier = UdfUtilities.ReadDString(buffer, offset + 72, 128);
            DescriptorCharSet = EndianUtilities.ToStruct<CharacterSetSpecification>(buffer, offset + 200);
            ExplanatoryCharSet = EndianUtilities.ToStruct<CharacterSetSpecification>(buffer, offset + 264);
            VolumeAbstractExtent = new ExtentDescriptor();
            VolumeAbstractExtent.ReadFrom(buffer, offset + 328);
            VolumeCopyrightNoticeExtent = new ExtentDescriptor();
            VolumeCopyrightNoticeExtent.ReadFrom(buffer, offset + 336);
            ApplicationIdentifier = EndianUtilities.ToStruct<ApplicationEntityIdentifier>(buffer, offset + 344);
            RecordingTime = UdfUtilities.ParseTimestamp(buffer, offset + 376);
            ImplementationIdentifier = EndianUtilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 388);
            ImplementationUse = EndianUtilities.ToByteArray(buffer, offset + 420, 64);
            PredecessorVolumeDescriptorSequenceLocation = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 484);
            Flags = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 488);

            return 512;
        }
    }
}
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
using System.IO;

namespace DiscUtils.Udf
{
    internal sealed class PrimaryVolumeDescriptor : BaseTaggedDescriptor<PrimaryVolumeDescriptor>
    {
        public uint VolumeDescriptorSequenceNumber;
        public uint PrimaryVolumeDescriptorNumber;
        public string VolumeIdentifier;
        public ushort VolumeSequenceNumber;
        public ushort MaxVolumeSquenceNumber;
        public ushort InterchangeLevel;
        public ushort MaxInterchangeLevel;
        public uint CharacterSetList;
        public uint MaxCharacterSetList;
        public string VolumeSetIdentifier;
        public CharacterSetSpecification DescriptorCharSet;
        public CharacterSetSpecification ExplanatoryCharSet;
        public ExtentDescriptor VolumeAbstractExtent;
        public ExtentDescriptor VolumeCopyrightNoticeExtent;
        public EntityIdentifier ApplicationIdentifier;
        public DateTime RecordingTime;
        public EntityIdentifier ImplementationIdentifier;
        public byte[] ImplementationUse;
        public uint PredecessorVolumeDescriptorSequenceLocation;
        public ushort Flags;

        public PrimaryVolumeDescriptor() :
            base(TagIdentifier.PrimaryVolumeDescriptor)
        {
        }

        public override int Parse(byte[] buffer, int offset)
        {
            VolumeDescriptorSequenceNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 16);
            PrimaryVolumeDescriptorNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 20);
            VolumeIdentifier = UdfUtilities.ReadDString(buffer, offset + 24, 32);
            VolumeSequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 56);
            MaxVolumeSquenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 58);
            InterchangeLevel = Utilities.ToUInt16LittleEndian(buffer, offset + 60);
            MaxInterchangeLevel = Utilities.ToUInt16LittleEndian(buffer, offset + 62);
            CharacterSetList = Utilities.ToUInt32LittleEndian(buffer, offset + 64);
            MaxCharacterSetList = Utilities.ToUInt32LittleEndian(buffer, offset + 68);
            VolumeSetIdentifier = UdfUtilities.ReadDString(buffer, offset + 72, 128);
            DescriptorCharSet = Utilities.ToStruct<CharacterSetSpecification>(buffer, offset + 200);
            ExplanatoryCharSet = Utilities.ToStruct<CharacterSetSpecification>(buffer, offset + 264);
            VolumeAbstractExtent = new ExtentDescriptor();
            VolumeAbstractExtent.ReadFrom(buffer, offset + 328);
            VolumeCopyrightNoticeExtent = new ExtentDescriptor();
            VolumeCopyrightNoticeExtent.ReadFrom(buffer, offset + 336);
            ApplicationIdentifier = Utilities.ToStruct<ApplicationEntityIdentifier>(buffer, offset + 344);
            RecordingTime = UdfUtilities.ParseTimestamp(buffer, offset + 376);
            ImplementationIdentifier = Utilities.ToStruct<ImplementationEntityIdentifier>(buffer, offset + 388);
            ImplementationUse = Utilities.ToByteArray(buffer, offset + 420, 64);
            PredecessorVolumeDescriptorSequenceLocation = Utilities.ToUInt32LittleEndian(buffer, offset + 484);
            Flags = Utilities.ToUInt16LittleEndian(buffer, offset + 488);

            return 512;
        }
    }
}

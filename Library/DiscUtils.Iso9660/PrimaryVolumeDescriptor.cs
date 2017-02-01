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
using System.Text;
using DiscUtils.Internal;

namespace DiscUtils.Iso9660
{
    internal class PrimaryVolumeDescriptor : CommonVolumeDescriptor
    {
        public PrimaryVolumeDescriptor(byte[] src, int offset)
            : base(src, offset, Encoding.ASCII) {}

        public PrimaryVolumeDescriptor(
            uint volumeSpaceSize,
            uint pathTableSize,
            uint typeLPathTableLocation,
            uint typeMPathTableLocation,
            uint rootDirExtentLocation,
            uint rootDirDataLength,
            DateTime buildTime)
            : base(
                VolumeDescriptorType.Primary, 1, volumeSpaceSize, pathTableSize, typeLPathTableLocation,
                typeMPathTableLocation, rootDirExtentLocation, rootDirDataLength, buildTime, Encoding.ASCII) {}

        internal override void WriteTo(byte[] buffer, int offset)
        {
            base.WriteTo(buffer, offset);
            IsoUtilities.WriteAChars(buffer, offset + 8, 32, SystemIdentifier);
            IsoUtilities.WriteString(buffer, offset + 40, 32, true, VolumeIdentifier, Encoding.ASCII, true);
            IsoUtilities.ToBothFromUInt32(buffer, offset + 80, VolumeSpaceSize);
            IsoUtilities.ToBothFromUInt16(buffer, offset + 120, VolumeSetSize);
            IsoUtilities.ToBothFromUInt16(buffer, offset + 124, VolumeSequenceNumber);
            IsoUtilities.ToBothFromUInt16(buffer, offset + 128, LogicalBlockSize);
            IsoUtilities.ToBothFromUInt32(buffer, offset + 132, PathTableSize);
            IsoUtilities.ToBytesFromUInt32(buffer, offset + 140, TypeLPathTableLocation);
            IsoUtilities.ToBytesFromUInt32(buffer, offset + 144, OptionalTypeLPathTableLocation);
            IsoUtilities.ToBytesFromUInt32(buffer, offset + 148, Utilities.BitSwap(TypeMPathTableLocation));
            IsoUtilities.ToBytesFromUInt32(buffer, offset + 152, Utilities.BitSwap(OptionalTypeMPathTableLocation));
            RootDirectory.WriteTo(buffer, offset + 156, Encoding.ASCII);
            IsoUtilities.WriteDChars(buffer, offset + 190, 129, VolumeSetIdentifier);
            IsoUtilities.WriteAChars(buffer, offset + 318, 129, PublisherIdentifier);
            IsoUtilities.WriteAChars(buffer, offset + 446, 129, DataPreparerIdentifier);
            IsoUtilities.WriteAChars(buffer, offset + 574, 129, ApplicationIdentifier);
            IsoUtilities.WriteDChars(buffer, offset + 702, 37, CopyrightFileIdentifier); // FIXME!!
            IsoUtilities.WriteDChars(buffer, offset + 739, 37, AbstractFileIdentifier); // FIXME!!
            IsoUtilities.WriteDChars(buffer, offset + 776, 37, BibliographicFileIdentifier); // FIXME!!
            IsoUtilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 813, CreationDateAndTime);
            IsoUtilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 830, ModificationDateAndTime);
            IsoUtilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 847, ExpirationDateAndTime);
            IsoUtilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 864, EffectiveDateAndTime);
            buffer[offset + 881] = FileStructureVersion;
        }
    }
}
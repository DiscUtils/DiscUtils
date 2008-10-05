//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils.Iso9660
{
    internal class PrimaryVolumeDescriptor : CommonVolumeDescriptor
    {
        public PrimaryVolumeDescriptor(byte[] src, int offset)
            : base(src, offset, Encoding.ASCII)
        {
        }

        public PrimaryVolumeDescriptor(
            uint volumeSpaceSize,
            uint pathTableSize,
            uint typeLPathTableLocation,
            uint typeMPathTableLocation,
            uint rootDirExtentLocation,
            uint rootDirDataLength,
            DateTime buildTime)
            : base(VolumeDescriptorType.Primary, 1, volumeSpaceSize, pathTableSize, typeLPathTableLocation, typeMPathTableLocation, rootDirExtentLocation, rootDirDataLength, buildTime, Encoding.ASCII)
        {
        }

        internal override void WriteTo(byte[] buffer, int offset)
        {
            base.WriteTo(buffer, offset);
            Utilities.WriteAChars(buffer, offset + 8, 32, SystemIdentifier);
            Utilities.WriteAChars(buffer, offset + 40, 32, VolumeIdentifier);
            Utilities.ToBothFromUInt32(buffer, offset + 80, VolumeSpaceSize);
            Utilities.ToBothFromUInt16(buffer, offset + 120, VolumeSetSize);
            Utilities.ToBothFromUInt16(buffer, offset + 124, VolumeSequenceNumber);
            Utilities.ToBothFromUInt16(buffer, offset + 128, LogicalBlockSize);
            Utilities.ToBothFromUInt32(buffer, offset + 132, PathTableSize);
            Utilities.ToBytesFromUInt32(buffer, offset + 140, TypeLPathTableLocation);
            Utilities.ToBytesFromUInt32(buffer, offset + 144, OptionalTypeLPathTableLocation);
            Utilities.ToBytesFromUInt32(buffer, offset + 148, Utilities.ByteSwap(TypeMPathTableLocation));
            Utilities.ToBytesFromUInt32(buffer, offset + 152, Utilities.ByteSwap(OptionalTypeMPathTableLocation));
            RootDirectory.WriteTo(buffer, offset + 156, Encoding.ASCII);
            Utilities.WriteDChars(buffer, offset + 190, 129, VolumeSetIdentifier);
            Utilities.WriteAChars(buffer, offset + 318, 129, PublisherIdentifier);
            Utilities.WriteAChars(buffer, offset + 446, 129, DataPreparerIdentifier);
            Utilities.WriteAChars(buffer, offset + 574, 129, ApplicationIdentifier);
            Utilities.WriteDChars(buffer, offset + 702, 37, CopyrightFileIdentifier); // FIXME!!
            Utilities.WriteDChars(buffer, offset + 739, 37, AbstractFileIdentifier); // FIXME!!
            Utilities.WriteDChars(buffer, offset + 776, 37, BibliographicFileIdentifier); // FIXME!!
            Utilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 813, CreationDateAndTime);
            Utilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 830, ModificationDateAndTime);
            Utilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 847, ExpirationDateAndTime);
            Utilities.ToVolumeDescriptorTimeFromUTC(buffer, offset + 864, EffectiveDateAndTime);
            buffer[offset + 881] = FileStructureVersion;
        }
    }

}

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

namespace DiscUtils.Udf
{
    internal sealed class MetadataPartitionMap : PartitionMap
    {
        public ushort VolumeSequenceNumber;
        public ushort PartitionNumber;
        public uint MetadataFileLocation;
        public uint MetadataMirrorFileLocation;
        public uint MetadataBitmapFileLocation;
        public uint AllocationUnitSize;
        public ushort AlignmentUnitSize;
        public byte Flags;

        public override int Size
        {
            get { return 64; }
        }

        protected override int Parse(byte[] buffer, int offset)
        {
            VolumeSequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 36);
            PartitionNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 38);
            MetadataFileLocation = Utilities.ToUInt32LittleEndian(buffer, offset + 40);
            MetadataMirrorFileLocation = Utilities.ToUInt32LittleEndian(buffer, offset + 44);
            MetadataBitmapFileLocation = Utilities.ToUInt32LittleEndian(buffer, offset + 48);
            AllocationUnitSize = Utilities.ToUInt32LittleEndian(buffer, offset + 52);
            AlignmentUnitSize = Utilities.ToUInt16LittleEndian(buffer, offset + 56);
            Flags = buffer[offset + 58];

            return 64;
        }
    }
}

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
using System.IO;

namespace DiscUtils.Udf
{
    internal abstract class PartitionMap : IByteArraySerializable
    {
        public byte Type;

        public int ReadFrom(byte[] buffer, int offset)
        {
            Type = buffer[offset];
            return Parse(buffer, offset);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        protected abstract int Parse(byte[] buffer, int offset);
        public abstract int Size { get; }

        public static PartitionMap CreateFrom(byte[] buffer, int offset)
        {
            PartitionMap result = null;

            byte type = buffer[offset];
            if (type == 1)
            {
                result = new Type1PartitionMap();
            }
            else if(type == 2)
            {
                EntityIdentifier id = Utilities.ToStruct<UdfEntityIdentifier>(buffer, offset + 4);
                switch (id.Identifier)
                {
                    case "*UDF Virtual Partition":
                        result = new VirtualPartitionMap();
                        break;
                    case "*UDF Sparable Partition":
                        result = new SparablePartitionMap();
                        break;
                    case "*UDF Metadata Partition":
                        result = new MetadataPartitionMap();
                        break;
                    default:
                        throw new InvalidDataException("Unrecognized partition map entity id: " + id);
                }
            }


            if (result != null)
            {
                result.ReadFrom(buffer, offset);
            }

            return result;
        }
    }

    internal sealed class Type1PartitionMap : PartitionMap
    {
        public ushort VolumeSequenceNumber;
        public ushort PartitionNumber;

        protected override int Parse(byte[] buffer, int offset)
        {
            VolumeSequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 2);
            PartitionNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 4);
            return 6;
        }

        public override int Size
        {
            get { return 6; }
        }
    }

    internal sealed class VirtualPartitionMap : PartitionMap
    {
        public ushort VolumeSequenceNumber;
        public ushort PartitionNumber;

        protected override int Parse(byte[] buffer, int offset)
        {
            VolumeSequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 36);
            PartitionNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 38);
            return 64;
        }

        public override int Size
        {
            get { return 64; }
        }
    }

    internal sealed class SparablePartitionMap : PartitionMap
    {
        public ushort VolumeSequenceNumber;
        public ushort PartitionNumber;
        public ushort PacketLength;
        public byte NumSparingTables;
        public uint SparingTableSize;
        public uint[] LocationsOfSparingTables;

        protected override int Parse(byte[] buffer, int offset)
        {
            VolumeSequenceNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 36);
            PartitionNumber = Utilities.ToUInt16LittleEndian(buffer, offset + 38);
            PacketLength = Utilities.ToUInt16LittleEndian(buffer, offset + 40);
            NumSparingTables = buffer[offset + 42];
            SparingTableSize = Utilities.ToUInt32LittleEndian(buffer, offset + 44);
            LocationsOfSparingTables = new uint[NumSparingTables];
            for (int i = 0; i < NumSparingTables; ++i)
            {
                LocationsOfSparingTables[i] = Utilities.ToUInt32LittleEndian(buffer, offset + 48 + 4 * i);
            }

            return 64;
        }

        public override int Size
        {
            get { return 64; }
        }
    }

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

        public override int Size
        {
            get { return 64; }
        }
    }
}

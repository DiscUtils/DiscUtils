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

namespace DiscUtils.HfsPlus
{
    using System;

    internal sealed class VolumeHeader : IByteArraySerializable
    {
        public const ushort HfsPlusSignature = 0x482b;

        public ushort Signature;
        public ushort Version;
        public VolumeAttributes Attributes;
        public uint LastMountedVersion;
        public uint JournalInfoBlock;

        public DateTime CreateDate;
        public DateTime ModifyDate;
        public DateTime BackupDate;
        public DateTime CheckedDate;

        public uint FileCount;
        public uint FolderCount;

        public uint BlockSize;
        public uint TotalBlocks;
        public uint FreeBlocks;

        public uint NextAllocation;
        public uint ResourceClumpSize;
        public uint DataClumpSize;
        public CatalogNodeId NextCatalogId;

        public uint WriteCount;
        public ulong EncodingsBitmap;

        public uint[] FinderInfo;

        public ForkData AllocationFile;
        public ForkData ExtentsFile;
        public ForkData CatalogFile;
        public ForkData AttributesFile;
        public ForkData StartupFile;

        public int Size
        {
            get { return 512; }
        }

        public bool IsValid
        {
            get { return Signature == HfsPlusSignature; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Signature = Utilities.ToUInt16BigEndian(buffer, offset + 0);
            Version = Utilities.ToUInt16BigEndian(buffer, offset + 2);
            Attributes = (VolumeAttributes)Utilities.ToUInt32BigEndian(buffer, offset + 4);
            LastMountedVersion = Utilities.ToUInt32BigEndian(buffer, offset + 8);
            JournalInfoBlock = Utilities.ToUInt32BigEndian(buffer, offset + 12);

            CreateDate = HfsPlusUtilities.ReadHFSPlusDate(DateTimeKind.Local, buffer, offset + 16);
            ModifyDate = HfsPlusUtilities.ReadHFSPlusDate(DateTimeKind.Utc, buffer, offset + 20);
            BackupDate = HfsPlusUtilities.ReadHFSPlusDate(DateTimeKind.Utc, buffer, offset + 24);
            CheckedDate = HfsPlusUtilities.ReadHFSPlusDate(DateTimeKind.Utc, buffer, offset + 28);

            FileCount = Utilities.ToUInt32BigEndian(buffer, offset + 32);
            FolderCount = Utilities.ToUInt32BigEndian(buffer, offset + 36);

            BlockSize = Utilities.ToUInt32BigEndian(buffer, offset + 40);
            TotalBlocks = Utilities.ToUInt32BigEndian(buffer, offset + 44);
            FreeBlocks = Utilities.ToUInt32BigEndian(buffer, offset + 48);

            NextAllocation = Utilities.ToUInt32BigEndian(buffer, offset + 52);
            ResourceClumpSize = Utilities.ToUInt32BigEndian(buffer, offset + 56);
            DataClumpSize = Utilities.ToUInt32BigEndian(buffer, offset + 60);
            NextCatalogId = new CatalogNodeId(Utilities.ToUInt32BigEndian(buffer, offset + 64));

            WriteCount = Utilities.ToUInt32BigEndian(buffer, offset + 68);
            EncodingsBitmap = Utilities.ToUInt64BigEndian(buffer, offset + 72);

            FinderInfo = new uint[8];
            for (int i = 0; i < 8; ++i)
            {
                FinderInfo[i] = Utilities.ToUInt32BigEndian(buffer, offset + 80 + (i * 4));
            }

            AllocationFile = (ForkData)Utilities.ToStruct<ForkData>(buffer, offset + 112);
            ExtentsFile = (ForkData)Utilities.ToStruct<ForkData>(buffer, offset + 192);
            CatalogFile = (ForkData)Utilities.ToStruct<ForkData>(buffer, offset + 272);
            AttributesFile = (ForkData)Utilities.ToStruct<ForkData>(buffer, offset + 352);
            StartupFile = (ForkData)Utilities.ToStruct<ForkData>(buffer, offset + 432);

            return 512;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

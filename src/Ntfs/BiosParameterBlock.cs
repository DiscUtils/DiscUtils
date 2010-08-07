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

namespace DiscUtils.Ntfs
{
    internal class BiosParameterBlock
    {
        public string OemId;
        public ushort BytesPerSector;
        public byte SectorsPerCluster;
        public ushort ReservedSectors; // Must be 0
        public byte NumFats; // Must be 0
        public ushort FatRootEntriesCount; // Must be 0
        public ushort TotalSectors16; // Must be 0
        public byte Media; // Must be 0xF8
        public ushort FatSize16; // Must be 0
        public ushort SectorsPerTrack; // Value: 0x3F 0x00
        public ushort NumHeads; // Value: 0xFF 0x00
        public uint HiddenSectors; // Value: 0x3F 0x00 0x00 0x00
        public uint TotalSectors32; // Must be 0
        public byte BiosDriveNumber; // Value: 0x80 (first hard disk)
        public byte ChkDskFlags; // Value: 0x00
        public byte SignatureByte; // Value: 0x80
        public byte PaddingByte; // Value: 0x00
        public long TotalSectors64;
        public long MftCluster;
        public long MftMirrorCluster;
        public byte RawMftRecordSize;
        public byte RawIndexBufferSize;
        public ulong VolumeSerialNumber;

        internal static BiosParameterBlock Initialized(Geometry diskGeometry, int clusterSize, uint partitionStartLba, long partitionSizeLba, int mftRecordSize, int indexBufferSize)
        {
            BiosParameterBlock bpb = new BiosParameterBlock();
            bpb.OemId = "NTFS    ";
            bpb.BytesPerSector = Sizes.Sector;
            bpb.SectorsPerCluster = (byte)(clusterSize / bpb.BytesPerSector);
            bpb.ReservedSectors = 0;
            bpb.NumFats = 0;
            bpb.FatRootEntriesCount = 0;
            bpb.TotalSectors16 = 0;
            bpb.Media = 0xF8;
            bpb.FatSize16 = 0;
            bpb.SectorsPerTrack = (ushort)diskGeometry.SectorsPerTrack;
            bpb.NumHeads = (ushort)diskGeometry.HeadsPerCylinder;
            bpb.HiddenSectors = partitionStartLba;
            bpb.TotalSectors32 = 0;
            bpb.BiosDriveNumber = 0x80;
            bpb.ChkDskFlags = 0;
            bpb.SignatureByte = 0x80;
            bpb.PaddingByte = 0;
            bpb.TotalSectors64 = partitionSizeLba - 1;
            bpb.RawMftRecordSize = bpb.CodeRecordSize(mftRecordSize);
            bpb.RawIndexBufferSize = bpb.CodeRecordSize(indexBufferSize);
            bpb.VolumeSerialNumber = GenSerialNumber();

            return bpb;
        }

        internal static BiosParameterBlock FromBytes(byte[] bytes, int offset)
        {
            BiosParameterBlock bpb = new BiosParameterBlock();
            bpb.OemId = Utilities.BytesToString(bytes, offset + 0x03, 8);
            bpb.BytesPerSector = Utilities.ToUInt16LittleEndian(bytes, offset + 0x0B);
            bpb.SectorsPerCluster = bytes[offset + 0x0D];
            bpb.ReservedSectors = Utilities.ToUInt16LittleEndian(bytes, offset + 0x0E);
            bpb.NumFats = bytes[offset + 0x10];
            bpb.FatRootEntriesCount = Utilities.ToUInt16LittleEndian(bytes, offset + 0x11);
            bpb.TotalSectors16 = Utilities.ToUInt16LittleEndian(bytes, offset + 0x13);
            bpb.Media = bytes[offset + 0x15];
            bpb.FatSize16 = Utilities.ToUInt16LittleEndian(bytes, offset + 0x16);
            bpb.SectorsPerTrack = Utilities.ToUInt16LittleEndian(bytes, offset + 0x18);
            bpb.NumHeads = Utilities.ToUInt16LittleEndian(bytes, offset + 0x1A);
            bpb.HiddenSectors = Utilities.ToUInt32LittleEndian(bytes, offset + 0x1C);
            bpb.TotalSectors32 = Utilities.ToUInt32LittleEndian(bytes, offset + 0x20);
            bpb.BiosDriveNumber = bytes[offset + 0x24];
            bpb.ChkDskFlags = bytes[offset + 0x25];
            bpb.SignatureByte = bytes[offset + 0x26];
            bpb.PaddingByte = bytes[offset + 0x27];
            bpb.TotalSectors64 = Utilities.ToInt64LittleEndian(bytes, offset + 0x28);
            bpb.MftCluster = Utilities.ToInt64LittleEndian(bytes, offset + 0x30);
            bpb.MftMirrorCluster = Utilities.ToInt64LittleEndian(bytes, offset + 0x38);
            bpb.RawMftRecordSize = bytes[offset + 0x40];
            bpb.RawIndexBufferSize = bytes[offset + 0x44];
            bpb.VolumeSerialNumber = Utilities.ToUInt64LittleEndian(bytes, offset + 0x48);

            return bpb;
        }

        internal void ToBytes(byte[] buffer, int offset)
        {
            Utilities.StringToBytes(OemId, buffer, offset + 0x03, 8);
            Utilities.WriteBytesLittleEndian(BytesPerSector, buffer, offset + 0x0B);
            buffer[offset + 0x0D] = SectorsPerCluster;
            Utilities.WriteBytesLittleEndian(ReservedSectors, buffer, offset + 0x0E);
            buffer[offset + 0x10] = NumFats;
            Utilities.WriteBytesLittleEndian(FatRootEntriesCount, buffer, offset + 0x11);
            Utilities.WriteBytesLittleEndian(TotalSectors16, buffer, offset + 0x13);
            buffer[offset + 0x15] = Media;
            Utilities.WriteBytesLittleEndian(FatSize16, buffer, offset + 0x16);
            Utilities.WriteBytesLittleEndian(SectorsPerTrack, buffer, offset + 0x18);
            Utilities.WriteBytesLittleEndian(NumHeads, buffer, offset + 0x1A);
            Utilities.WriteBytesLittleEndian(HiddenSectors, buffer, offset + 0x1C);
            Utilities.WriteBytesLittleEndian(TotalSectors32, buffer, offset + 0x20);
            buffer[offset + 0x24] = BiosDriveNumber;
            buffer[offset + 0x25] = ChkDskFlags;
            buffer[offset + 0x26] = SignatureByte;
            buffer[offset + 0x27] = PaddingByte;
            Utilities.WriteBytesLittleEndian(TotalSectors64, buffer, offset + 0x28);
            Utilities.WriteBytesLittleEndian(MftCluster, buffer, offset + 0x30);
            Utilities.WriteBytesLittleEndian(MftMirrorCluster, buffer, offset + 0x38);
            buffer[offset + 0x40] = RawMftRecordSize;
            buffer[offset + 0x44] = RawIndexBufferSize;
            Utilities.WriteBytesLittleEndian(VolumeSerialNumber, buffer, offset + 0x48);
        }

        public int MftRecordSize
        {
            get { return CalcRecordSize(RawMftRecordSize); }
        }

        public int IndexBufferSize
        {
            get { return CalcRecordSize(RawIndexBufferSize); }
        }

        public int BytesPerCluster
        {
            get { return ((int)BytesPerSector) * ((int)SectorsPerCluster); }
        }

        internal int CalcRecordSize(byte rawSize)
        {
            if ((rawSize & 0x80) != 0)
            {
                return 1 << (-(sbyte)rawSize);
            }
            else
            {
                return rawSize * SectorsPerCluster * BytesPerSector;
            }
        }

        private byte CodeRecordSize(int size)
        {
            if (size >= BytesPerCluster)
            {
                return (byte)(size / BytesPerCluster);
            }
            else
            {
                sbyte val = 0;
                while (size != 1)
                {
                    size = (size >> 1) & 0x7FFFFFFF;
                    val++;
                }
                return (byte)-val;
            }
        }

        private static ulong GenSerialNumber()
        {
            byte[] buffer = new byte[8];
            Random rng = new Random();
            rng.NextBytes(buffer);
            return Utilities.ToUInt64LittleEndian(buffer, 0);
        }
    }
}

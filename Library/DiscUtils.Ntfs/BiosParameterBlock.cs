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
using System.Globalization;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal class BiosParameterBlock
    {
        public byte BiosDriveNumber; // Value: 0x80 (first hard disk)
        public ushort BytesPerSector;
        public byte ChkDskFlags; // Value: 0x00
        public ushort FatRootEntriesCount; // Must be 0
        public ushort FatSize16; // Must be 0
        public uint HiddenSectors; // Value: 0x3F 0x00 0x00 0x00
        public byte Media; // Must be 0xF8
        public long MftCluster;
        public long MftMirrorCluster;
        public byte NumFats; // Must be 0
        public ushort NumHeads; // Value: 0xFF 0x00
        public string OemId;
        public byte PaddingByte; // Value: 0x00
        public byte RawIndexBufferSize;
        public byte RawMftRecordSize;
        public ushort ReservedSectors; // Must be 0
        public byte SectorsPerCluster;
        public ushort SectorsPerTrack; // Value: 0x3F 0x00
        public byte SignatureByte; // Value: 0x80
        public ushort TotalSectors16; // Must be 0
        public uint TotalSectors32; // Must be 0
        public long TotalSectors64;
        public ulong VolumeSerialNumber;

        public int BytesPerCluster
        {
            get { return BytesPerSector * SectorsPerCluster; }
        }

        public int IndexBufferSize
        {
            get { return CalcRecordSize(RawIndexBufferSize); }
        }

        public int MftRecordSize
        {
            get { return CalcRecordSize(RawMftRecordSize); }
        }

        public void Dump(TextWriter writer, string linePrefix)
        {
            writer.WriteLine(linePrefix + "BIOS PARAMETER BLOCK (BPB)");
            writer.WriteLine(linePrefix + "                OEM ID: " + OemId);
            writer.WriteLine(linePrefix + "      Bytes per Sector: " + BytesPerSector);
            writer.WriteLine(linePrefix + "   Sectors per Cluster: " + SectorsPerCluster);
            writer.WriteLine(linePrefix + "      Reserved Sectors: " + ReservedSectors);
            writer.WriteLine(linePrefix + "                # FATs: " + NumFats);
            writer.WriteLine(linePrefix + "    # FAT Root Entries: " + FatRootEntriesCount);
            writer.WriteLine(linePrefix + "   Total Sectors (16b): " + TotalSectors16);
            writer.WriteLine(linePrefix + "                 Media: " + Media.ToString("X", CultureInfo.InvariantCulture) +
                             "h");
            writer.WriteLine(linePrefix + "        FAT size (16b): " + FatSize16);
            writer.WriteLine(linePrefix + "     Sectors per Track: " + SectorsPerTrack);
            writer.WriteLine(linePrefix + "               # Heads: " + NumHeads);
            writer.WriteLine(linePrefix + "        Hidden Sectors: " + HiddenSectors);
            writer.WriteLine(linePrefix + "   Total Sectors (32b): " + TotalSectors32);
            writer.WriteLine(linePrefix + "     BIOS Drive Number: " + BiosDriveNumber);
            writer.WriteLine(linePrefix + "          Chkdsk Flags: " + ChkDskFlags);
            writer.WriteLine(linePrefix + "        Signature Byte: " + SignatureByte);
            writer.WriteLine(linePrefix + "   Total Sectors (64b): " + TotalSectors64);
            writer.WriteLine(linePrefix + "       MFT Record Size: " + RawMftRecordSize);
            writer.WriteLine(linePrefix + "     Index Buffer Size: " + RawIndexBufferSize);
            writer.WriteLine(linePrefix + "  Volume Serial Number: " + VolumeSerialNumber);
        }

        internal static BiosParameterBlock Initialized(Geometry diskGeometry, int clusterSize, uint partitionStartLba,
                                                       long partitionSizeLba, int mftRecordSize, int indexBufferSize)
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
            bpb.OemId = EndianUtilities.BytesToString(bytes, offset + 0x03, 8);
            bpb.BytesPerSector = EndianUtilities.ToUInt16LittleEndian(bytes, offset + 0x0B);
            bpb.SectorsPerCluster = bytes[offset + 0x0D];
            bpb.ReservedSectors = EndianUtilities.ToUInt16LittleEndian(bytes, offset + 0x0E);
            bpb.NumFats = bytes[offset + 0x10];
            bpb.FatRootEntriesCount = EndianUtilities.ToUInt16LittleEndian(bytes, offset + 0x11);
            bpb.TotalSectors16 = EndianUtilities.ToUInt16LittleEndian(bytes, offset + 0x13);
            bpb.Media = bytes[offset + 0x15];
            bpb.FatSize16 = EndianUtilities.ToUInt16LittleEndian(bytes, offset + 0x16);
            bpb.SectorsPerTrack = EndianUtilities.ToUInt16LittleEndian(bytes, offset + 0x18);
            bpb.NumHeads = EndianUtilities.ToUInt16LittleEndian(bytes, offset + 0x1A);
            bpb.HiddenSectors = EndianUtilities.ToUInt32LittleEndian(bytes, offset + 0x1C);
            bpb.TotalSectors32 = EndianUtilities.ToUInt32LittleEndian(bytes, offset + 0x20);
            bpb.BiosDriveNumber = bytes[offset + 0x24];
            bpb.ChkDskFlags = bytes[offset + 0x25];
            bpb.SignatureByte = bytes[offset + 0x26];
            bpb.PaddingByte = bytes[offset + 0x27];
            bpb.TotalSectors64 = EndianUtilities.ToInt64LittleEndian(bytes, offset + 0x28);
            bpb.MftCluster = EndianUtilities.ToInt64LittleEndian(bytes, offset + 0x30);
            bpb.MftMirrorCluster = EndianUtilities.ToInt64LittleEndian(bytes, offset + 0x38);
            bpb.RawMftRecordSize = bytes[offset + 0x40];
            bpb.RawIndexBufferSize = bytes[offset + 0x44];
            bpb.VolumeSerialNumber = EndianUtilities.ToUInt64LittleEndian(bytes, offset + 0x48);

            return bpb;
        }

        internal void ToBytes(byte[] buffer, int offset)
        {
            EndianUtilities.StringToBytes(OemId, buffer, offset + 0x03, 8);
            EndianUtilities.WriteBytesLittleEndian(BytesPerSector, buffer, offset + 0x0B);
            buffer[offset + 0x0D] = SectorsPerCluster;
            EndianUtilities.WriteBytesLittleEndian(ReservedSectors, buffer, offset + 0x0E);
            buffer[offset + 0x10] = NumFats;
            EndianUtilities.WriteBytesLittleEndian(FatRootEntriesCount, buffer, offset + 0x11);
            EndianUtilities.WriteBytesLittleEndian(TotalSectors16, buffer, offset + 0x13);
            buffer[offset + 0x15] = Media;
            EndianUtilities.WriteBytesLittleEndian(FatSize16, buffer, offset + 0x16);
            EndianUtilities.WriteBytesLittleEndian(SectorsPerTrack, buffer, offset + 0x18);
            EndianUtilities.WriteBytesLittleEndian(NumHeads, buffer, offset + 0x1A);
            EndianUtilities.WriteBytesLittleEndian(HiddenSectors, buffer, offset + 0x1C);
            EndianUtilities.WriteBytesLittleEndian(TotalSectors32, buffer, offset + 0x20);
            buffer[offset + 0x24] = BiosDriveNumber;
            buffer[offset + 0x25] = ChkDskFlags;
            buffer[offset + 0x26] = SignatureByte;
            buffer[offset + 0x27] = PaddingByte;
            EndianUtilities.WriteBytesLittleEndian(TotalSectors64, buffer, offset + 0x28);
            EndianUtilities.WriteBytesLittleEndian(MftCluster, buffer, offset + 0x30);
            EndianUtilities.WriteBytesLittleEndian(MftMirrorCluster, buffer, offset + 0x38);
            buffer[offset + 0x40] = RawMftRecordSize;
            buffer[offset + 0x44] = RawIndexBufferSize;
            EndianUtilities.WriteBytesLittleEndian(VolumeSerialNumber, buffer, offset + 0x48);
        }

        internal int CalcRecordSize(byte rawSize)
        {
            if ((rawSize & 0x80) != 0)
            {
                return 1 << -(sbyte)rawSize;
            }
            return rawSize * SectorsPerCluster * BytesPerSector;
        }

        private static ulong GenSerialNumber()
        {
            byte[] buffer = new byte[8];
            Random rng = new Random();
            rng.NextBytes(buffer);
            return EndianUtilities.ToUInt64LittleEndian(buffer, 0);
        }

        private byte CodeRecordSize(int size)
        {
            if (size >= BytesPerCluster)
            {
                return (byte)(size / BytesPerCluster);
            }
            sbyte val = 0;
            while (size != 1)
            {
                size = (size >> 1) & 0x7FFFFFFF;
                val++;
            }

            return (byte)-val;
        }
    }
}
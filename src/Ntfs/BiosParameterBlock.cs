//
// Copyright (c) 2008-2009, Kenneth Bell
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

        public int MftRecordSize
        {
            get { return CalcRecordSize(RawMftRecordSize); }
        }

        public int IndexBufferSize
        {
            get { return CalcRecordSize(RawIndexBufferSize); }
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
    }
}

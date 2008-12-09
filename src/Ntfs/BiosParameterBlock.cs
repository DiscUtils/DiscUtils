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
        public int MftRecordSize;
        public int IndexBufferSize;
        public ulong VolumeSerialNumber;

        internal static BiosParameterBlock FromBytes(byte[] bytes, int p, int p_3)
        {
            BiosParameterBlock bpb = new BiosParameterBlock();
            bpb.OemId = Utilities.BytesToString(bytes, 0x03, 8);
            bpb.BytesPerSector = Utilities.ToUInt16LittleEndian(bytes, 0x0B);
            bpb.SectorsPerCluster = bytes[0x0D];
            bpb.ReservedSectors = Utilities.ToUInt16LittleEndian(bytes, 0x0E);
            bpb.NumFats = bytes[0x10];
            bpb.FatRootEntriesCount = Utilities.ToUInt16LittleEndian(bytes, 0x11);
            bpb.TotalSectors16 = Utilities.ToUInt16LittleEndian(bytes, 0x13);
            bpb.Media = bytes[0x15];
            bpb.FatSize16 = Utilities.ToUInt16LittleEndian(bytes, 0x16);
            bpb.SectorsPerTrack = Utilities.ToUInt16LittleEndian(bytes, 0x18);
            bpb.NumHeads = Utilities.ToUInt16LittleEndian(bytes, 0x1A);
            bpb.HiddenSectors = Utilities.ToUInt32LittleEndian(bytes, 0x1C);
            bpb.TotalSectors32 = Utilities.ToUInt32LittleEndian(bytes, 0x20);
            bpb.BiosDriveNumber = bytes[0x24];
            bpb.ChkDskFlags = bytes[0x25];
            bpb.SignatureByte = bytes[0x26];
            bpb.PaddingByte = bytes[0x27];
            bpb.TotalSectors64 = Utilities.ToInt64LittleEndian(bytes, 0x28);
            bpb.MftCluster = Utilities.ToInt64LittleEndian(bytes, 0x30);
            bpb.MftMirrorCluster = Utilities.ToInt64LittleEndian(bytes, 0x38);

            if ((bytes[0x40] & 0x80) != 0)
            {
                bpb.MftRecordSize = 1 << (-(sbyte)bytes[0x40]);
            }
            else
            {
                bpb.MftRecordSize = bytes[0x40] * bpb.SectorsPerCluster * bpb.BytesPerSector;
            }

            if ((bytes[0x44] & 0x80) != 0)
            {
                bpb.IndexBufferSize = 1 << (-(sbyte)bytes[0x44]);
            }
            else
            {
                bpb.IndexBufferSize = bytes[0x44] * bpb.SectorsPerCluster * bpb.BytesPerSector;
            }

            bpb.VolumeSerialNumber = Utilities.ToUInt64LittleEndian(bytes, 0x48);

            return bpb;
        }
    }
}

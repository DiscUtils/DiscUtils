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

namespace DiscUtils.LogicalDiskManager
{
    internal sealed class ComponentRecord : DatabaseRecord
    {
        public uint LinkId; // Identical on mirrors
        public ExtentMergeType MergeType; // (02 Spanned, Simple, Mirrored)  (01 on striped)
        public ulong NumExtents; // Could be num disks
        public string StatusString;
        public long StripeSizeSectors;
        public long StripeStride; // aka num partitions
        public uint Unknown1; // Zero
        public uint Unknown2; // Zero
        public ulong Unknown3; // 00 .. 00
        public ulong Unknown4; // ??
        public ulong VolumeId;

        protected override void DoReadFrom(byte[] buffer, int offset)
        {
            base.DoReadFrom(buffer, offset);

            int pos = offset + 0x18;

            Id = ReadVarULong(buffer, ref pos);
            Name = ReadVarString(buffer, ref pos);
            StatusString = ReadVarString(buffer, ref pos);
            MergeType = (ExtentMergeType)ReadByte(buffer, ref pos);
            Unknown1 = ReadUInt(buffer, ref pos); // Zero
            NumExtents = ReadVarULong(buffer, ref pos);
            Unknown2 = ReadUInt(buffer, ref pos);
            LinkId = ReadUInt(buffer, ref pos);
            Unknown3 = ReadULong(buffer, ref pos); // Zero
            VolumeId = ReadVarULong(buffer, ref pos);
            Unknown4 = ReadVarULong(buffer, ref pos); // Zero

            if ((Flags & 0x1000) != 0)
            {
                StripeSizeSectors = ReadVarLong(buffer, ref pos);
                StripeStride = ReadVarLong(buffer, ref pos);
            }
        }
    }
}
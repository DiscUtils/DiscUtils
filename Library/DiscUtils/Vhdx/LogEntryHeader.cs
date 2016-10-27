//
// Copyright (c) 2008-2013, Kenneth Bell
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

namespace DiscUtils.Vhdx
{
    using System;

    internal sealed class LogEntryHeader : IByteArraySerializable
    {
        public const uint LogEntrySignature = 0x65676F6C;

        public uint Signature;
        public uint Checksum;
        public uint EntryLength;
        public uint Tail;
        public ulong SequenceNumber;
        public uint DescriptorCount;
        public uint Reserved;
        public Guid LogGuid;
        public ulong FlushedFileOffset;
        public ulong LastFileOffset;

        private byte[] _data;

        public int Size
        {
            get { return 64; }
        }

        public bool IsValid
        {
            get
            {
                return Signature == LogEntrySignature;
            }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            _data = new byte[Size];
            Array.Copy(buffer, offset, _data, 0, Size);

            Signature = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
            Checksum = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
            EntryLength = Utilities.ToUInt32LittleEndian(buffer, offset + 8);
            Tail = Utilities.ToUInt32LittleEndian(buffer, offset + 12);
            SequenceNumber = Utilities.ToUInt64LittleEndian(buffer, offset + 16);
            DescriptorCount = Utilities.ToUInt32LittleEndian(buffer, offset + 24);
            Reserved = Utilities.ToUInt32LittleEndian(buffer, offset + 28);
            LogGuid = Utilities.ToGuidLittleEndian(buffer, offset + 32);
            FlushedFileOffset = Utilities.ToUInt64LittleEndian(buffer, offset + 48);
            LastFileOffset = Utilities.ToUInt64LittleEndian(buffer, offset + 56);

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}

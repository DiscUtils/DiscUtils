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

using System;
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class LogEntryHeader : IByteArraySerializable
    {
        public const uint LogEntrySignature = 0x65676F6C;

        private byte[] _data;
        public uint Checksum;
        public uint DescriptorCount;
        public uint EntryLength;
        public ulong FlushedFileOffset;
        public ulong LastFileOffset;
        public Guid LogGuid;
        public uint Reserved;
        public ulong SequenceNumber;

        public uint Signature;
        public uint Tail;

        public bool IsValid
        {
            get { return Signature == LogEntrySignature; }
        }

        public int Size
        {
            get { return 64; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            _data = new byte[Size];
            Array.Copy(buffer, offset, _data, 0, Size);

            Signature = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
            Checksum = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
            EntryLength = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8);
            Tail = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 12);
            SequenceNumber = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 16);
            DescriptorCount = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 24);
            Reserved = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 28);
            LogGuid = EndianUtilities.ToGuidLittleEndian(buffer, offset + 32);
            FlushedFileOffset = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 48);
            LastFileOffset = EndianUtilities.ToUInt64LittleEndian(buffer, offset + 56);

            return Size;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }
    }
}
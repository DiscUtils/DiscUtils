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
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal struct FileRecordReference : IByteArraySerializable, IComparable<FileRecordReference>
    {
        public FileRecordReference(ulong val)
        {
            Value = val;
        }

        public FileRecordReference(long mftIndex, ushort sequenceNumber)
        {
            Value = (ulong)(mftIndex & 0x0000FFFFFFFFFFFFL) |
                    ((ulong)sequenceNumber << 48 & 0xFFFF000000000000L);
        }

        public ulong Value { get; private set; }

        public long MftIndex
        {
            get { return (long)(Value & 0x0000FFFFFFFFFFFFL); }
        }

        public ushort SequenceNumber
        {
            get { return (ushort)((Value >> 48) & 0xFFFF); }
        }

        public int Size
        {
            get { return 8; }
        }

        public bool IsNull
        {
            get { return SequenceNumber == 0; }
        }

        public static bool operator ==(FileRecordReference a, FileRecordReference b)
        {
            return a.Value == b.Value;
        }

        public static bool operator !=(FileRecordReference a, FileRecordReference b)
        {
            return a.Value != b.Value;
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Value = EndianUtilities.ToUInt64LittleEndian(buffer, offset);
            return 8;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(Value, buffer, offset);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is FileRecordReference))
            {
                return false;
            }

            return Value == ((FileRecordReference)obj).Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public int CompareTo(FileRecordReference other)
        {
            if (Value < other.Value)
            {
                return -1;
            }
            if (Value > other.Value)
            {
                return 1;
            }
            return 0;
        }

        public override string ToString()
        {
            return "MFT:" + MftIndex + " (ver: " + SequenceNumber + ")";
        }
    }
}
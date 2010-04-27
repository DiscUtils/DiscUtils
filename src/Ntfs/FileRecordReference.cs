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
    internal struct FileRecordReference : IByteArraySerializable, IComparable<FileRecordReference>
    {
        private ulong _val;

        public FileRecordReference(ulong val)
        {
            _val = val;
        }

        public FileRecordReference(long mftIndex, ushort sequenceNumber)
        {
            _val = (ulong)(mftIndex & 0x0000FFFFFFFFFFFFL) | ((ulong)((ulong)sequenceNumber << 48) & 0xFFFF000000000000L);
        }

        public ulong Value
        {
            get { return _val; }
        }

        public long MftIndex
        {
            get { return (long)(_val & 0x0000FFFFFFFFFFFFL); }
        }

        public ushort SequenceNumber
        {
            get { return (ushort)((_val >> 48) & 0xFFFF); }
        }

        public override string ToString()
        {
            return "MFT:" + MftIndex + " (ver: " + SequenceNumber + ")";
        }

        #region IByteArraySerializable Members

        public int ReadFrom(byte[] buffer, int offset)
        {
            _val = Utilities.ToUInt64LittleEndian(buffer, offset);
            return 8;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            Utilities.WriteBytesLittleEndian(_val, buffer, offset);
        }

        public int Size
        {
            get { return 8; }
        }

        #endregion

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is FileRecordReference))
            {
                return false;
            }

            return _val == ((FileRecordReference)obj)._val;
        }

        public override int GetHashCode()
        {
            return _val.GetHashCode();
        }

        public static bool operator ==(FileRecordReference a, FileRecordReference b)
        {
            return a._val == b._val;
        }

        public static bool operator !=(FileRecordReference a, FileRecordReference b)
        {
            return a._val != b._val;
        }

        #region IComparable<FileReference> Members

        public int CompareTo(FileRecordReference other)
        {
            if (_val < other._val)
            {
                return -1;
            }
            else if (_val > other._val)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        #endregion
    }
}

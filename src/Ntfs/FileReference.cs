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
    internal struct FileReference : IByteArraySerializable
    {
        private ulong _val;

        public FileReference(ulong val)
        {
            _val = val;
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

        public void ReadFrom(byte[] buffer, int offset)
        {
            _val = Utilities.ToUInt32LittleEndian(buffer, offset);
        }

        #endregion

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is FileReference))
            {
                return false;
            }

            return _val == ((FileReference)obj)._val;
        }

        public override int GetHashCode()
        {
            return _val.GetHashCode();
        }

        public static bool operator ==(FileReference a, FileReference b)
        {
            return a._val == b._val;
        }

        public static bool operator !=(FileReference a, FileReference b)
        {
            return a._val != b._val;
        }
    }
}

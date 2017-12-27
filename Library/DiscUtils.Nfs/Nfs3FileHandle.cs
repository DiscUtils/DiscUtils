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

namespace DiscUtils.Nfs
{
    public sealed class Nfs3FileHandle : IEquatable<Nfs3FileHandle>, IComparable<Nfs3FileHandle>
    {
        public Nfs3FileHandle()
        {
        }

        public Nfs3FileHandle(XdrDataReader reader)
        {
            Value = reader.ReadBuffer(Nfs3Mount.MaxFileHandleSize);
        }

        public byte[] Value { get; set; }

        public int CompareTo(Nfs3FileHandle other)
        {
            if (other.Value == null)
            {
                return Value == null ? 0 : 1;
            }
            if (Value == null)
            {
                return -1;
            }

            int maxIndex = Math.Min(Value.Length, other.Value.Length);
            for (int i = 0; i < maxIndex; ++i)
            {
                int diff = Value[i] - other.Value[i];
                if (diff != 0)
                {
                    return diff;
                }
            }

            return Value.Length - other.Value.Length;
        }

        public bool Equals(Nfs3FileHandle other)
        {
            if (other == null)
            {
                return false;
            }

            if (Value == null)
            {
                return other.Value == null;
            }
            if (other.Value == null)
            {
                return false;
            }

            if (Value.Length != other.Value.Length)
            {
                return false;
            }

            for (int i = 0; i < Value.Length; ++i)
            {
                if (Value[i] != other.Value[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            Nfs3FileHandle other = obj as Nfs3FileHandle;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            int value = 0;
            if (Value != null)
            {
                for (int i = 0; i < Value.Length; ++i)
                {
                    value = (value << 1) ^ Value[i];
                }
            }

            return value;
        }

        internal void Write(XdrDataWriter writer)
        {
            writer.WriteBuffer(Value);
        }

        public override string ToString()
        {
            int value = 0;
            if (Value != null)
            {
                for (int i = Value.Length - 1; i >= 0; i--)
                {
                    value = (value << sizeof(byte)) | Value[i];
                }
            }

            return value.ToString();
        }
    }
}
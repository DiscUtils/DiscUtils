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
    public sealed class Nfs3WeakCacheConsistencyAttr
    {
        internal Nfs3WeakCacheConsistencyAttr(XdrDataReader reader)
        {
            Size = reader.ReadInt64();
            ModifyTime = new Nfs3FileTime(reader);
            ChangeTime = new Nfs3FileTime(reader);
        }

        public Nfs3WeakCacheConsistencyAttr()
        {
        }

        public Nfs3FileTime ChangeTime { get; set; }

        public Nfs3FileTime ModifyTime { get; set; }

        public long Size { get; set; }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(Size);
            ModifyTime.Write(writer);
            ChangeTime.Write(writer);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3WeakCacheConsistencyAttr);
        }

        public bool Equals(Nfs3WeakCacheConsistencyAttr other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Size == Size
                && object.Equals(other.ModifyTime, ModifyTime)
                && object.Equals(other.ChangeTime, ChangeTime);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Size, ModifyTime, ChangeTime);
        }
    }
}
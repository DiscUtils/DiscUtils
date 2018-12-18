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
    public sealed class Nfs3WeakCacheConsistency
    {
        internal Nfs3WeakCacheConsistency(XdrDataReader reader)
        {
            if (reader.ReadBool())
            {
                Before = new Nfs3WeakCacheConsistencyAttr(reader);
            }

            if (reader.ReadBool())
            {
                After = new Nfs3FileAttributes(reader);
            }
        }

        public Nfs3WeakCacheConsistency()
        {
        }

        public Nfs3FileAttributes After { get; set; }

        public Nfs3WeakCacheConsistencyAttr Before { get; set; }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(Before != null);
            if (Before != null)
            {
                Before.Write(writer);
            }

            writer.Write(After != null);
            if (After != null)
            {
                After.Write(writer);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3WeakCacheConsistency);
        }

        public bool Equals(Nfs3WeakCacheConsistency other)
        {
            if (other == null)
            {
                return false;
            }

            return object.Equals(other.After, After)
                && object.Equals(other.Before, Before);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(After, Before);
        }
    }
}
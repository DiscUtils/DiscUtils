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
#if !NET20
using System.Linq;
#endif

namespace DiscUtils.Nfs
{
    public sealed class Nfs3WriteResult : Nfs3CallResult
    {
        internal Nfs3WriteResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();
            CacheConsistency = new Nfs3WeakCacheConsistency(reader);
            if (Status == Nfs3Status.Ok)
            {
                Count = reader.ReadInt32();
                HowCommitted = (Nfs3StableHow)reader.ReadInt32();
                WriteVerifier = reader.ReadUInt64();
            }
        }

        public Nfs3WriteResult()
        {
        }

        public Nfs3WeakCacheConsistency CacheConsistency { get; set; }

        public int Count { get; set; }

        public Nfs3StableHow HowCommitted { get; set; }

        public ulong WriteVerifier { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);
            CacheConsistency.Write(writer);
            if(Status == Nfs3Status.Ok)
            {
                writer.Write(Count);
                writer.Write((int)HowCommitted);
                writer.Write(WriteVerifier);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3WriteResult);
        }

        public bool Equals(Nfs3WriteResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.CacheConsistency, CacheConsistency)
                && other.Count == Count
                && other.WriteVerifier == WriteVerifier
                && other.HowCommitted == HowCommitted;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, CacheConsistency, Count, WriteVerifier, HowCommitted);
        }
    }
}
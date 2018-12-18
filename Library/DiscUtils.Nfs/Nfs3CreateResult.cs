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
    public class Nfs3CreateResult : Nfs3CallResult
    {
        internal Nfs3CreateResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();
            if (Status == Nfs3Status.Ok)
            {
                if (reader.ReadBool())
                {
                    FileHandle = new Nfs3FileHandle(reader);
                }

                if (reader.ReadBool())
                {
                    FileAttributes = new Nfs3FileAttributes(reader);
                }
            }

            CacheConsistency = new Nfs3WeakCacheConsistency(reader);
        }

        public Nfs3CreateResult()
        {
        }

        public Nfs3WeakCacheConsistency CacheConsistency { get; set; }

        public Nfs3FileAttributes FileAttributes { get; set; }

        public Nfs3FileHandle FileHandle { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);

            if (Status == Nfs3Status.Ok)
            {
                writer.Write(FileHandle != null);
                if (FileHandle != null)
                {
                    FileHandle.Write(writer);
                }

                writer.Write(FileAttributes != null);
                if (FileAttributes != null)
                {
                    FileAttributes.Write(writer);
                }
            }

            CacheConsistency.Write(writer);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3CreateResult);
        }

        public bool Equals(Nfs3CreateResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.FileHandle, FileHandle)
                && object.Equals(other.FileAttributes, FileAttributes)
                && object.Equals(other.CacheConsistency, CacheConsistency);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, FileHandle, FileAttributes, CacheConsistency);
        }
    }
}
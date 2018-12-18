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
    public sealed class Nfs3LookupResult : Nfs3CallResult
    {
        public Nfs3LookupResult()
        {
        }

        internal Nfs3LookupResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();
            if (Status == Nfs3Status.Ok)
            {
                ObjectHandle = new Nfs3FileHandle(reader);
                if (reader.ReadBool())
                {
                    ObjectAttributes = new Nfs3FileAttributes(reader);
                }
            }

            if (reader.ReadBool())
            {
                DirAttributes = new Nfs3FileAttributes(reader);
            }
        }

        public Nfs3FileAttributes DirAttributes { get; set; }

        public Nfs3FileAttributes ObjectAttributes { get; set; }

        public Nfs3FileHandle ObjectHandle { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)this.Status);

            if (Status == Nfs3Status.Ok)
            {
                ObjectHandle.Write(writer);

                writer.Write(ObjectAttributes != null);

                if (ObjectAttributes != null)
                {
                    ObjectAttributes.Write(writer);
                }
            }

            writer.Write(DirAttributes != null);

            if (DirAttributes != null)
            {
                DirAttributes.Write(writer);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3LookupResult);
        }

        public bool Equals(Nfs3LookupResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.ObjectHandle, ObjectHandle)
                && object.Equals(other.ObjectAttributes, ObjectAttributes)
                && object.Equals(other.DirAttributes, DirAttributes);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, ObjectHandle, ObjectAttributes, DirAttributes);
        }
    }
}
//
// Copyright (c) 2017, Bianco Veigel
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
    public class Nfs3FileSystemStatResult : Nfs3CallResult
    {
        public Nfs3FileSystemStatResult()
        {
        }

        internal Nfs3FileSystemStatResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();
            if (reader.ReadBool())
            {
                PostOpAttributes = new Nfs3FileAttributes(reader);
            }

            if (Status == Nfs3Status.Ok)
            {
                FileSystemStat = new Nfs3FileSystemStat(reader);
            }
        }

        public Nfs3FileAttributes PostOpAttributes { get; set; }

        public Nfs3FileSystemStat FileSystemStat { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);

            writer.Write(PostOpAttributes != null);
            if (PostOpAttributes != null)
            {
                PostOpAttributes.Write(writer);
            }

            if (Status == Nfs3Status.Ok)
            {
                FileSystemStat.Write(writer);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3FileSystemStatResult);
        }

        public bool Equals(Nfs3FileSystemStatResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.PostOpAttributes, PostOpAttributes)
                && object.Equals(other.FileSystemStat, FileSystemStat);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, PostOpAttributes, FileSystemStat);
        }
    }
}

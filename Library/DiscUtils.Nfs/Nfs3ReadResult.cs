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
    public sealed class Nfs3ReadResult : Nfs3CallResult
    {
        internal Nfs3ReadResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();
            if (reader.ReadBool())
            {
                FileAttributes = new Nfs3FileAttributes(reader);
            }

            if (Status == Nfs3Status.Ok)
            {
                Count = reader.ReadInt32();
                Eof = reader.ReadBool();
                Data = reader.ReadBuffer();
            }
        }

        public Nfs3ReadResult()
        {
        }

        public int Count { get; set; }

        public byte[] Data { get; set; }

        public bool Eof { get; set; }

        public Nfs3FileAttributes FileAttributes { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);

            writer.Write(FileAttributes != null);
            if (FileAttributes != null)
            {
                FileAttributes.Write(writer);
            }

            if (Status == Nfs3Status.Ok)
            {
                writer.Write(Count);
                writer.Write(Eof);
                writer.WriteBuffer(Data);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3ReadResult);
        }

        public bool Equals(Nfs3ReadResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.FileAttributes, FileAttributes)
                && other.Count == Count
#if !NET20
                && Enumerable.SequenceEqual(other.Data, Data)
#endif
                && other.Eof == Eof;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, FileAttributes, Count, Eof, Data);
        }
    }
}
//
// Copyright (c) 2017, Quamotion
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
using System.Collections.Generic;
#if !NET20
using System.Linq;
#endif

namespace DiscUtils.Nfs
{
    public class Nfs3ReadDirResult : Nfs3CallResult
    {
        public Nfs3ReadDirResult()
        {
        }

        public Nfs3ReadDirResult(XdrDataReader reader)
        {
            Status = (Nfs3Status)reader.ReadInt32();

            if (reader.ReadBool())
            {
                DirAttributes = new Nfs3FileAttributes(reader);
            }

            DirEntries = new List<Nfs3DirectoryEntry>();
            if (Status == Nfs3Status.Ok)
            {
                CookieVerifier = reader.ReadUInt64();

                while (reader.ReadBool())
                {
                    DirEntries.Add(new Nfs3DirectoryEntry(reader));
                }

                Eof = reader.ReadBool();
            }
        }

        public Nfs3FileAttributes DirAttributes { get; set; }

        public List<Nfs3DirectoryEntry> DirEntries { get; set; }

        public ulong CookieVerifier { get; set; }

        public bool Eof { get; set; }

        public override void Write(XdrDataWriter writer)
        {
            writer.Write((int)Status);

            writer.Write(DirAttributes != null);
            if (DirAttributes != null)
            {
                DirAttributes.Write(writer);
            }

            if (Status == Nfs3Status.Ok)
            {
                writer.Write(CookieVerifier);

                foreach (var entry in DirEntries)
                {
                    writer.Write(true);
                    entry.Write(writer);
                }

                writer.Write(false);
                writer.Write(Eof);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3ReadDirResult);
        }

        public bool Equals(Nfs3ReadDirResult other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Status == Status
                && object.Equals(other.DirAttributes, DirAttributes)
                && other.CookieVerifier == CookieVerifier
#if !NET20
                && Enumerable.SequenceEqual(other.DirEntries, DirEntries)
#endif
                && other.Eof == Eof;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Status, DirAttributes, CookieVerifier, DirEntries, Eof);
        }
    }
}

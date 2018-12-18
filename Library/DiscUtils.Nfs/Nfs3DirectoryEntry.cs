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
using System.IO;

namespace DiscUtils.Nfs
{
    public sealed class Nfs3DirectoryEntry
    {
        internal Nfs3DirectoryEntry(XdrDataReader reader)
        {
            FileId = reader.ReadUInt64();
            Name = reader.ReadString();
            Cookie = reader.ReadUInt64();
            if (reader.ReadBool())
            {
                FileAttributes = new Nfs3FileAttributes(reader);
            }

            if (reader.ReadBool())
            {
                FileHandle = new Nfs3FileHandle(reader);
            }
        }

        public Nfs3DirectoryEntry()
        {
        }

        public ulong Cookie { get; set; }

        public Nfs3FileAttributes FileAttributes { get; set; }

        public Nfs3FileHandle FileHandle { get; set; }

        public ulong FileId { get; set; }

        public string Name { get; set; }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write(FileId);
            writer.Write(Name);
            writer.Write(Cookie);

            writer.Write(FileAttributes != null);
            if (FileAttributes != null)
            {
                FileAttributes.Write(writer);
            }

            writer.Write(FileHandle != null);
            if (FileHandle != null)
            {
                FileHandle.Write(writer);
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3DirectoryEntry);
        }

        public bool Equals(Nfs3DirectoryEntry other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Cookie == Cookie
                && object.Equals(other.FileAttributes, FileAttributes)
                && object.Equals(other.FileHandle, FileHandle)
                && other.FileId == FileId
                && object.Equals(other.Name, Name);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Cookie, FileAttributes, FileHandle, FileId, Name);
        }

        public override string ToString()
        {
            return this.Name;
        }

        public long GetSize()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                XdrDataWriter writer = new XdrDataWriter(stream);
                Write(writer);
                return stream.Length;
            }
        }
    }
}
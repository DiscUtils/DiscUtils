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
    public sealed class Nfs3FileAttributes
    {
        public Nfs3FileTime AccessTime;
        public long BytesUsed;
        public Nfs3FileTime ChangeTime;
        public ulong FileId;
        public ulong FileSystemId;
        public uint Gid;
        public uint LinkCount;
        public UnixFilePermissions Mode;
        public Nfs3FileTime ModifyTime;
        public uint RdevMajor;
        public uint RdevMinor;
        public long Size;
        public Nfs3FileType Type;
        public uint Uid;

        public Nfs3FileAttributes()
        {
        }

        internal Nfs3FileAttributes(XdrDataReader reader)
        {
            Type = (Nfs3FileType)reader.ReadInt32();
            Mode = (UnixFilePermissions)reader.ReadInt32();
            LinkCount = reader.ReadUInt32();
            Uid = reader.ReadUInt32();
            Gid = reader.ReadUInt32();
            Size = reader.ReadInt64();
            BytesUsed = reader.ReadInt64();
            RdevMajor = reader.ReadUInt32();
            RdevMinor = reader.ReadUInt32();
            FileSystemId = reader.ReadUInt64();
            FileId = reader.ReadUInt64();
            AccessTime = new Nfs3FileTime(reader);
            ModifyTime = new Nfs3FileTime(reader);
            ChangeTime = new Nfs3FileTime(reader);
        }

        internal void Write(XdrDataWriter writer)
        {
            writer.Write((int)Type);
            writer.Write((int)Mode);
            writer.Write(LinkCount);
            writer.Write(Uid);
            writer.Write(Gid);
            writer.Write(Size);
            writer.Write(BytesUsed);
            writer.Write(RdevMajor);
            writer.Write(RdevMinor);
            writer.Write(FileSystemId);
            writer.Write(FileId);
            AccessTime.Write(writer);
            ModifyTime.Write(writer);
            ChangeTime.Write(writer);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Nfs3FileAttributes);
        }

        public bool Equals(Nfs3FileAttributes other)
        {
            if (other == null)
            {
                return false;
            }

            return other.Type == Type
                && other.Mode == Mode
                && other.LinkCount == LinkCount
                && other.Uid == Uid
                && other.Gid == Gid
                && other.Size == Size
                && other.BytesUsed == BytesUsed
                && other.RdevMajor == RdevMajor
                && other.RdevMinor == RdevMinor
                && other.FileSystemId == FileSystemId
                && other.FileId == FileId
                && object.Equals(other.AccessTime, AccessTime)
                && object.Equals(other.ModifyTime, ModifyTime)
                && object.Equals(other.ChangeTime, ChangeTime);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(Type, Mode, LinkCount, Uid, Gid, Size, BytesUsed, RdevMajor),
                RdevMinor, FileSystemId, FileId, AccessTime, ModifyTime, ChangeTime);
        }
    }
}
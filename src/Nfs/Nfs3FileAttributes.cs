//
// Copyright (c) 2008-2010, Kenneth Bell
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


namespace DiscUtils.Nfs
{
    internal class Nfs3FileAttributes
    {
        public Nfs3FileType Type;
        public UnixFilePermissions Mode;
        public uint LinkCount;
        public uint Uid;
        public uint Gid;
        public long Size;
        public long BytesUsed;
        public uint RdevMajor;
        public uint RdevMinor;
        public ulong FileSystemId;
        public ulong FileId;
        public Nfs3FileTime AccessTime;
        public Nfs3FileTime ModifyTime;
        public Nfs3FileTime ChangeTime;

        public Nfs3FileAttributes(XdrDataReader reader)
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
    }
}

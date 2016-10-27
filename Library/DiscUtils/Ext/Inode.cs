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

namespace DiscUtils.Ext
{
    using System;
    using System.IO;

    internal class Inode : IByteArraySerializable
    {
        public ushort Mode;
        public ushort UserIdLow;
        public uint FileSize;
        public uint AccessTime;
        public uint CreationTime;
        public uint ModificationTime;
        public uint DeletionTime;
        public ushort GroupIdLow;
        public ushort LinksCount;
        public uint BlocksCount;
        public InodeFlags Flags;
        public uint[] DirectBlocks;
        public uint IndirectBlock;
        public uint DoubleIndirectBlock;
        public uint TripleIndirectBlock;
        public uint FileVersion;
        public uint FileAcl;
        public uint DirAcl;
        public uint FragAddress;
        public byte Fragment;
        public byte FragmentSize;
        public ushort UserIdHigh;
        public ushort GroupIdHigh;

        public ExtentBlock Extents;
        public byte[] FastSymlink;

        public UnixFileType FileType
        {
            get { return (UnixFileType)((Mode >> 12) & 0xff); }
        }

        public int Size
        {
            get { throw new NotImplementedException(); }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Mode = Utilities.ToUInt16LittleEndian(buffer, offset + 0);
            UserIdLow = Utilities.ToUInt16LittleEndian(buffer, offset + 2);
            FileSize = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
            AccessTime = Utilities.ToUInt32LittleEndian(buffer, offset + 8);
            CreationTime = Utilities.ToUInt32LittleEndian(buffer, offset + 12);
            ModificationTime = Utilities.ToUInt32LittleEndian(buffer, offset + 16);
            DeletionTime = Utilities.ToUInt32LittleEndian(buffer, offset + 20);
            GroupIdLow = Utilities.ToUInt16LittleEndian(buffer, offset + 24);
            LinksCount = Utilities.ToUInt16LittleEndian(buffer, offset + 26);
            BlocksCount = Utilities.ToUInt32LittleEndian(buffer, offset + 28);
            Flags = (InodeFlags)Utilities.ToUInt32LittleEndian(buffer, offset + 32);

            FastSymlink = null;
            Extents = null;
            DirectBlocks = null;
            if (FileType == UnixFileType.Link && BlocksCount == 0)
            {
                FastSymlink = new byte[60];
                Array.Copy(buffer, offset + 40, FastSymlink, 0, 60);
            }
            else if ((Flags & InodeFlags.ExtentsUsed) != 0)
            {
                Extents = Utilities.ToStruct<ExtentBlock>(buffer, offset + 40);
            }
            else
            {
                DirectBlocks = new uint[12];
                for (int i = 0; i < 12; ++i)
                {
                    DirectBlocks[i] = Utilities.ToUInt32LittleEndian(buffer, offset + 40 + (i * 4));
                }

                IndirectBlock = Utilities.ToUInt32LittleEndian(buffer, offset + 88);
                DoubleIndirectBlock = Utilities.ToUInt32LittleEndian(buffer, offset + 92);
                TripleIndirectBlock = Utilities.ToUInt32LittleEndian(buffer, offset + 96);
            }

            FileVersion = Utilities.ToUInt32LittleEndian(buffer, offset + 100);
            FileAcl = Utilities.ToUInt32LittleEndian(buffer, offset + 104);
            DirAcl = Utilities.ToUInt32LittleEndian(buffer, offset + 108);
            FragAddress = Utilities.ToUInt32LittleEndian(buffer, offset + 112);
            Fragment = buffer[offset + 116];
            FragmentSize = buffer[offset + 117];
            UserIdHigh = Utilities.ToUInt16LittleEndian(buffer, offset + 120);
            GroupIdHigh = Utilities.ToUInt16LittleEndian(buffer, offset + 122);

            return 128;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public IBuffer GetContentBuffer(Context context)
        {
            if (FastSymlink != null)
            {
                return new StreamBuffer(new MemoryStream(FastSymlink, false), Ownership.Dispose);
            }
            else if ((Flags & InodeFlags.ExtentsUsed) != 0)
            {
                return new ExtentsFileBuffer(context, this);
            }
            else
            {
                return new FileBuffer(context, this);
            }
        }
    }
}

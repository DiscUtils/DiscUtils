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
using DiscUtils.Streams;

namespace DiscUtils.Ext
{
    internal class Inode : IByteArraySerializable
    {
        public uint AccessTime;
        public uint BlocksCount;
        public uint CreationTime;
        public uint DeletionTime;
        public uint DirAcl;
        public uint[] DirectBlocks;
        public uint DoubleIndirectBlock;

        public ExtentBlock Extents;
        public byte[] FastSymlink;
        public uint FileAcl;
        public uint FileSize;
        public uint FileVersion;
        public InodeFlags Flags;
        public uint FragAddress;
        public byte Fragment;
        public byte FragmentSize;
        public ushort GroupIdHigh;
        public ushort GroupIdLow;
        public uint IndirectBlock;
        public ushort LinksCount;
        public ushort Mode;
        public uint ModificationTime;
        public uint TripleIndirectBlock;
        public ushort UserIdHigh;
        public ushort UserIdLow;

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
            Mode = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0);
            UserIdLow = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 2);
            FileSize = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
            AccessTime = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8);
            CreationTime = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 12);
            ModificationTime = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 16);
            DeletionTime = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 20);
            GroupIdLow = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 24);
            LinksCount = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 26);
            BlocksCount = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 28);
            Flags = (InodeFlags)EndianUtilities.ToUInt32LittleEndian(buffer, offset + 32);

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
                Extents = EndianUtilities.ToStruct<ExtentBlock>(buffer, offset + 40);
            }
            else
            {
                DirectBlocks = new uint[12];
                for (int i = 0; i < 12; ++i)
                {
                    DirectBlocks[i] = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 40 + i * 4);
                }

                IndirectBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 88);
                DoubleIndirectBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 92);
                TripleIndirectBlock = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 96);
            }

            FileVersion = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 100);
            FileAcl = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 104);
            DirAcl = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 108);
            FragAddress = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 112);
            Fragment = buffer[offset + 116];
            FragmentSize = buffer[offset + 117];
            UserIdHigh = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 120);
            GroupIdHigh = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 122);

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
            if ((Flags & InodeFlags.ExtentsUsed) != 0)
            {
                return new ExtentsFileBuffer(context, this);
            }
            return new FileBuffer(context, this);
        }
    }
}
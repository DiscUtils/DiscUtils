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

namespace DiscUtils.SquashFs
{
    internal abstract class Inode : IByteArraySerializable
    {
        public ushort GidKey;
        public uint InodeNumber;
        public ushort Mode;
        public DateTime ModificationTime;
        public int NumLinks;
        public InodeType Type;
        public ushort UidKey;

        public virtual long FileSize
        {
            get { return 0; }
            set { throw new NotImplementedException(); }
        }

        public abstract int Size { get; }

        public virtual int ReadFrom(byte[] buffer, int offset)
        {
            Type = (InodeType)EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0);
            Mode = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 2);
            UidKey = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 4);
            GidKey = EndianUtilities.ToUInt16LittleEndian(buffer, offset + 6);
            ModificationTime = ((long) EndianUtilities.ToUInt32LittleEndian(buffer, offset + 8)).FromUnixTimeSeconds().DateTime;
            InodeNumber = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 12);
            return 16;
        }

        public virtual void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian((ushort)Type, buffer, offset + 0);
            EndianUtilities.WriteBytesLittleEndian(Mode, buffer, offset + 2);
            EndianUtilities.WriteBytesLittleEndian(UidKey, buffer, offset + 4);
            EndianUtilities.WriteBytesLittleEndian(GidKey, buffer, offset + 6);
            EndianUtilities.WriteBytesLittleEndian(Convert.ToUInt32((new DateTimeOffset(ModificationTime)).ToUnixTimeSeconds()), buffer, offset + 8);
            EndianUtilities.WriteBytesLittleEndian(InodeNumber, buffer, offset + 12);
        }

        public static Inode Read(MetablockReader inodeReader)
        {
            byte[] typeData = new byte[2];
            if (inodeReader.Read(typeData, 0, 2) != 2)
            {
                throw new IOException("Unable to read Inode type");
            }

            InodeType type = (InodeType)EndianUtilities.ToUInt16LittleEndian(typeData, 0);
            Inode inode = InstantiateType(type);

            byte[] inodeData = new byte[inode.Size];
            inodeData[0] = typeData[0];
            inodeData[1] = typeData[1];

            if (inodeReader.Read(inodeData, 2, inode.Size - 2) != inode.Size - 2)
            {
                throw new IOException("Unable to read whole Inode");
            }

            inode.ReadFrom(inodeData, 0);

            return inode;
        }

        private static Inode InstantiateType(InodeType type)
        {
            switch (type)
            {
                case InodeType.Directory:
                    return new DirectoryInode();
                case InodeType.ExtendedDirectory:
                    return new ExtendedDirectoryInode();
                case InodeType.File:
                    return new RegularInode();
                case InodeType.Symlink:
                    return new SymlinkInode();
                case InodeType.CharacterDevice:
                case InodeType.BlockDevice:
                    return new DeviceInode();
                default:
                    throw new NotImplementedException("Inode type not implemented: " + type);
            }
        }
    }
}
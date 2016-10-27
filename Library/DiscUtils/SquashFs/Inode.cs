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

namespace DiscUtils.SquashFs
{
    using System;
    using System.IO;

    internal abstract class Inode : IByteArraySerializable
    {
        public InodeType Type;
        public ushort Mode;
        public ushort UidKey;
        public ushort GidKey;
        public DateTime ModificationTime;
        public uint InodeNumber;
        public int NumLinks;

        public abstract int Size
        {
            get;
        }

        public virtual long FileSize
        {
            get { return 0; }
            set { throw new NotImplementedException(); }
        }

        public static Inode Read(MetablockReader inodeReader)
        {
            byte[] typeData = new byte[2];
            if (inodeReader.Read(typeData, 0, 2) != 2)
            {
                throw new IOException("Unable to read Inode type");
            }

            InodeType type = (InodeType)Utilities.ToUInt16LittleEndian(typeData, 0);
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

        public virtual int ReadFrom(byte[] buffer, int offset)
        {
            Type = (InodeType)Utilities.ToUInt16LittleEndian(buffer, offset + 0);
            Mode = Utilities.ToUInt16LittleEndian(buffer, offset + 2);
            UidKey = Utilities.ToUInt16LittleEndian(buffer, offset + 4);
            GidKey = Utilities.ToUInt16LittleEndian(buffer, offset + 6);
            ModificationTime = Utilities.DateTimeFromUnix(Utilities.ToUInt32LittleEndian(buffer, offset + 8));
            InodeNumber = Utilities.ToUInt32LittleEndian(buffer, offset + 12);
            return 16;
        }

        public virtual void WriteTo(byte[] buffer, int offset)
        {
            Utilities.WriteBytesLittleEndian((ushort)Type, buffer, offset + 0);
            Utilities.WriteBytesLittleEndian(Mode, buffer, offset + 2);
            Utilities.WriteBytesLittleEndian(UidKey, buffer, offset + 4);
            Utilities.WriteBytesLittleEndian(GidKey, buffer, offset + 6);
            Utilities.WriteBytesLittleEndian(Utilities.DateTimeToUnix(ModificationTime), buffer, offset + 8);
            Utilities.WriteBytesLittleEndian(InodeNumber, buffer, offset + 12);
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

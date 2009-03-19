//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.Text;

namespace DiscUtils.Ntfs
{
    [Flags]
    internal enum FileNameRecordFlags : uint
    {
        None =         0x00000000,
        ReadOnly =     0x00000001,
        Hidden =       0x00000002,
        System =       0x00000004,
        Archive =      0x00000020,
        Device =       0x00000040,
        Normal =       0x00000080,
        Temporary =    0x00000100,
        Sparse    =    0x00000200,
        ReparsePoint = 0x00000400,
        Compressed =   0x00000800,
        Offline =      0x00001000,
        NotIndexed =   0x00002000,
        Encrypted =    0x00004000,
        Directory =    0x10000000,
        IndexView =    0x20000000
    }

    internal class FileNameRecord : IByteArraySerializable
    {
        public FileReference ParentDirectory;
        public DateTime CreationTime;
        public DateTime ModificationTime;
        public DateTime MftChangedTime;
        public DateTime LastAccessTime;
        public ulong AllocatedSize;
        public ulong RealSize;
        public FileNameRecordFlags Flags;
        public uint Unknown;
        public byte FileNameNamespace;
        public string FileName;

        public FileNameRecord()
        {
        }

        public FileNameRecord(byte[] data, int offset)
        {
            ReadFrom(data, offset);
        }

        public FileNameRecord(string name)
        {
            FileName = name;
        }

        public FileAttributes FileAttributes
        {
            get { return ConvertFlags(Flags); }
        }

        public override string ToString()
        {
            return FileName;
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "FILE NAME RECORD");
            writer.WriteLine(indent + "   Parent Directory: " + ParentDirectory);
            writer.WriteLine(indent + "      Creation Time: " + CreationTime);
            writer.WriteLine(indent + "  Modification Time: " + ModificationTime);
            writer.WriteLine(indent + "   MFT Changed Time: " + MftChangedTime);
            writer.WriteLine(indent + "   Last Access Time: " + LastAccessTime);
            writer.WriteLine(indent + "     Allocated Size: " + AllocatedSize);
            writer.WriteLine(indent + "          Real Size: " + RealSize);
            writer.WriteLine(indent + "              Flags: " + Flags);
            writer.WriteLine(indent + "            Unknown: " + Unknown);
            writer.WriteLine(indent + "          File Name: " + FileName);
        }

        #region IByteArraySerializable Members

        public void ReadFrom(byte[] buffer, int offset)
        {
            ParentDirectory = new FileReference(Utilities.ToUInt64LittleEndian(buffer, offset + 0x00));
            CreationTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x08));
            ModificationTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x10));
            MftChangedTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x18));
            LastAccessTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x20));
            AllocatedSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x28);
            RealSize = Utilities.ToUInt64LittleEndian(buffer, offset + 0x30);
            Flags = (FileNameRecordFlags)Utilities.ToUInt32LittleEndian(buffer, offset + 0x38);
            Unknown = Utilities.ToUInt32LittleEndian(buffer, offset + 0x3C);
            byte fnLen = buffer[offset + 0x40];
            FileNameNamespace = buffer[offset + 0x41];
            FileName = Encoding.Unicode.GetString(buffer, offset + 0x42, fnLen * 2);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            Utilities.WriteBytesLittleEndian((ulong)ParentDirectory.Value, buffer, offset + 0x00);
            Utilities.WriteBytesLittleEndian((ulong)CreationTime.ToFileTimeUtc(), buffer, offset + 0x08);
            Utilities.WriteBytesLittleEndian((ulong)ModificationTime.ToFileTimeUtc(), buffer, offset + 0x10);
            Utilities.WriteBytesLittleEndian((ulong)MftChangedTime.ToFileTimeUtc(), buffer, offset + 0x18);
            Utilities.WriteBytesLittleEndian((ulong)LastAccessTime.ToFileTimeUtc(), buffer, offset + 0x20);
            Utilities.WriteBytesLittleEndian(AllocatedSize, buffer, offset + 0x28);
            Utilities.WriteBytesLittleEndian(RealSize, buffer, offset + 0x30);
            Utilities.WriteBytesLittleEndian((uint)Flags, buffer, offset + 0x38);
            Utilities.WriteBytesLittleEndian(Unknown, buffer, offset + 0x3C);
            buffer[offset + 0x40] = (byte)FileName.Length;
            buffer[offset + 0x41] = FileNameNamespace;
            Encoding.Unicode.GetBytes(FileName, 0, FileName.Length, buffer, offset + 0x42);
        }

        public int Size
        {
            get
            {
                return 0x42 + FileName.Length * 2;
            }
        }

        #endregion

        internal static FileAttributes ConvertFlags(FileNameRecordFlags flags)
        {
            FileAttributes result = (FileAttributes)(((uint)flags) & 0xFFFF);
            if ((flags & FileNameRecordFlags.Directory) != 0)
            {
                result |= FileAttributes.Directory;
            }
            return result;
        }
    }
}

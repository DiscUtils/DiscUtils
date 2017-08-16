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

namespace DiscUtils.Ntfs
{
    internal sealed class StandardInformation : IByteArraySerializable, IDiagnosticTraceable
    {
        private bool _haveExtraFields = true;
        public uint ClassId;
        public DateTime CreationTime;
        public FileAttributeFlags FileAttributes;
        public DateTime LastAccessTime;
        public uint MaxVersions;
        public DateTime MftChangedTime;
        public DateTime ModificationTime;
        public uint OwnerId;
        public ulong QuotaCharged;
        public uint SecurityId;
        public ulong UpdateSequenceNumber;
        public uint Version;

        public int Size
        {
            get { return _haveExtraFields ? 0x48 : 0x30; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            CreationTime = ReadDateTime(buffer, 0x00);
            ModificationTime = ReadDateTime(buffer, 0x08);
            MftChangedTime = ReadDateTime(buffer, 0x10);
            LastAccessTime = ReadDateTime(buffer, 0x18);
            FileAttributes = (FileAttributeFlags)EndianUtilities.ToUInt32LittleEndian(buffer, 0x20);
            MaxVersions = EndianUtilities.ToUInt32LittleEndian(buffer, 0x24);
            Version = EndianUtilities.ToUInt32LittleEndian(buffer, 0x28);
            ClassId = EndianUtilities.ToUInt32LittleEndian(buffer, 0x2C);

            if (buffer.Length > 0x30)
            {
                OwnerId = EndianUtilities.ToUInt32LittleEndian(buffer, 0x30);
                SecurityId = EndianUtilities.ToUInt32LittleEndian(buffer, 0x34);
                QuotaCharged = EndianUtilities.ToUInt64LittleEndian(buffer, 0x38);
                UpdateSequenceNumber = EndianUtilities.ToUInt64LittleEndian(buffer, 0x40);
                _haveExtraFields = true;
                return 0x48;
            }
            _haveExtraFields = false;
            return 0x30;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian(CreationTime.ToFileTimeUtc(), buffer, 0x00);
            EndianUtilities.WriteBytesLittleEndian(ModificationTime.ToFileTimeUtc(), buffer, 0x08);
            EndianUtilities.WriteBytesLittleEndian(MftChangedTime.ToFileTimeUtc(), buffer, 0x10);
            EndianUtilities.WriteBytesLittleEndian(LastAccessTime.ToFileTimeUtc(), buffer, 0x18);
            EndianUtilities.WriteBytesLittleEndian((uint)FileAttributes, buffer, 0x20);
            EndianUtilities.WriteBytesLittleEndian(MaxVersions, buffer, 0x24);
            EndianUtilities.WriteBytesLittleEndian(Version, buffer, 0x28);
            EndianUtilities.WriteBytesLittleEndian(ClassId, buffer, 0x2C);

            if (_haveExtraFields)
            {
                EndianUtilities.WriteBytesLittleEndian(OwnerId, buffer, 0x30);
                EndianUtilities.WriteBytesLittleEndian(SecurityId, buffer, 0x34);
                EndianUtilities.WriteBytesLittleEndian(QuotaCharged, buffer, 0x38);
                EndianUtilities.WriteBytesLittleEndian(UpdateSequenceNumber, buffer, 0x38);
            }
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "      Creation Time: " + CreationTime);
            writer.WriteLine(indent + "  Modification Time: " + ModificationTime);
            writer.WriteLine(indent + "   MFT Changed Time: " + MftChangedTime);
            writer.WriteLine(indent + "   Last Access Time: " + LastAccessTime);
            writer.WriteLine(indent + "   File Permissions: " + FileAttributes);
            writer.WriteLine(indent + "       Max Versions: " + MaxVersions);
            writer.WriteLine(indent + "            Version: " + Version);
            writer.WriteLine(indent + "           Class Id: " + ClassId);
            writer.WriteLine(indent + "        Security Id: " + SecurityId);
            writer.WriteLine(indent + "      Quota Charged: " + QuotaCharged);
            writer.WriteLine(indent + "     Update Seq Num: " + UpdateSequenceNumber);
        }

        public static StandardInformation InitializeNewFile(File file, FileAttributeFlags flags)
        {
            DateTime now = DateTime.UtcNow;

            NtfsStream siStream = file.CreateStream(AttributeType.StandardInformation, null);
            StandardInformation si = new StandardInformation();
            si.CreationTime = now;
            si.ModificationTime = now;
            si.MftChangedTime = now;
            si.LastAccessTime = now;
            si.FileAttributes = flags;
            siStream.SetContent(si);

            return si;
        }

        internal static FileAttributes ConvertFlags(FileAttributeFlags flags, bool isDirectory)
        {
            FileAttributes result = (FileAttributes)((uint)flags & 0xFFFF);

            if (isDirectory)
            {
                result |= System.IO.FileAttributes.Directory;
            }

            return result;
        }

        internal static FileAttributeFlags SetFileAttributes(FileAttributes newAttributes, FileAttributeFlags existing)
        {
            return (FileAttributeFlags)(((uint)existing & 0xFFFF0000) | ((uint)newAttributes & 0xFFFF));
        }

        private static DateTime ReadDateTime(byte[] buffer, int offset)
        {
            try
            {
                return DateTime.FromFileTimeUtc(EndianUtilities.ToInt64LittleEndian(buffer, offset));
            }
            catch (ArgumentException)
            {
                return DateTime.FromFileTimeUtc(0);
            }
        }
    }
}
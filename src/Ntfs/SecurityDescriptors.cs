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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;

namespace DiscUtils.Ntfs
{
    internal sealed class SecurityDescriptors : File
    {
        private static IComparer<HashIndexKey> _hashIndexComparer = new HashIndexKeyComparer();
        private static IComparer<IdIndexKey> _idIndexComparer = new IdIndexKeyComparer();

        private Index<HashIndexKey, IndexData> _hashIndex;
        private Index<IdIndexKey, IndexData> _idIndex;

        public SecurityDescriptors(NtfsFileSystem fileSystem, FileRecord fileRecord)
            : base(fileSystem, fileRecord)
        {
            _hashIndex = new Index<HashIndexKey, IndexData>(this, "$SDH", _fileSystem.BiosParameterBlock, _hashIndexComparer);
            _idIndex = new Index<IdIndexKey, IndexData>(this, "$SII", _fileSystem.BiosParameterBlock, _idIndexComparer);
        }

        public FileSecurity GetDescriptorById(uint id)
        {
            IndexData data = _idIndex.FindFirst(new IndexQuery(id)).Value;
            using(Stream s = OpenAttribute(AttributeType.Data, "$SDS", FileAccess.Read))
            {
                s.Position = data.SdsOffset;
                byte[] buffer = Utilities.ReadFully(s, data.SdsLength);

                SecurityDescriptorRecord record = new SecurityDescriptorRecord();
                record.Read(buffer, 0);

                FileSecurity fs = new FileSecurity();
                fs.SetSecurityDescriptorBinaryForm(record.SecurityDescriptor, AccessControlSections.All);
                return fs;
            }
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "SECURITY DESCRIPTORS");

            using (Stream s = OpenAttribute(AttributeType.Data, "$SDS", FileAccess.Read))
            {
                byte[] buffer = Utilities.ReadFully(s, (int)s.Length);


                int pos = 0;
                while (pos < buffer.Length)
                {

                    SecurityDescriptorRecord rec = new SecurityDescriptorRecord();
                    if (!rec.Read(buffer, pos))
                    {
                        break;
                    }

                    FileSecurity secDesc = new FileSecurity();
                    secDesc.SetSecurityDescriptorBinaryForm(rec.SecurityDescriptor, AccessControlSections.All);
                    string secDescStr = secDesc.GetSecurityDescriptorSddlForm(AccessControlSections.All);

                    writer.WriteLine(indent + "  SECURITY DESCRIPTOR RECORD");
                    writer.WriteLine(indent + "           Hash: " + rec.Hash);
                    writer.WriteLine(indent + "             Id: " + rec.Id);
                    writer.WriteLine(indent + "    File Offset: " + rec.OffsetInFile);
                    writer.WriteLine(indent + "           Size: " + rec.EntrySize);
                    writer.WriteLine(indent + "          Value: " + secDescStr);

                    pos = (int)Utilities.RoundUp(rec.OffsetInFile + rec.EntrySize, 16);
                }
            }
        }

        private sealed class HashIndexKey : IByteArraySerializable
        {
            public uint Hash;
            public uint Id;

            public void ReadFrom(byte[] buffer, int offset)
            {
                Hash = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
                Id = Utilities.ToUInt32LittleEndian(buffer, offset + 4);
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Utilities.WriteBytesLittleEndian(Hash, buffer, offset + 0);
                Utilities.WriteBytesLittleEndian(Id, buffer, offset + 4);
            }

            public int Size
            {
                get { return 8; }
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Key-Hash:{0},Id:{1}]", Hash, Id);
            }
        }

        private sealed class HashIndexKeyComparer : IComparer<HashIndexKey>
        {
            public int Compare(HashIndexKey x, HashIndexKey y)
            {
                if (x.Hash < y.Hash)
                {
                    return -1;
                }
                else if (x.Hash > y.Hash)
                {
                    return 1;
                }
                else if(x.Id < y.Id)
                {
                    return -1;
                }
                else if (x.Id > y.Id)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        private sealed class IdIndexKey : IByteArraySerializable
        {
            public uint Id;

            public void ReadFrom(byte[] buffer, int offset)
            {
                Id = Utilities.ToUInt32LittleEndian(buffer, offset + 0);
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Utilities.WriteBytesLittleEndian(Id, buffer, offset + 0);
            }

            public int Size
            {
                get { return 4; }
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Key-Id:{0}]", Id);
            }
        }

        private sealed class IdIndexKeyComparer : IComparer<IdIndexKey>
        {
            public int Compare(IdIndexKey x, IdIndexKey y)
            {
                if (x.Id < y.Id)
                {
                    return -1;
                }
                else if (x.Id > y.Id)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }

        private sealed class IndexData : IByteArraySerializable
        {
            public uint Hash;
            public uint Id;
            public long SdsOffset;
            public int SdsLength;

            public void ReadFrom(byte[] buffer, int offset)
            {
                Hash = Utilities.ToUInt32LittleEndian(buffer, offset + 0x00);
                Id = Utilities.ToUInt32LittleEndian(buffer, offset + 0x04);
                SdsOffset = Utilities.ToInt64LittleEndian(buffer, offset + 0x08);
                SdsLength = Utilities.ToInt32LittleEndian(buffer, offset + 0x10);
                // Padding...
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Utilities.WriteBytesLittleEndian(Hash, buffer, offset + 0x00);
                Utilities.WriteBytesLittleEndian(Id, buffer, offset + 0x04);
                Utilities.WriteBytesLittleEndian(SdsOffset, buffer, offset + 0x08);
                Utilities.WriteBytesLittleEndian(SdsLength, buffer, offset + 0x10);
            }

            public int Size
            {
                get { return 0x14; }
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Data-Hash:{0},Id:{1},SdsOffset:{2},SdsLength:{3}]", Hash, Id, SdsOffset, SdsLength);
            }
        }

        private sealed class IndexQuery : IComparable<IdIndexKey>
        {
            private uint _id;

            public IndexQuery(uint id)
            {
                _id = id;
            }

            public int CompareTo(IdIndexKey other)
            {
                if (_id < other.Id)
                {
                    return -1;
                }
                else if (_id > other.Id)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}

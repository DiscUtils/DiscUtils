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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.AccessControl;
using DiscUtils.Internal;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class SecurityDescriptors : IDiagnosticTraceable
    {
        // File consists of pairs of duplicate blocks (one after the other), providing
        // redundancy.  When a pair is full, the next pair is used.
        private const int BlockSize = 0x40000;

        private readonly File _file;
        private readonly IndexView<HashIndexKey, HashIndexData> _hashIndex;
        private readonly IndexView<IdIndexKey, IdIndexData> _idIndex;
        private uint _nextId;
        private long _nextSpace;

        public SecurityDescriptors(File file)
        {
            _file = file;
            _hashIndex = new IndexView<HashIndexKey, HashIndexData>(file.GetIndex("$SDH"));
            _idIndex = new IndexView<IdIndexKey, IdIndexData>(file.GetIndex("$SII"));

            foreach (KeyValuePair<IdIndexKey, IdIndexData> entry in _idIndex.Entries)
            {
                if (entry.Key.Id > _nextId)
                {
                    _nextId = entry.Key.Id;
                }

                long end = entry.Value.SdsOffset + entry.Value.SdsLength;
                if (end > _nextSpace)
                {
                    _nextSpace = end;
                }
            }

            if (_nextId == 0)
            {
                _nextId = 256;
            }
            else
            {
                _nextId++;
            }

            _nextSpace = MathUtilities.RoundUp(_nextSpace, 16);
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "SECURITY DESCRIPTORS");

            using (Stream s = _file.OpenStream(AttributeType.Data, "$SDS", FileAccess.Read))
            {
                byte[] buffer = StreamUtilities.ReadExact(s, (int)s.Length);

                foreach (KeyValuePair<IdIndexKey, IdIndexData> entry in _idIndex.Entries)
                {
                    int pos = (int)entry.Value.SdsOffset;

                    SecurityDescriptorRecord rec = new SecurityDescriptorRecord();
                    if (!rec.Read(buffer, pos))
                    {
                        break;
                    }

                    string secDescStr = "--unknown--";
                    if (rec.SecurityDescriptor[0] != 0)
                    {
                        RawSecurityDescriptor sd = new RawSecurityDescriptor(rec.SecurityDescriptor, 0);
                        secDescStr = sd.GetSddlForm(AccessControlSections.All);
                    }

                    writer.WriteLine(indent + "  SECURITY DESCRIPTOR RECORD");
                    writer.WriteLine(indent + "           Hash: " + rec.Hash);
                    writer.WriteLine(indent + "             Id: " + rec.Id);
                    writer.WriteLine(indent + "    File Offset: " + rec.OffsetInFile);
                    writer.WriteLine(indent + "           Size: " + rec.EntrySize);
                    writer.WriteLine(indent + "          Value: " + secDescStr);
                }
            }
        }

        public static SecurityDescriptors Initialize(File file)
        {
            file.CreateIndex("$SDH", 0, AttributeCollationRule.SecurityHash);
            file.CreateIndex("$SII", 0, AttributeCollationRule.UnsignedLong);
            file.CreateStream(AttributeType.Data, "$SDS");

            return new SecurityDescriptors(file);
        }

        public RawSecurityDescriptor GetDescriptorById(uint id)
        {
            IdIndexData data;
            if (_idIndex.TryGetValue(new IdIndexKey(id), out data))
            {
                return ReadDescriptor(data).Descriptor;
            }

            return null;
        }

        public uint AddDescriptor(RawSecurityDescriptor newDescriptor)
        {
            // Search to see if this is a known descriptor
            SecurityDescriptor newDescObj = new SecurityDescriptor(newDescriptor);
            uint newHash = newDescObj.CalcHash();
            byte[] newByteForm = new byte[newDescObj.Size];
            newDescObj.WriteTo(newByteForm, 0);

            foreach (KeyValuePair<HashIndexKey, HashIndexData> entry in _hashIndex.FindAll(new HashFinder(newHash)))
            {
                SecurityDescriptor stored = ReadDescriptor(entry.Value);

                byte[] storedByteForm = new byte[stored.Size];
                stored.WriteTo(storedByteForm, 0);

                if (Utilities.AreEqual(newByteForm, storedByteForm))
                {
                    return entry.Value.Id;
                }
            }

            long offset = _nextSpace;

            // Write the new descriptor to the end of the existing descriptors
            SecurityDescriptorRecord record = new SecurityDescriptorRecord();
            record.SecurityDescriptor = newByteForm;
            record.Hash = newHash;
            record.Id = _nextId;

            // If we'd overflow into our duplicate block, skip over it to the
            // start of the next block
            if ((offset + record.Size) / BlockSize % 2 == 1)
            {
                _nextSpace = MathUtilities.RoundUp(offset, BlockSize * 2);
                offset = _nextSpace;
            }

            record.OffsetInFile = offset;

            byte[] buffer = new byte[record.Size];
            record.WriteTo(buffer, 0);

            using (Stream s = _file.OpenStream(AttributeType.Data, "$SDS", FileAccess.ReadWrite))
            {
                s.Position = _nextSpace;
                s.Write(buffer, 0, buffer.Length);
                s.Position = BlockSize + _nextSpace;
                s.Write(buffer, 0, buffer.Length);
            }

            // Make the next descriptor land at the end of this one
            _nextSpace = MathUtilities.RoundUp(_nextSpace + buffer.Length, 16);
            _nextId++;

            // Update the indexes
            HashIndexData hashIndexData = new HashIndexData();
            hashIndexData.Hash = record.Hash;
            hashIndexData.Id = record.Id;
            hashIndexData.SdsOffset = record.OffsetInFile;
            hashIndexData.SdsLength = (int)record.EntrySize;

            HashIndexKey hashIndexKey = new HashIndexKey();
            hashIndexKey.Hash = record.Hash;
            hashIndexKey.Id = record.Id;

            _hashIndex[hashIndexKey] = hashIndexData;

            IdIndexData idIndexData = new IdIndexData();
            idIndexData.Hash = record.Hash;
            idIndexData.Id = record.Id;
            idIndexData.SdsOffset = record.OffsetInFile;
            idIndexData.SdsLength = (int)record.EntrySize;

            IdIndexKey idIndexKey = new IdIndexKey();
            idIndexKey.Id = record.Id;

            _idIndex[idIndexKey] = idIndexData;

            _file.UpdateRecordInMft();

            return record.Id;
        }

        private SecurityDescriptor ReadDescriptor(IndexData data)
        {
            using (Stream s = _file.OpenStream(AttributeType.Data, "$SDS", FileAccess.Read))
            {
                s.Position = data.SdsOffset;
                byte[] buffer = StreamUtilities.ReadExact(s, data.SdsLength);

                SecurityDescriptorRecord record = new SecurityDescriptorRecord();
                record.Read(buffer, 0);

                return new SecurityDescriptor(new RawSecurityDescriptor(record.SecurityDescriptor, 0));
            }
        }

        internal abstract class IndexData
        {
            public uint Hash;
            public uint Id;
            public int SdsLength;
            public long SdsOffset;

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Data-Hash:{0},Id:{1},SdsOffset:{2},SdsLength:{3}]",
                    Hash, Id, SdsOffset, SdsLength);
            }
        }

        internal sealed class HashIndexKey : IByteArraySerializable
        {
            public uint Hash;
            public uint Id;

            public int Size
            {
                get { return 8; }
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                Hash = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
                Id = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 4);
                return 8;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                EndianUtilities.WriteBytesLittleEndian(Hash, buffer, offset + 0);
                EndianUtilities.WriteBytesLittleEndian(Id, buffer, offset + 4);
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Key-Hash:{0},Id:{1}]", Hash, Id);
            }
        }

        internal sealed class HashIndexData : IndexData, IByteArraySerializable
        {
            public int Size
            {
                get { return 0x14; }
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                Hash = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x00);
                Id = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x04);
                SdsOffset = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x08);
                SdsLength = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x10);
                return 0x14;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                EndianUtilities.WriteBytesLittleEndian(Hash, buffer, offset + 0x00);
                EndianUtilities.WriteBytesLittleEndian(Id, buffer, offset + 0x04);
                EndianUtilities.WriteBytesLittleEndian(SdsOffset, buffer, offset + 0x08);
                EndianUtilities.WriteBytesLittleEndian(SdsLength, buffer, offset + 0x10);
                ////Array.Copy(new byte[] { (byte)'I', 0, (byte)'I', 0 }, 0, buffer, offset + 0x14, 4);
            }
        }

        internal sealed class IdIndexKey : IByteArraySerializable
        {
            public uint Id;

            public IdIndexKey() {}

            public IdIndexKey(uint id)
            {
                Id = id;
            }

            public int Size
            {
                get { return 4; }
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                Id = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0);
                return 4;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                EndianUtilities.WriteBytesLittleEndian(Id, buffer, offset + 0);
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Key-Id:{0}]", Id);
            }
        }

        internal sealed class IdIndexData : IndexData, IByteArraySerializable
        {
            public int Size
            {
                get { return 0x14; }
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                Hash = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x00);
                Id = EndianUtilities.ToUInt32LittleEndian(buffer, offset + 0x04);
                SdsOffset = EndianUtilities.ToInt64LittleEndian(buffer, offset + 0x08);
                SdsLength = EndianUtilities.ToInt32LittleEndian(buffer, offset + 0x10);
                return 0x14;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                EndianUtilities.WriteBytesLittleEndian(Hash, buffer, offset + 0x00);
                EndianUtilities.WriteBytesLittleEndian(Id, buffer, offset + 0x04);
                EndianUtilities.WriteBytesLittleEndian(SdsOffset, buffer, offset + 0x08);
                EndianUtilities.WriteBytesLittleEndian(SdsLength, buffer, offset + 0x10);
            }
        }

        private class HashFinder : IComparable<HashIndexKey>
        {
            private readonly uint _toMatch;

            public HashFinder(uint toMatch)
            {
                _toMatch = toMatch;
            }

            public int CompareTo(HashIndexKey other)
            {
                return CompareTo(other.Hash);
            }

            public int CompareTo(uint otherHash)
            {
                if (_toMatch < otherHash)
                {
                    return -1;
                }
                if (_toMatch > otherHash)
                {
                    return 1;
                }

                return 0;
            }
        }
    }
}
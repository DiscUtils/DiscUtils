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

using System;
using System.Globalization;
using System.IO;
using System.Security.Principal;

namespace DiscUtils.Ntfs
{
    internal sealed class Quotas
    {
        private IndexView<OwnerKey, OwnerRecord> _ownerIndex;
        private IndexView<OwnerRecord, QuotaRecord> _quotaIndex;

        public Quotas(File file)
        {
            _ownerIndex = new IndexView<OwnerKey, OwnerRecord>(file.GetIndex("$O"));
            _quotaIndex = new IndexView<OwnerRecord, QuotaRecord>(file.GetIndex("$Q"));
        }

        public static Quotas Initialize(File file)
        {
            Index ownerIndex = file.CreateIndex("$O", (AttributeType)0, AttributeCollationRule.Sid);
            Index quotaIndox = file.CreateIndex("$Q", (AttributeType)0, AttributeCollationRule.UnsignedLong);

            IndexView<OwnerKey, OwnerRecord> ownerIndexView = new IndexView<OwnerKey, OwnerRecord>(ownerIndex);
            IndexView<OwnerRecord, QuotaRecord> quotaIndexView = new IndexView<OwnerRecord, QuotaRecord>(quotaIndox);

            OwnerKey adminSid = new OwnerKey(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
            OwnerRecord adminOwnerId = new OwnerRecord(256);

            ownerIndexView[adminSid] = adminOwnerId;

            quotaIndexView[new OwnerRecord(1)] = new QuotaRecord(null);
            quotaIndexView[adminOwnerId] = new QuotaRecord(adminSid.Sid);

            return new Quotas(file);
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "QUOTAS");

            writer.WriteLine(indent + "  OWNER INDEX");
            foreach (var entry in _ownerIndex.Entries)
            {
                writer.WriteLine(indent + "    OWNER INDEX ENTRY");
                writer.WriteLine(indent + "            SID: " + entry.Key.Sid);
                writer.WriteLine(indent + "       Owner Id: " + entry.Value.OwnerId);
            }

            writer.WriteLine(indent + "  QUOTA INDEX");
            foreach (var entry in _quotaIndex.Entries)
            {
                writer.WriteLine(indent + "    QUOTA INDEX ENTRY");
                writer.WriteLine(indent + "           Owner Id: " + entry.Key.OwnerId);
                writer.WriteLine(indent + "           User SID: " + entry.Value.Sid);
                writer.WriteLine(indent + "            Changed: " + entry.Value.ChangeTime);
                writer.WriteLine(indent + "           Exceeded: " + entry.Value.ExceededTime);
                writer.WriteLine(indent + "         Bytes Used: " + entry.Value.BytesUsed);
                writer.WriteLine(indent + "              Flags: " + entry.Value.Flags);
                writer.WriteLine(indent + "         Hard Limit: " + entry.Value.HardLimit);
                writer.WriteLine(indent + "      Warning Limit: " + entry.Value.WarningLimit);
                writer.WriteLine(indent + "            Version: " + entry.Value.Version);
            }
        }

        internal sealed class OwnerKey : IByteArraySerializable
        {
            public SecurityIdentifier Sid;

            public OwnerKey()
            {
            }

            public OwnerKey(SecurityIdentifier sid)
            {
                Sid = sid;
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                Sid = new SecurityIdentifier(buffer, offset);
                return Sid.BinaryLength;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Sid.GetBinaryForm(buffer, offset);
            }

            public int Size
            {
                get { return Sid.BinaryLength; }
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[Sid:{0}]", Sid);
            }
        }

        internal sealed class OwnerRecord : IByteArraySerializable
        {
            public int OwnerId;

            public OwnerRecord()
            {
            }

            public OwnerRecord(int ownerId)
            {
                OwnerId = ownerId;
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                OwnerId = Utilities.ToInt32LittleEndian(buffer, offset);
                return 4;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Utilities.WriteBytesLittleEndian(OwnerId, buffer, offset);
            }

            public int Size
            {
                get { return 4; }
            }

            public override string ToString()
            {
                return "[OwnerId:" + OwnerId + "]";
            }
        }

        internal sealed class QuotaRecord : IByteArraySerializable
        {
            public int Version;
            public int Flags;
            public long BytesUsed;
            public DateTime ChangeTime;
            public long WarningLimit;
            public long HardLimit;
            public long ExceededTime;
            public SecurityIdentifier Sid;

            public QuotaRecord()
            {
            }

            public QuotaRecord(SecurityIdentifier sid)
            {
                Version = 2;
                Flags = 1;
                ChangeTime = DateTime.UtcNow;
                WarningLimit = -1;
                HardLimit = -1;
                Sid = sid;
            }

            public int ReadFrom(byte[] buffer, int offset)
            {
                Version = Utilities.ToInt32LittleEndian(buffer, offset);
                Flags = Utilities.ToInt32LittleEndian(buffer, offset + 0x04);
                BytesUsed = Utilities.ToInt64LittleEndian(buffer, offset + 0x08);
                ChangeTime = DateTime.FromFileTimeUtc(Utilities.ToInt64LittleEndian(buffer, offset + 0x10));
                WarningLimit = Utilities.ToInt64LittleEndian(buffer, offset + 0x18);
                HardLimit = Utilities.ToInt64LittleEndian(buffer, offset + 0x20);
                ExceededTime = Utilities.ToInt64LittleEndian(buffer, offset + 0x28);
                if (buffer.Length - offset > 0x30)
                {
                    Sid = new SecurityIdentifier(buffer, offset + 0x30);
                    return 0x30 + Sid.BinaryLength;
                }

                return 0x30;
            }

            public void WriteTo(byte[] buffer, int offset)
            {
                Utilities.WriteBytesLittleEndian(Version, buffer, offset);
                Utilities.WriteBytesLittleEndian(Flags, buffer, offset + 0x04);
                Utilities.WriteBytesLittleEndian(BytesUsed, buffer, offset + 0x08);
                Utilities.WriteBytesLittleEndian(ChangeTime.ToFileTimeUtc(), buffer, offset + 0x10);
                Utilities.WriteBytesLittleEndian(WarningLimit, buffer, offset + 0x18);
                Utilities.WriteBytesLittleEndian(HardLimit, buffer, offset + 0x20);
                Utilities.WriteBytesLittleEndian(ExceededTime, buffer, offset + 0x28);
                if (Sid != null)
                {
                    Sid.GetBinaryForm(buffer, offset + 0x30);
                }
            }

            public int Size
            {
                get { return 0x30 + (Sid == null ? 0 : Sid.BinaryLength); }
            }

            public override string ToString()
            {
                return "[V:" + Version + ",F:" + Flags + ",BU:" + BytesUsed + ",CT:" + ChangeTime + ",WL:" + WarningLimit + ",HL:" + HardLimit + ",ET:" + ExceededTime + ",SID:" + Sid + "]";
            }
        }
    }
}

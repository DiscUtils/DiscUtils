//
// Copyright (c) 2008-2012, Kenneth Bell
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
using DiscUtils.Streams;

namespace DiscUtils.Vhdx
{
    internal sealed class MetadataTable : IByteArraySerializable
    {
        public const int FixedSize = (int)(64 * Sizes.OneKiB);
        public const ulong MetadataTableSignature = 0x617461646174656D;

        public static readonly Guid FileParametersGuid = new Guid("CAA16737-FA36-4D43-B3B6-33F0AA44E76B");
        public static readonly Guid VirtualDiskSizeGuid = new Guid("2FA54224-CD1B-4876-B211-5DBED83BF4B8");
        public static readonly Guid Page83DataGuid = new Guid("BECA12AB-B2E6-4523-93EF-C309E000C746");
        public static readonly Guid LogicalSectorSizeGuid = new Guid("8141Bf1D-A96F-4709-BA47-F233A8FAAb5F");
        public static readonly Guid PhysicalSectorSizeGuid = new Guid("CDA348C7-445D-4471-9CC9-E9885251C556");
        public static readonly Guid ParentLocatorGuid = new Guid("A8D35F2D-B30B-454D-ABF7-D3D84834AB0C");

        private static readonly Dictionary<Guid, object> KnownMetadata = InitMetadataTable();

        private readonly byte[] _headerData = new byte[32];
        public IDictionary<MetadataEntryKey, MetadataEntry> Entries = new Dictionary<MetadataEntryKey, MetadataEntry>();
        public ushort EntryCount;

        public ulong Signature = MetadataTableSignature;

        public bool IsValid
        {
            get
            {
                if (Signature != MetadataTableSignature)
                {
                    return false;
                }

                if (EntryCount > 2047)
                {
                    return false;
                }

                foreach (MetadataEntry entry in Entries.Values)
                {
                    if ((entry.Flags & MetadataEntryFlags.IsRequired) != 0)
                    {
                        if (!KnownMetadata.ContainsKey(entry.ItemId))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public int Size
        {
            get { return FixedSize; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            Array.Copy(buffer, offset, _headerData, 0, 32);

            Signature = EndianUtilities.ToUInt64LittleEndian(_headerData, 0);
            EntryCount = EndianUtilities.ToUInt16LittleEndian(_headerData, 10);

            Entries = new Dictionary<MetadataEntryKey, MetadataEntry>();
            if (IsValid)
            {
                for (int i = 0; i < EntryCount; ++i)
                {
                    MetadataEntry entry = EndianUtilities.ToStruct<MetadataEntry>(buffer, offset + 32 + i * 32);
                    Entries[MetadataEntryKey.FromEntry(entry)] = entry;
                }
            }

            return FixedSize;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EntryCount = (ushort)Entries.Count;
            EndianUtilities.WriteBytesLittleEndian(Signature, _headerData, 0);
            EndianUtilities.WriteBytesLittleEndian(EntryCount, _headerData, 10);

            Array.Copy(_headerData, 0, buffer, offset, 32);

            int bufferOffset = 32 + offset;
            foreach (KeyValuePair<MetadataEntryKey, MetadataEntry> entry in Entries)
            {
                entry.Value.WriteTo(buffer, bufferOffset);
                bufferOffset += 32;
            }
        }

        private static Dictionary<Guid, object> InitMetadataTable()
        {
            Dictionary<Guid, object> knownMetadata = new Dictionary<Guid, object>();
            knownMetadata[FileParametersGuid] = null;
            knownMetadata[VirtualDiskSizeGuid] = null;
            knownMetadata[Page83DataGuid] = null;
            knownMetadata[LogicalSectorSizeGuid] = null;
            knownMetadata[PhysicalSectorSizeGuid] = null;
            knownMetadata[ParentLocatorGuid] = null;
            return knownMetadata;
        }
    }
}
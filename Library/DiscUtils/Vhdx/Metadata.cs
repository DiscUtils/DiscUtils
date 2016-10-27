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

namespace DiscUtils.Vhdx
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
#if !NETCORE
    using System.Security.Permissions;
#endif

    internal sealed class Metadata
    {
        private Stream _regionStream;
        private MetadataTable _table;

        private FileParameters _fileParams;
        private ulong _diskSize;
        private Guid _page83Data;
        private uint _logicalSectorSize;
        private uint _physicalSectorSize;
        private ParentLocator _parentLocator;

        public Metadata(Stream regionStream)
        {
            _regionStream = regionStream;
            _regionStream.Position = 0;
            _table = Utilities.ReadStruct<MetadataTable>(_regionStream);

            _fileParams = ReadStruct<FileParameters>(MetadataTable.FileParametersGuid, false);
            _diskSize = ReadValue<ulong>(MetadataTable.VirtualDiskSizeGuid, false, Utilities.ToUInt64LittleEndian);
            _page83Data = ReadValue<Guid>(MetadataTable.Page83DataGuid, false, Utilities.ToGuidLittleEndian);
            _logicalSectorSize = ReadValue<uint>(MetadataTable.LogicalSectorSizeGuid, false, Utilities.ToUInt32LittleEndian);
            _physicalSectorSize = ReadValue<uint>(MetadataTable.PhysicalSectorSizeGuid, false, Utilities.ToUInt32LittleEndian);
            _parentLocator = ReadStruct<ParentLocator>(MetadataTable.ParentLocatorGuid, false);
        }

        private delegate T Reader<T>(byte[] buffer, int offset);

        private delegate void Writer<T>(T val, byte[] buffer, int offset);

        public MetadataTable Table
        {
            get { return _table; }
        }

        public FileParameters FileParameters
        {
            get { return _fileParams; }
        }

        public ulong DiskSize
        {
            get { return _diskSize; }
        }

        public uint LogicalSectorSize
        {
            get { return _logicalSectorSize; }
        }

        public uint PhysicalSectorSize
        {
            get { return _physicalSectorSize; }
        }

        public ParentLocator ParentLocator
        {
            get { return _parentLocator; }
        }

        internal static Metadata Initialize(Stream metadataStream, FileParameters fileParameters, ulong diskSize, uint logicalSectorSize, uint physicalSectorSize, ParentLocator parentLocator)
        {
            MetadataTable header = new MetadataTable();

            uint dataOffset = (uint)(64 * Sizes.OneKiB);
            dataOffset += AddEntryStruct(fileParameters, MetadataTable.FileParametersGuid, MetadataEntryFlags.IsRequired, header, dataOffset, metadataStream);
            dataOffset += AddEntryValue(diskSize, Utilities.WriteBytesLittleEndian, MetadataTable.VirtualDiskSizeGuid, MetadataEntryFlags.IsRequired | MetadataEntryFlags.IsVirtualDisk, header, dataOffset, metadataStream);
            dataOffset += AddEntryValue(Guid.NewGuid(), Utilities.WriteBytesLittleEndian, MetadataTable.Page83DataGuid, MetadataEntryFlags.IsRequired | MetadataEntryFlags.IsVirtualDisk, header, dataOffset, metadataStream);
            dataOffset += AddEntryValue(logicalSectorSize, Utilities.WriteBytesLittleEndian, MetadataTable.LogicalSectorSizeGuid, MetadataEntryFlags.IsRequired | MetadataEntryFlags.IsVirtualDisk, header, dataOffset, metadataStream);
            dataOffset += AddEntryValue(physicalSectorSize, Utilities.WriteBytesLittleEndian, MetadataTable.PhysicalSectorSizeGuid, MetadataEntryFlags.IsRequired | MetadataEntryFlags.IsVirtualDisk, header, dataOffset, metadataStream);
            if (parentLocator != null)
            {
                dataOffset += AddEntryStruct(parentLocator, MetadataTable.ParentLocatorGuid, MetadataEntryFlags.IsRequired, header, dataOffset, metadataStream);
            }

            metadataStream.Position = 0;
            Utilities.WriteStruct(metadataStream, header);
            return new Metadata(metadataStream);
        }

        private static uint AddEntryStruct<T>(T data, Guid id, MetadataEntryFlags flags, MetadataTable header, uint dataOffset, Stream stream)
            where T : IByteArraySerializable
        {
            MetadataEntryKey key = new MetadataEntryKey(id, (flags & MetadataEntryFlags.IsUser) != 0);
            MetadataEntry entry = new MetadataEntry();
            entry.ItemId = id;
            entry.Offset = dataOffset;
            entry.Length = (uint)data.Size;
            entry.Flags = flags;

            header.Entries[key] = entry;

            stream.Position = dataOffset;
            Utilities.WriteStruct(stream, data);

            return entry.Length;
        }

#if !NETCORE
        [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
#endif
        private static uint AddEntryValue<T>(T data, Writer<T> writer, Guid id, MetadataEntryFlags flags, MetadataTable header, uint dataOffset, Stream stream)
        {
            MetadataEntryKey key = new MetadataEntryKey(id, (flags & MetadataEntryFlags.IsUser) != 0);
            MetadataEntry entry = new MetadataEntry();
            entry.ItemId = id;
            entry.Offset = dataOffset;
            entry.Length = (uint)Marshal.SizeOf(typeof(T));
            entry.Flags = flags;

            header.Entries[key] = entry;

            stream.Position = dataOffset;

            byte[] buffer = new byte[entry.Length];
            writer(data, buffer, 0);
            stream.Write(buffer, 0, buffer.Length);

            return entry.Length;
        }

        private T ReadStruct<T>(Guid itemId, bool isUser)
            where T : IByteArraySerializable, new()
        {
            MetadataEntryKey key = new MetadataEntryKey(itemId, isUser);
            MetadataEntry entry;
            if (_table.Entries.TryGetValue(key, out entry))
            {
                _regionStream.Position = entry.Offset;
                return Utilities.ReadStruct<T>(_regionStream, (int)entry.Length);
            }

            return default(T);
        }

#if !NETCORE
        [SecurityPermission(SecurityAction.Demand, UnmanagedCode = true)]
#endif
        private T ReadValue<T>(Guid itemId, bool isUser, Reader<T> reader)
        {
            MetadataEntryKey key = new MetadataEntryKey(itemId, isUser);
            MetadataEntry entry;
            if (_table.Entries.TryGetValue(key, out entry))
            {
                _regionStream.Position = entry.Offset;
                byte[] data = Utilities.ReadFully(_regionStream, Marshal.SizeOf(typeof(T)));
                return reader(data, 0);
            }

            return default(T);
        }
    }
}

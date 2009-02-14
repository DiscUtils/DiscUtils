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

namespace DiscUtils.Vmdk
{
    internal class DescriptorFile
    {
        private List<DescriptorFileEntry> _header;
        private List<ExtentDescriptor> _descriptors;
        private List<DescriptorFileEntry> _diskDataBase;

        private const string HeaderVersion = "version";
        private const string HeaderContentId = "CID";
        private const string HeaderParentContentId = "parentCID";
        private const string HeaderCreateType = "createType";
        private const string HeaderParentFileNameHint = "parentFileNameHint";

        private const string DiskDbAdapterType = "ddb.adapterType";
        private const string DiskDbSectors = "ddb.geometry.sectors";
        private const string DiskDbHeads = "ddb.geometry.heads";
        private const string DiskDbCylinders = "ddb.geometry.cylinders";
        private const string DiskDbHardwareVersion = "ddb.virtualHWVersion";
        private const string DiskDbUuid = "ddb.uuid";

        public DescriptorFile(Stream source)
        {
            _header = new List<DescriptorFileEntry>();
            _descriptors = new List<ExtentDescriptor>();
            _diskDataBase = new List<DescriptorFileEntry>();

            Load(source);
        }

        public uint ContentId
        {
            get { return uint.Parse(GetHeader(HeaderContentId), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        }

        public uint ParentContentId
        {
            get { return uint.Parse(GetHeader(HeaderParentContentId), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        }

        public DiskCreateType CreateType
        {
            get { return ParseCreateType(GetHeader(HeaderCreateType)); }
        }

        public string ParentFileNameHint
        {
            get { return GetHeader(HeaderParentFileNameHint); }
        }

        public List<ExtentDescriptor> Extents
        {
            get { return _descriptors; }
        }

        public Geometry DiskGeometry
        {
            get
            {
                return new Geometry(
                    int.Parse(GetDiskDatabase(DiskDbCylinders), CultureInfo.InvariantCulture),
                    int.Parse(GetDiskDatabase(DiskDbHeads), CultureInfo.InvariantCulture),
                    int.Parse(GetDiskDatabase(DiskDbSectors), CultureInfo.InvariantCulture));
            }
        }

        public Guid UniqueId
        {
            get { return ParseUuid(GetDiskDatabase(DiskDbUuid)); }
        }

        public DiskAdapterType AdaptorType
        {
            get { return ParseAdapterType(GetDiskDatabase(DiskDbAdapterType)); }
        }

        private static DiskAdapterType ParseAdapterType(string value)
        {
            switch (value)
            {
                case "ide":
                    return DiskAdapterType.Ide;
                case "buslogic":
                    return DiskAdapterType.BusLogicScsi;
                case "lsilogic":
                    return DiskAdapterType.LsiLogicScsi;
                case "legacyESX":
                    return DiskAdapterType.LegacyESX;
                default:
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unknown type: {0}", value), "value");
            }
        }

        private static DiskCreateType ParseCreateType(string value)
        {
            switch (value)
            {
                case "monolithicSparse":
                    return DiskCreateType.MonolithicSparse;
                case "vmfsSparse":
                    return DiskCreateType.VmfsSparse;
                case "monolithicFlat":
                    return DiskCreateType.MonolithicFlat;
                case "vmfs":
                    return DiskCreateType.Vmfs;
                case "twoGbMaxExtentSparse":
                    return DiskCreateType.TwoGbMaxExtentSparse;
                case "twoGbMaxExtentFlat":
                    return DiskCreateType.TwoGbMaxExtentFlat;
                case "fullDevice":
                    return DiskCreateType.FullDevice;
                case "vmfsRaw":
                    return DiskCreateType.VmfsRaw;
                case "partitionedDevice":
                    return DiskCreateType.PartitionedDevice;
                case "vmfsRawDeviceMap":
                    return DiskCreateType.VmfsRawDeviceMap;
                case "vmfsPassthroughRawDeviceMap":
                    return DiskCreateType.VmfsPassthroughRawDeviceMap;
                case "streamOptimized":
                    return DiskCreateType.StreamOptimized;
                default:
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Unknown type: {0}", value), "value");
            }
        }

        private static Guid ParseUuid(string value)
        {
            byte[] data = new byte[16];
            string[] bytesAsHex = value.Split(' ', '-');
            if (bytesAsHex.Length != 16)
            {
                throw new ArgumentException("Invalid UUID", "value");
            }

            for (int i = 0; i < 16; ++i)
            {
                data[i] = byte.Parse(bytesAsHex[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return new Guid(data);
        }

        private string GetHeader(string key)
        {
            foreach (var entry in _header)
            {
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }
            return null;
        }

        private string GetDiskDatabase(string key)
        {
            foreach (var entry in _diskDataBase)
            {
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }
            return null;
        }

        private void Load(Stream source)
        {
            StreamReader reader = new StreamReader(source);
            string line = reader.ReadLine();
            while (line != null)
            {
                int commentPos = line.IndexOf('#');
                if (commentPos >= 0)
                {
                    line = line.Substring(0, commentPos);
                }

                if (line.StartsWith("RW", StringComparison.Ordinal)
                    || line.StartsWith("RDONLY", StringComparison.Ordinal)
                    || line.StartsWith("NOACCESS", StringComparison.Ordinal))
                {
                    _descriptors.Add(ExtentDescriptor.Parse(line));
                }
                else
                {
                    DescriptorFileEntry entry = DescriptorFileEntry.Parse(line);
                    if (entry.Key.StartsWith("ddb.", StringComparison.Ordinal))
                    {
                        _diskDataBase.Add(entry);
                    }
                    else
                    {
                        _header.Add(entry);
                    }
                }

                line = reader.ReadLine();
            }
        }
    }
}

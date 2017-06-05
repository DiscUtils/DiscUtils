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
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Vmdk
{
    internal class DescriptorFile
    {
        private const string HeaderVersion = "version";
        private const string HeaderContentId = "CID";
        private const string HeaderParentContentId = "parentCID";
        private const string HeaderCreateType = "createType";
        private const string HeaderParentFileNameHint = "parentFileNameHint";

        private const string DiskDbAdapterType = "ddb.adapterType";
        private const string DiskDbSectors = "ddb.geometry.sectors";
        private const string DiskDbHeads = "ddb.geometry.heads";
        private const string DiskDbCylinders = "ddb.geometry.cylinders";
        private const string DiskDbBiosSectors = "ddb.geometry.biosSectors";
        private const string DiskDbBiosHeads = "ddb.geometry.biosHeads";
        private const string DiskDbBiosCylinders = "ddb.geometry.biosCylinders";
        private const string DiskDbHardwareVersion = "ddb.virtualHWVersion";
        private const string DiskDbUuid = "ddb.uuid";

        private const long MaxSize = 20 * Sizes.OneKiB;
        private readonly List<DescriptorFileEntry> _diskDataBase;

        private readonly List<DescriptorFileEntry> _header;

        public DescriptorFile()
        {
            _header = new List<DescriptorFileEntry>();
            Extents = new List<ExtentDescriptor>();
            _diskDataBase = new List<DescriptorFileEntry>();

            _header.Add(new DescriptorFileEntry(HeaderVersion, "1", DescriptorFileEntryType.Plain));
            _header.Add(new DescriptorFileEntry(HeaderContentId, "ffffffff", DescriptorFileEntryType.Plain));
            _header.Add(new DescriptorFileEntry(HeaderParentContentId, "ffffffff", DescriptorFileEntryType.Plain));
            _header.Add(new DescriptorFileEntry(HeaderCreateType, string.Empty, DescriptorFileEntryType.Quoted));
        }

        public DescriptorFile(Stream source)
        {
            _header = new List<DescriptorFileEntry>();
            Extents = new List<ExtentDescriptor>();
            _diskDataBase = new List<DescriptorFileEntry>();

            Load(source);
        }

        public DiskAdapterType AdapterType
        {
            get { return ParseAdapterType(GetDiskDatabase(DiskDbAdapterType)); }
            set { SetDiskDatabase(DiskDbAdapterType, FormatAdapterType(value)); }
        }

        public Geometry BiosGeometry
        {
            get
            {
                string cylStr = GetDiskDatabase(DiskDbBiosCylinders);
                string headsStr = GetDiskDatabase(DiskDbBiosHeads);
                string sectorsStr = GetDiskDatabase(DiskDbBiosSectors);
                if (!string.IsNullOrEmpty(cylStr) && !string.IsNullOrEmpty(headsStr) &&
                    !string.IsNullOrEmpty(sectorsStr))
                {
                    return new Geometry(
                        int.Parse(cylStr, CultureInfo.InvariantCulture),
                        int.Parse(headsStr, CultureInfo.InvariantCulture),
                        int.Parse(sectorsStr, CultureInfo.InvariantCulture));
                }

                return null;
            }

            set
            {
                SetDiskDatabase(DiskDbBiosCylinders, value.Cylinders.ToString(CultureInfo.InvariantCulture));
                SetDiskDatabase(DiskDbBiosHeads, value.HeadsPerCylinder.ToString(CultureInfo.InvariantCulture));
                SetDiskDatabase(DiskDbBiosSectors, value.SectorsPerTrack.ToString(CultureInfo.InvariantCulture));
            }
        }

        public uint ContentId
        {
            get { return uint.Parse(GetHeader(HeaderContentId), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
            set
            {
                SetHeader(HeaderContentId, value.ToString("x8", CultureInfo.InvariantCulture),
                    DescriptorFileEntryType.Plain);
            }
        }

        public DiskCreateType CreateType
        {
            get { return ParseCreateType(GetHeader(HeaderCreateType)); }
            set { SetHeader(HeaderCreateType, FormatCreateType(value), DescriptorFileEntryType.Plain); }
        }

        public Geometry DiskGeometry
        {
            get
            {
                string cylStr = GetDiskDatabase(DiskDbCylinders);
                string headsStr = GetDiskDatabase(DiskDbHeads);
                string sectorsStr = GetDiskDatabase(DiskDbSectors);
                if (!string.IsNullOrEmpty(cylStr) && !string.IsNullOrEmpty(headsStr) &&
                    !string.IsNullOrEmpty(sectorsStr))
                {
                    return new Geometry(
                        int.Parse(cylStr, CultureInfo.InvariantCulture),
                        int.Parse(headsStr, CultureInfo.InvariantCulture),
                        int.Parse(sectorsStr, CultureInfo.InvariantCulture));
                }

                return null;
            }

            set
            {
                SetDiskDatabase(DiskDbCylinders, value.Cylinders.ToString(CultureInfo.InvariantCulture));
                SetDiskDatabase(DiskDbHeads, value.HeadsPerCylinder.ToString(CultureInfo.InvariantCulture));
                SetDiskDatabase(DiskDbSectors, value.SectorsPerTrack.ToString(CultureInfo.InvariantCulture));
            }
        }

        public List<ExtentDescriptor> Extents { get; }

        public string HardwareVersion
        {
            get { return GetDiskDatabase(DiskDbHardwareVersion); }
            set { SetDiskDatabase(DiskDbHardwareVersion, value); }
        }

        public uint ParentContentId
        {
            get { return uint.Parse(GetHeader(HeaderParentContentId), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
            set
            {
                SetHeader(HeaderParentContentId, value.ToString("x8", CultureInfo.InvariantCulture),
                    DescriptorFileEntryType.Plain);
            }
        }

        public string ParentFileNameHint
        {
            get { return GetHeader(HeaderParentFileNameHint); }
            set { SetHeader(HeaderParentFileNameHint, value, DescriptorFileEntryType.Quoted); }
        }

        public Guid UniqueId
        {
            get { return ParseUuid(GetDiskDatabase(DiskDbUuid)); }
            set { SetDiskDatabase(DiskDbUuid, FormatUuid(value)); }
        }

        internal void Write(Stream stream)
        {
            StringBuilder content = new StringBuilder();

            content.Append("# Disk DescriptorFile\n");
            for (int i = 0; i < _header.Count; ++i)
            {
                content.Append(_header[i].ToString(false) + "\n");
            }

            content.Append("\n");
            content.Append("# Extent description\n");
            for (int i = 0; i < Extents.Count; ++i)
            {
                content.Append(Extents[i] + "\n");
            }

            content.Append("\n");
            content.Append("# The Disk Data Base\n");
            content.Append("#DDB\n");
            for (int i = 0; i < _diskDataBase.Count; ++i)
            {
                content.Append(_diskDataBase[i].ToString(true) + "\n");
            }

            byte[] contentBytes = Encoding.ASCII.GetBytes(content.ToString());
            stream.Write(contentBytes, 0, contentBytes.Length);
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
                    return DiskAdapterType.LegacyEsx;
                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Unknown type: {0}", value), nameof(value));
            }
        }

        private static string FormatAdapterType(DiskAdapterType value)
        {
            switch (value)
            {
                case DiskAdapterType.Ide:
                    return "ide";
                case DiskAdapterType.BusLogicScsi:
                    return "buslogic";
                case DiskAdapterType.LsiLogicScsi:
                    return "lsilogic";
                case DiskAdapterType.LegacyEsx:
                    return "legacyESX";
                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Unknown type: {0}", value), nameof(value));
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
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Unknown type: {0}", value), nameof(value));
            }
        }

        private static string FormatCreateType(DiskCreateType value)
        {
            switch (value)
            {
                case DiskCreateType.MonolithicSparse:
                    return "monolithicSparse";
                case DiskCreateType.VmfsSparse:
                    return "vmfsSparse";
                case DiskCreateType.MonolithicFlat:
                    return "monolithicFlat";
                case DiskCreateType.Vmfs:
                    return "vmfs";
                case DiskCreateType.TwoGbMaxExtentSparse:
                    return "twoGbMaxExtentSparse";
                case DiskCreateType.TwoGbMaxExtentFlat:
                    return "twoGbMaxExtentFlat";
                case DiskCreateType.FullDevice:
                    return "fullDevice";
                case DiskCreateType.VmfsRaw:
                    return "vmfsRaw";
                case DiskCreateType.PartitionedDevice:
                    return "partitionedDevice";
                case DiskCreateType.VmfsRawDeviceMap:
                    return "vmfsRawDeviceMap";
                case DiskCreateType.VmfsPassthroughRawDeviceMap:
                    return "vmfsPassthroughRawDeviceMap";
                case DiskCreateType.StreamOptimized:
                    return "streamOptimized";
                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Unknown type: {0}", value), nameof(value));
            }
        }

        private static Guid ParseUuid(string value)
        {
            byte[] data = new byte[16];
            string[] bytesAsHex = value.Split(' ', '-');
            if (bytesAsHex.Length != 16)
            {
                throw new ArgumentException("Invalid UUID", nameof(value));
            }

            for (int i = 0; i < 16; ++i)
            {
                data[i] = byte.Parse(bytesAsHex[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return new Guid(data);
        }

        private static string FormatUuid(Guid value)
        {
            byte[] data = value.ToByteArray();
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:x2} {1:x2} {2:x2} {3:x2} {4:x2} {5:x2} {6:x2} {7:x2}-{8:x2} {9:x2} {10:x2} {11:x2} {12:x2} {13:x2} {14:x2} {15:x2}",
                data[0],
                data[1],
                data[2],
                data[3],
                data[4],
                data[5],
                data[6],
                data[7],
                data[8],
                data[9],
                data[10],
                data[11],
                data[12],
                data[13],
                data[14],
                data[15]);
        }

        private string GetHeader(string key)
        {
            foreach (DescriptorFileEntry entry in _header)
            {
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private void SetHeader(string key, string newValue, DescriptorFileEntryType type)
        {
            foreach (DescriptorFileEntry entry in _header)
            {
                if (entry.Key == key)
                {
                    entry.Value = newValue;
                    return;
                }
            }

            _header.Add(new DescriptorFileEntry(key, newValue, type));
        }

        private string GetDiskDatabase(string key)
        {
            foreach (DescriptorFileEntry entry in _diskDataBase)
            {
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private void SetDiskDatabase(string key, string value)
        {
            foreach (DescriptorFileEntry entry in _diskDataBase)
            {
                if (entry.Key == key)
                {
                    entry.Value = value;
                    return;
                }
            }

            _diskDataBase.Add(new DescriptorFileEntry(key, value, DescriptorFileEntryType.Quoted));
        }

        private void Load(Stream source)
        {
            if (source.Length - source.Position > MaxSize)
            {
                throw new IOException(string.Format(CultureInfo.InvariantCulture,
                    "Invalid VMDK descriptor file, more than {0} bytes in length", MaxSize));
            }

            StreamReader reader = new StreamReader(source);
            string line = reader.ReadLine();
            while (line != null)
            {
                line = line.Trim('\0');

                int commentPos = line.IndexOf('#');
                if (commentPos >= 0)
                {
                    line = line.Substring(0, commentPos);
                }

                if (line.Length > 0)
                {
                    if (line.StartsWith("RW", StringComparison.Ordinal)
                        || line.StartsWith("RDONLY", StringComparison.Ordinal)
                        || line.StartsWith("NOACCESS", StringComparison.Ordinal))
                    {
                        Extents.Add(ExtentDescriptor.Parse(line));
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
                }

                line = reader.ReadLine();
            }
        }
    }
}
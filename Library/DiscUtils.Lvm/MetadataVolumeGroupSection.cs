//
// Copyright (c) 2016, Bianco Veigel
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

namespace DiscUtils.Lvm
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class MetadataVolumeGroupSection
    {
        public string Name;
        public string Id;
        public ulong SequenceNumber;
        public string Format;
        public VolumeGroupStatus Status;
        public string[] Flags;
        public ulong ExtentSize;
        public ulong MaxLv;
        public ulong MaxPv;
        public ulong MetadataCopies;

        public MetadataPhysicalVolumeSection[] PhysicalVolumes;
        public MetadataLogicalVolumeSection[] LogicalVolumes;


        internal void Parse(string head, TextReader data)
        {
            Name = head.Trim().TrimEnd('{').TrimEnd();
            string line;
            while ((line = Metadata.ReadLine(data)) != null)
            {
                if (line == String.Empty) continue;
                if (line.Contains("="))
                {
                    var parameter = Metadata.ParseParameter(line);
                    switch (parameter.Key.Trim().ToLowerInvariant())
                    {
                        case "id":
                            Id = Metadata.ParseStringValue(parameter.Value);
                            break;
                        case "seqno":
                            SequenceNumber = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        case "format":
                            Format = Metadata.ParseStringValue(parameter.Value);
                            break;
                        case "status":
                            var values = Metadata.ParseArrayValue(parameter.Value);
                            foreach (var value in values)
                            {
                                switch (value.ToLowerInvariant().Trim())
                                {
                                    case "read":
                                        Status |= VolumeGroupStatus.Read;
                                        break;
                                    case "write":
                                        Status |= VolumeGroupStatus.Write;
                                        break;
                                    case "resizeable":
                                        Status |= VolumeGroupStatus.Resizeable;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException("status", "Unexpected status in volume group metadata");
                                }
                            }
                            break;
                        case "flags":
                            Flags = Metadata.ParseArrayValue(parameter.Value);
                            break;
                        case "extent_size":
                            ExtentSize = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        case "max_lv":
                            MaxLv = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        case "max_pv":
                            MaxPv = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        case "metadata_copies":
                            MetadataCopies = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(parameter.Key, "Unexpected parameter in volume group metadata");
                    }
                }
                else if (line.EndsWith("{"))
                {
                    var sectionName = line.TrimEnd('{').TrimEnd().ToLowerInvariant();
                    switch (sectionName)
                    {
                        case "physical_volumes":
                            PhysicalVolumes = ParsePhysicalVolumeSection(data);
                            break;
                        case "logical_volumes":
                            LogicalVolumes = ParseLogicalVolumeSection(data);

                            break;
                        default:
                            throw new ArgumentOutOfRangeException(sectionName, "Unexpected section in volume group metadata");
                    }
                }
                else if (line.EndsWith("}"))
                {
                    break;
                }
            }
        }

        private MetadataLogicalVolumeSection[] ParseLogicalVolumeSection(TextReader data)
        {
            var result = new List<MetadataLogicalVolumeSection>();

            string line;
            while ((line = Metadata.ReadLine(data)) != null)
            {
                if (line == String.Empty) continue;
                if (line.EndsWith("{"))
                {
                    var pv = new MetadataLogicalVolumeSection();
                    pv.Parse(line, data);
                    result.Add(pv);
                }
                else if (line.EndsWith("}"))
                {
                    break;
                }
            }
            return result.ToArray();
        }

        private MetadataPhysicalVolumeSection[] ParsePhysicalVolumeSection(TextReader data)
        {
            var result = new List<MetadataPhysicalVolumeSection>();

            string line;
            while ((line = Metadata.ReadLine(data)) != null)
            {
                if (line == String.Empty) continue;
                if (line.EndsWith("{"))
                {
                    var pv = new MetadataPhysicalVolumeSection();
                    pv.Parse(line, data);
                    result.Add(pv);
                }
                else if (line.EndsWith("}"))
                {
                    break;
                }
            }
            return result.ToArray();
        }
    }

    [Flags]
    internal enum VolumeGroupStatus
    {
        None = 0x0,
        Read = 0x1,
        Write = 0x2,
        Resizeable = 0x4,
    }
}

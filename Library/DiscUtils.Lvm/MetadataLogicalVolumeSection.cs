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
    using System.IO;
    using System.Collections.Generic;
    using DiscUtils.Streams;

    internal class MetadataLogicalVolumeSection
    {
        public string Name;
        public string Id;
        public Guid Identity;
        public LogicalVolumeStatus Status;
        public string[] Flags;
        public string CreationHost;
        public DateTime CreationTime;
        public ulong SegmentCount;
        public MetadataSegmentSection[] Segments;
        private Dictionary<string, PhysicalVolume> _pvs;
        private ulong _extentSize;
        internal void Parse(string head, TextReader data)
        {
            var segments = new List<MetadataSegmentSection>();
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
                            byte[] guid = new byte[16];
                            EndianUtilities.StringToBytes(Id.Replace("-", String.Empty), guid, 0, 16);
                            // Mark it as a version 4 GUID
                            guid[7] = (byte)((guid[7] | (byte)0x40) & (byte)0x4f);
                            guid[8] = (byte)((guid[8] | (byte)0x80) & (byte)0xbf);
                            Identity = new Guid(guid);
                            break;
                        case "status":
                            var values = Metadata.ParseArrayValue(parameter.Value);
                            foreach (var value in values)
                            {
                                switch (value.ToLowerInvariant().Trim())
                                {
                                    case "read":
                                        Status |= LogicalVolumeStatus.Read;
                                        break;
                                    case "write":
                                        Status |= LogicalVolumeStatus.Write;
                                        break;
                                    case "visible":
                                        Status |= LogicalVolumeStatus.Visible;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException("status", "Unexpected status in physical volume metadata");
                                }
                            }
                            break;
                        case "flags":
                            Flags = Metadata.ParseArrayValue(parameter.Value);
                            break;
                        case "creation_host":
                            CreationHost = Metadata.ParseStringValue(parameter.Value);
                            break;
                        case "creation_time":
                            CreationTime = Metadata.ParseDateTimeValue(parameter.Value);
                            break;
                        case "segment_count":
                            SegmentCount = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(parameter.Key, "Unexpected parameter in global metadata");
                    }
                }
                else if (line.EndsWith("{"))
                {
                    var segment = new MetadataSegmentSection();
                    segment.Parse(line, data);
                    segments.Add(segment);
                }
                else if (line.EndsWith("}"))
                {
                    break;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(line, "unexpected input");
                }
            }
            Segments = segments.ToArray();
        }

        internal long ExtentCount
        {
            get
            {
                var length = 0L;
                foreach (var segment in Segments)
                {
                    length += (long) segment.ExtentCount;
                }
                return length;
            }
        }

        public SparseStreamOpenDelegate Open(Dictionary<string, PhysicalVolume> availablePvs, ulong extentSize)
        {
            _pvs = availablePvs;
            _extentSize = extentSize;
            return Open;
        }

        private SparseStream Open()
        {
            if ((Status & LogicalVolumeStatus.Read) == 0)
                throw new IOException("volume is not readable");

            var segments = new List<MetadataSegmentSection>();
            foreach (var segment in Segments)
            {
                if (segment.Type != SegmentType.Striped)
                    throw new IOException("unsupported segment type");
                segments.Add(segment);
            }
            segments.Sort(CompareSegments);

            // Sanity Check...
            ulong pos = 0;
            foreach (var segment in segments)
            {
                if (segment.StartExtent != pos)
                {
                    throw new IOException("Volume extents are non-contiguous");
                }

                pos += segment.ExtentCount;
            }

            List<SparseStream> streams = new List<SparseStream>();
            foreach (var segment in segments)
            {
                streams.Add(OpenSegment(segment));
            }
            return new ConcatStream(Ownership.Dispose, streams.ToArray());
        }

        private SparseStream OpenSegment(MetadataSegmentSection segment)
        {
            if (segment.Stripes.Length != 1)
            {
                throw new IOException("invalid number of stripes");
            }
            var stripe = segment.Stripes[0];
            PhysicalVolume pv;
            if (!_pvs.TryGetValue(stripe.PhysicalVolumeName, out pv))
            {
                throw new IOException("missing pv");
            }
            if (pv.PvHeader.DiskAreas.Length != 1)
            {
                throw new IOException("invalid number od pv data areas");
            }
            var dataArea = pv.PvHeader.DiskAreas[0];
            var start = dataArea.Offset + (stripe.StartExtentNumber*_extentSize*PhysicalVolume.SECTOR_SIZE);
            var length = segment.ExtentCount*_extentSize*PhysicalVolume.SECTOR_SIZE;
            return new SubStream(pv.Content, Ownership.None, (long) start, (long)length);
        }

        private int CompareSegments(MetadataSegmentSection x, MetadataSegmentSection y)
        {
            if (x.StartExtent > y.StartExtent)
            {
                return 1;
            }
            else if (x.StartExtent < y.StartExtent)
            {
                return -1;
            }
            return 0;
        }
    }

    [Flags]
    internal enum LogicalVolumeStatus
    {
        None = 0x0,
        Read = 0x1,
        Write = 0x2,
        Visible = 0x4,
    }
}

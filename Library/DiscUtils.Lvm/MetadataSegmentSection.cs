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

    internal class MetadataSegmentSection
    {
        public string Name;
        public ulong StartExtent;
        public ulong ExtentCount;
        public SegmentType Type;
        public ulong StripeCount;
        public MetadataStripe[] Stripes;

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
                        case "start_extent":
                            StartExtent = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        case "extent_count":
                            ExtentCount = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        case "type":
                            var value = Metadata.ParseStringValue(parameter.Value);
                            switch (value)
                            {
                                case "striped":
                                    Type = SegmentType.Striped;
                                    break;
                                case "zero":
                                    Type = SegmentType.Zero;
                                    break;
                                case "error":
                                    Type = SegmentType.Error;
                                    break;
                                case "free":
                                    Type = SegmentType.Free;
                                    break;
                                case "snapshot":
                                    Type = SegmentType.Snapshot;
                                    break;
                                case "mirror":
                                    Type = SegmentType.Mirror;
                                    break;
                                case "raid1":
                                    Type = SegmentType.Raid1;
                                    break;
                                case "raid10":
                                    Type = SegmentType.Raid10;
                                    break;
                                case "raid4":
                                    Type = SegmentType.Raid4;
                                    break;
                                case "raid5":
                                    Type = SegmentType.Raid5;
                                    break;
                                case "raid5_la":
                                    Type = SegmentType.Raid5La;
                                    break;
                                case "raid5_ra":
                                    Type = SegmentType.Raid5Ra;
                                    break;
                                case "raid5_ls":
                                    Type = SegmentType.Raid5Ls;
                                    break;
                                case "raid5_rs":
                                    Type = SegmentType.Raid5Rs;
                                    break;
                                case "raid6":
                                    Type = SegmentType.Raid6;
                                    break;
                                case "raid6_zr":
                                    Type = SegmentType.Raid6Zr;
                                    break;
                                case "raid6_nr":
                                    Type = SegmentType.Raid6Nr;
                                    break;
                                case "raid6_nc":
                                    Type = SegmentType.Raid6Nc;
                                    break;
                                case "thin-pool":
                                    Type = SegmentType.ThinPool;
                                    break;
                                case "thin":
                                    Type = SegmentType.Thin;
                                    break;
                            }
                            break;
                        case "stripe_count":
                            StripeCount = Metadata.ParseNumericValue(parameter.Value);
                            break;
                        case "stripes":
                            if (parameter.Value.Trim() == "[")
                            {
                                Stripes = ParseStripesSection(data);
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(parameter.Key, "Unexpected parameter in global metadata");
                    }
                }
                else if (line.EndsWith("}"))
                {
                    return;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(line, "unexpected input");
                }
            }
        }

        private MetadataStripe[] ParseStripesSection(TextReader data)
        {
            var result = new List<MetadataStripe>();

            string line;
            while ((line = Metadata.ReadLine(data)) != null)
            {
                if (line == String.Empty) continue;
                if (line.EndsWith("]"))
                {
                    return result.ToArray();
                }
                var pv = new MetadataStripe();
                pv.Parse(line);
                result.Add(pv);
            }
            return result.ToArray();
        }
    }

    [Flags]
    internal enum SegmentType
    {
        //$ lvm segtypes, man(8) lvm
        None,
        Striped,
        Zero,
        Error,
        Free,
        Snapshot,
        Mirror,
        Raid1,
        Raid10,
        Raid4,
        Raid5,
        Raid5La,
        Raid5Ra,
        Raid5Ls,
        Raid5Rs,
        Raid6,
        Raid6Zr,
        Raid6Nr,
        Raid6Nc,
        ThinPool,
        Thin,
    }
}

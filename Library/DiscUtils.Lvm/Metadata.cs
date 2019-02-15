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

    internal class Metadata
    {
        public DateTime CreationTime;
        public string CreationHost;
        public string Description;
        public string Contents;
        public int Version;
        public MetadataVolumeGroupSection[] VolumeGroupSections;
        private static readonly double _maxSeconds = DateTime.MaxValue.Subtract(DateTimeOffsetExtensions.UnixEpoch).TotalSeconds;

        public static Metadata Parse(string metadata)
        {
            using (var reader = new StringReader(metadata))
            {
                var result = new Metadata();
                result.Parse(reader);
                return result;
            }
        }

        private void Parse(TextReader data)
        {
            string line;
            var vgSection = new List<MetadataVolumeGroupSection>();
            while ((line = ReadLine(data))!= null)
            {
                if (line == String.Empty) continue;
                if (line.Contains("="))
                {
                    var parameter = ParseParameter(line);
                    switch (parameter.Key.Trim().ToLowerInvariant())
                    {
                        case "contents":
                            Contents = ParseStringValue(parameter.Value);
                            break;
                        case "version":
                            Version = (int) ParseNumericValue(parameter.Value);
                            break;
                        case "description":
                            Description = ParseStringValue(parameter.Value);
                            break;
                        case "creation_host":
                            CreationHost = ParseStringValue(parameter.Value);
                            break;
                        case "creation_time":
                            CreationTime = ParseDateTimeValue(parameter.Value);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(parameter.Key, "Unexpected parameter in global metadata");
                    }
                }
                else if (line.EndsWith("{"))
                {
                    var vg = new MetadataVolumeGroupSection();
                    vg.Parse(line, data);
                    vgSection.Add(vg);
                }
            }
            VolumeGroupSections = vgSection.ToArray();
        }


        internal static string ReadLine(TextReader data)
        {
            var line = data.ReadLine();
            if (line == null) return null;
            return RemoveComment(line).Trim();
        }

        internal static string[] ParseArrayValue(string value)
        {
            var values = value.Trim('[', ']').Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Metadata.ParseStringValue(values[i]);
            }
            return values;
        }

        internal static string ParseStringValue(string value)
        {
            return value.Trim().Trim('"');
        }

        internal static DateTime ParseDateTimeValue(string value)
        {
            var numeric = ParseNumericValue(value);
            if (numeric > _maxSeconds)
                return DateTime.MaxValue;
            return DateTimeOffsetExtensions.UnixEpoch.AddSeconds(numeric);
        }

        internal static ulong ParseNumericValue(string value)
        {
            return UInt64.Parse(value.Trim());
        }

        internal static KeyValuePair<string, string> ParseParameter(string line)
        {
            var index = line.IndexOf("=", StringComparison.Ordinal);
            if (index < 0)
                throw new ArgumentException("invalid parameter line", line);
            return new KeyValuePair<string, string>(line.Substring(0,index).Trim(), line.Substring(index+1,line.Length-(index+1)).Trim());
        }

        internal static string RemoveComment(string line)
        {
            var index = line.IndexOf("#", StringComparison.Ordinal);
            if (index < 0) return line;
            return line.Substring(0, index);
        }
    }
}

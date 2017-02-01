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
using System.Globalization;

namespace DiscUtils.Vmdk
{
    internal class DescriptorFileEntry
    {
        private readonly DescriptorFileEntryType _type;

        public DescriptorFileEntry(string key, string value, DescriptorFileEntryType type)
        {
            Key = key;
            Value = value;
            _type = type;
        }

        public string Key { get; }

        public string Value { get; set; }

        public static DescriptorFileEntry Parse(string value)
        {
            string[] parts = value.Split(new[] { '=' }, 2);

            for (int i = 0; i < parts.Length; ++i)
            {
                parts[i] = parts[i].Trim();
            }

            if (parts.Length > 1)
            {
                if (parts[1].StartsWith("\"", StringComparison.Ordinal))
                {
                    return new DescriptorFileEntry(parts[0], parts[1].Trim('\"'), DescriptorFileEntryType.Quoted);
                }
                return new DescriptorFileEntry(parts[0], parts[1], DescriptorFileEntryType.Plain);
            }
            return new DescriptorFileEntry(parts[0], string.Empty, DescriptorFileEntryType.NoValue);
        }

        public override string ToString()
        {
            return ToString(true);
        }

        public string ToString(bool spaceOut)
        {
            // VMware workstation appears to be sensitive to spaces, wants them for 'header' values, not for DiskDataBase...
            string sep = spaceOut ? " " : string.Empty;

            switch (_type)
            {
                case DescriptorFileEntryType.NoValue:
                    return Key;
                case DescriptorFileEntryType.Plain:
                    return Key + sep + "=" + sep + Value;
                case DescriptorFileEntryType.Quoted:
                    return Key + sep + "=" + sep + "\"" + Value + "\"";
                default:
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unknown type: {0}",
                        _type));
            }
        }
    }
}
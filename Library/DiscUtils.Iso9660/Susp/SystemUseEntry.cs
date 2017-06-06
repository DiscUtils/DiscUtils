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
using System.IO;
using System.Text;
using DiscUtils.Streams;

namespace DiscUtils.Iso9660
{
    internal abstract class SystemUseEntry
    {
        public string Name;
        public byte Version;

        public static SystemUseEntry Parse(byte[] data, int offset, Encoding encoding, SuspExtension extension,
                                           out byte length)
        {
            if (data[offset] == 0)
            {
                // A zero-byte here is invalid and indicates an incorrectly written SUSP field.
                // Return null to indicate to the caller that SUSP parsing is terminated.
                length = 0;

                return null;
            }

            string name = EndianUtilities.BytesToString(data, offset, 2);
            length = data[offset + 2];
            byte version = data[offset + 3];

            switch (name)
            {
                case "CE":
                    return new ContinuationSystemUseEntry(name, length, version, data, offset);

                case "PD":
                    return new PaddingSystemUseEntry(name, length, version);

                case "SP":
                    return new SharingProtocolSystemUseEntry(name, length, version, data, offset);

                case "ST":
                    // Termination entry. There's no point in storing or validating this one.
                    // Return null to indicate to the caller that SUSP parsing is terminated.
                    return null;

                case "ER":
                    return new ExtensionSystemUseEntry(name, length, version, data, offset, encoding);

                case "ES":
                    return new ExtensionSelectSystemUseEntry(name, length, version, data, offset);

                case "AA":
                case "AB":
                case "AS":
                    // Placeholder support for Apple and Amiga extension records.
                    return new GenericSystemUseEntry(name, length, version, data, offset);

                default:
                    if (extension == null)
                    {
                        return new GenericSystemUseEntry(name, length, version, data, offset);
                    }

                    return extension.Parse(name, length, version, data, offset, encoding);
            }
        }

        protected void CheckAndSetCommonProperties(string name, byte length, byte version, byte minLength, byte maxVersion)
        {
            if (length < minLength)
            {
                throw new InvalidDataException("Invalid SUSP " + Name + " entry - too short, only " + length + " bytes");
            }

            if (version > maxVersion || version == 0)
            {
                throw new NotSupportedException("Unknown SUSP " + Name + " entry version: " + version);
            }

            Name = name;
            Version = version;
        }
    }
}
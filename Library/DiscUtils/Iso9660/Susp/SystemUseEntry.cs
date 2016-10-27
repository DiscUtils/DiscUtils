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

namespace DiscUtils.Iso9660
{
    using System;
    using System.IO;
    using System.Text;

    internal abstract class SystemUseEntry
    {
        public string Name;
        public byte Version;

        public static SystemUseEntry Parse(byte[] data, int offset, Encoding encoding, SuspExtension extension, out int length)
        {
            string name = Utilities.BytesToString(data, offset, 2);
            length = data[offset + 2];

            switch (name)
            {
                case "CE":
                    return new ContinuationSystemUseEntry(data, offset);

                case "PD":
                    return new PaddingSystemUseEntry(data, offset);

                case "SP":
                    return new SharingProtocolSystemUseEntry(data, offset);

                case "ST":
                    return new TerminatorSystemUseEntry(data, offset);

                case "ER":
                    return new ExtensionSystemUseEntry(data, offset, encoding);

                case "ES":
                    return new ExtensionSelectSystemUseEntry(data, offset);

                default:
                    if (extension == null)
                    {
                        return new GenericSystemUseEntry(data, offset);
                    }
                    else
                    {
                        return extension.Parse(name, data, offset, length, encoding);
                    }
            }
        }

        protected void CheckLengthAndVersion(byte len, byte minLength, byte maxVersion)
        {
            if (len < minLength)
            {
                throw new InvalidDataException("Invalid SUSP " + Name + " entry - too short, only " + len + " bytes");
            }

            if (Version > maxVersion || Version == 0)
            {
                throw new NotSupportedException("Unknown SUSP " + Name + " entry version: " + Version);
            }
        }
    }
}

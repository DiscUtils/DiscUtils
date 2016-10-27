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
    using System.IO;
    using System.Text;

    internal sealed class ExtensionSystemUseEntry : SystemUseEntry
    {
        public string ExtensionIdentifier;
        public string ExtensionDescriptor;
        public string ExtensionSource;
        public byte ExtensionVersion;

        public ExtensionSystemUseEntry(byte[] data, int offset, Encoding encoding)
        {
            byte len = data[offset + 2];

            Name = "ER";
            Version = data[offset + 3];

            CheckLengthAndVersion(len, 8, 1);

            int lenId = data[offset + 4];
            int lenDescriptor = data[offset + 5];
            int lenSource = data[offset + 6];

            ExtensionVersion = data[offset + 7];

            if (len < 8 + lenId + lenDescriptor + lenSource)
            {
                throw new InvalidDataException("Invalid SUSP ER entry - too short, only " + len + " bytes - expected: " + (8 + lenId + lenDescriptor + lenSource));
            }

            ExtensionIdentifier = IsoUtilities.ReadChars(data, offset + 8, lenId, encoding);
            ExtensionDescriptor = IsoUtilities.ReadChars(data, offset + 8 + lenId, lenDescriptor, encoding);
            ExtensionSource = IsoUtilities.ReadChars(data, offset + 8 + lenId + lenDescriptor, lenSource, encoding);
        }
    }
}

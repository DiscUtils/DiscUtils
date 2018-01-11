//
// Copyright (c) 2018, DiscUtils
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

namespace DiscUtils.Fat
{
    // More information here: https://www.kernel.org/doc/Documentation/filesystems/vfat.txt
    internal class Slot
    {
        internal Slot(Stream stream)
        {
            byte[] buffer = StreamUtilities.ReadExact(stream, 32);
            Load(buffer, 0);
        }

        internal string Name { get; private set; }

        internal byte AliasChecksum { get; private set; }

        private void Load(byte[] data, int offset)
        {
            byte[] buffer = new byte[12];
            Array.Copy(data, offset + 1, buffer, 0, 10);
            Name = Encoding.Unicode.GetString(buffer, 0, 10);

            var attr = (FatAttributes)data[offset + 11];
            if (attr != (FatAttributes.ReadOnly | FatAttributes.Hidden | FatAttributes.System | FatAttributes.VolumeId))
                throw new Exception($"Invalid value '{attr}' for attribute byte");

            var reserved = data[offset + 12];
            if (reserved != 0)
                throw new Exception($"Reserved byte value '{reserved}' should always be 0");

            AliasChecksum = data[offset + 13];

            Array.Copy(data, offset + 14, buffer, 0, 12);
            Name += Encoding.Unicode.GetString(buffer);

            var startingCluster = EndianUtilities.ToUInt16LittleEndian(data, offset + 26);
            if (startingCluster != 0)
                throw new Exception($"Starting cluster value {startingCluster} should always be 0");

            Array.Copy(data, offset + 28, buffer, 0, 4);
            Name += Encoding.Unicode.GetString(buffer, 0, 4);

            // StringComparison.Ordinal to force IndexOf to not ignore the null character
            // ref: https://github.com/dotnet/coreclr/issues/14066
            var index = Name.IndexOf("\0", StringComparison.Ordinal);
            if (index > -1)
                Name = Name.Substring(0, index);
        }
    }
}

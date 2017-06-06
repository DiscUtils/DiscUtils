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

using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Ntfs
{
    internal sealed class VolumeInformation : IByteArraySerializable, IDiagnosticTraceable
    {
        public const int VersionNt4 = 0x0102;
        public const int VersionW2k = 0x0300;
        public const int VersionXp = 0x0301;

        private byte _majorVersion;
        private byte _minorVersion;

        public VolumeInformation() {}

        public VolumeInformation(byte major, byte minor, VolumeInformationFlags flags)
        {
            _majorVersion = major;
            _minorVersion = minor;
            Flags = flags;
        }

        public VolumeInformationFlags Flags { get; private set; }

        public int Version
        {
            get { return _majorVersion << 8 | _minorVersion; }
        }

        public int Size
        {
            get { return 0x0C; }
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            _majorVersion = buffer[offset + 0x08];
            _minorVersion = buffer[offset + 0x09];
            Flags = (VolumeInformationFlags)EndianUtilities.ToUInt16LittleEndian(buffer, offset + 0x0A);
            return 0x0C;
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            EndianUtilities.WriteBytesLittleEndian((ulong)0, buffer, offset + 0x00);
            buffer[offset + 0x08] = _majorVersion;
            buffer[offset + 0x09] = _minorVersion;
            EndianUtilities.WriteBytesLittleEndian((ushort)Flags, buffer, offset + 0x0A);
        }

        public void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "  Version: " + _majorVersion + "." + _minorVersion);
            writer.WriteLine(indent + "    Flags: " + Flags);
        }
    }
}
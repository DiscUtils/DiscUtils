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
using System.Text;

namespace DiscUtils.Iso9660
{
    internal class BaseVolumeDescriptor
    {
        public const string Iso9660StandardIdentifier = "CD001";

        public readonly string StandardIdentifier;
        public readonly VolumeDescriptorType VolumeDescriptorType;
        public readonly byte VolumeDescriptorVersion;

        public BaseVolumeDescriptor(VolumeDescriptorType type, byte version)
        {
            VolumeDescriptorType = type;
            StandardIdentifier = "CD001";
            VolumeDescriptorVersion = version;
        }

        public BaseVolumeDescriptor(byte[] src, int offset)
        {
            VolumeDescriptorType = (VolumeDescriptorType)src[offset + 0];
            StandardIdentifier = Encoding.ASCII.GetString(src, offset + 1, 5);
            VolumeDescriptorVersion = src[offset + 6];
        }

        internal virtual void WriteTo(byte[] buffer, int offset)
        {
            Array.Clear(buffer, offset, IsoUtilities.SectorSize);
            buffer[offset] = (byte)VolumeDescriptorType;
            IsoUtilities.WriteAChars(buffer, offset + 1, 5, StandardIdentifier);
            buffer[offset + 6] = VolumeDescriptorVersion;
        }
    }
}
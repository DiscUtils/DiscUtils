//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.Collections.Generic;
using System.Text;
using System.IO;
using DiscUtils.Iso9660;
using System.Globalization;

namespace DiscUtils.Udf
{
    internal abstract class BaseTaggedDescriptor : IByteArraySerializable
    {
        public DescriptorTag Tag;

        internal readonly TagIdentifier RequiredTagIdentifier;

        protected BaseTaggedDescriptor(TagIdentifier id)
        {
            RequiredTagIdentifier = id;
        }

        public int ReadFrom(byte[] buffer, int offset)
        {
            if (!DescriptorTag.IsValid(buffer, offset))
            {
                throw new InvalidDataException("Invalid Anchor Volume Descriptor Pointer (invalid tag)");
            }

            Tag = new DescriptorTag();
            Tag.ReadFrom(buffer, offset);

            if (UdfUtilities.ComputeCrc(buffer, offset + Tag.Size, Tag.DescriptorCrcLength) != Tag.DescriptorCrc)
            {
                throw new InvalidDataException("Invalid Anchor Volume Descriptor Pointer (invalid CRC)");
            }

            return Parse(buffer, offset);
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public int Size
        {
            get { return 512; }
        }


        public abstract int Parse(byte[] buffer, int offset);
    }

    internal abstract class BaseTaggedDescriptor<T> : BaseTaggedDescriptor
        where T : BaseTaggedDescriptor, new()
    {
        protected BaseTaggedDescriptor(TagIdentifier id)
            : base(id)
        {
        }

        public static T FromStream(Stream stream, uint sector, uint sectorSize)
        {
            stream.Position = sector * (long)sectorSize;
            byte[] buffer = Utilities.ReadFully(stream, 512);

            T result = new T();
            result.ReadFrom(buffer, 0);
            if (result.Tag.TagIdentifier != result.RequiredTagIdentifier
                || result.Tag.TagLocation != sector)
            {
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, "Corrupt UDF file system, unable to read {0} tag at sector {1}", result.RequiredTagIdentifier, sector));
            }

            return result;
        }

        public override abstract int Parse(byte[] buffer, int offset);
    }
}

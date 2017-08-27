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

using System.Globalization;
using System.IO;
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal abstract class TaggedDescriptor<T> : BaseTaggedDescriptor
        where T : BaseTaggedDescriptor, new()
    {
        protected TaggedDescriptor(TagIdentifier id)
            : base(id) {}

        public static T FromStream(Stream stream, uint sector, uint sectorSize)
        {
            stream.Position = sector * (long)sectorSize;
            byte[] buffer = StreamUtilities.ReadExact(stream, 512);

            T result = new T();
            result.ReadFrom(buffer, 0);
            if (result.Tag.TagIdentifier != result.RequiredTagIdentifier
                || result.Tag.TagLocation != sector)
            {
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture,
                    "Corrupt UDF file system, unable to read {0} tag at sector {1}", result.RequiredTagIdentifier,
                    sector));
            }

            return result;
        }

        public abstract override int Parse(byte[] buffer, int offset);
    }
}
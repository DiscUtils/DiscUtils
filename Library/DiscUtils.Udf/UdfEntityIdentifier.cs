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
using DiscUtils.Streams;

namespace DiscUtils.Udf
{
    internal class UdfEntityIdentifier : EntityIdentifier
    {
        public override string ToString()
        {
            string major = ((uint)Suffix[1]).ToString("X", CultureInfo.InvariantCulture);
            string minor = ((uint)Suffix[0]).ToString("X", CultureInfo.InvariantCulture);
            OSClass osClass = (OSClass)Suffix[2];
            OSIdentifier osId = (OSIdentifier)EndianUtilities.ToUInt16BigEndian(Suffix, 2);
            return string.Format(CultureInfo.InvariantCulture, "{0} [UDF {1}.{2} : OS {3} {4}]", Identifier, major,
                minor, osClass, osId);
        }
    }
}
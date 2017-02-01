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

namespace DiscUtils.Udf
{
    /// <summary>
    /// Contains extended attribute information for a file or directory.
    /// </summary>
    public sealed class ExtendedAttribute
    {
        internal ExtendedAttribute(string id, byte[] data)
        {
            Identifier = id;
            Data = data;
        }

        /// <summary>
        /// Gets the data contained in the attribute.
        /// </summary>
        /// <remarks>The format of the data will depend on the identifier.</remarks>
        public byte[] Data { get; }

        /// <summary>
        /// Gets the value of the identifier of this attribute.
        /// </summary>
        /// <remarks>A typical identifier is "*UDF DVD CGMS Info".</remarks>
        public string Identifier { get; }
    }
}
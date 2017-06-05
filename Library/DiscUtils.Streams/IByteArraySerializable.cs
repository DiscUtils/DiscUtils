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

namespace DiscUtils.Streams
{
    /// <summary>
    /// Common interface for reading structures to/from byte arrays.
    /// </summary>
    public interface IByteArraySerializable
    {
        /// <summary>
        /// Gets the total number of bytes the structure occupies.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Reads the structure from a byte array.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The buffer offset to start reading from.</param>
        /// <returns>The number of bytes read.</returns>
        int ReadFrom(byte[] buffer, int offset);

        /// <summary>
        /// Writes a structure to a byte array.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="offset">The buffer offset to start writing at.</param>
        void WriteTo(byte[] buffer, int offset);
    }
}
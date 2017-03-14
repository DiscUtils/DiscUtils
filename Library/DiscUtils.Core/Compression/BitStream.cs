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

namespace DiscUtils.Compression
{
    /// <summary>
    /// Base class for bit streams.
    /// </summary>
    /// <remarks>
    /// The rules for conversion of a byte stream to a bit stream vary
    /// between implementations.
    /// </remarks>
    internal abstract class BitStream
    {
        /// <summary>
        /// Gets the maximum number of bits that can be peeked on the stream.
        /// </summary>
        public abstract int MaxReadAhead { get; }

        /// <summary>
        /// Reads bits from the stream.
        /// </summary>
        /// <param name="count">The number of bits to read.</param>
        /// <returns>The bits as a UInt32.</returns>
        public abstract uint Read(int count);

        /// <summary>
        /// Queries data from the stream.
        /// </summary>
        /// <param name="count">The number of bits to query.</param>
        /// <returns>The bits as a UInt32.</returns>
        /// <remarks>This method does not consume the bits (i.e. move the file pointer).</remarks>
        public abstract uint Peek(int count);

        /// <summary>
        /// Consumes bits from the stream without returning them.
        /// </summary>
        /// <param name="count">The number of bits to consume.</param>
        public abstract void Consume(int count);
    }
}
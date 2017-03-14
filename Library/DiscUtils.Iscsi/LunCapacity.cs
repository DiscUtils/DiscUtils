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

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Class representing the capacity of a LUN.
    /// </summary>
    public class LunCapacity
    {
        /// <summary>
        /// Initializes a new instance of the LunCapacity class.
        /// </summary>
        /// <param name="logicalBlockCount">The number of logical blocks.</param>
        /// <param name="blockSize">The size of each block.</param>
        public LunCapacity(long logicalBlockCount, int blockSize)
        {
            LogicalBlockCount = logicalBlockCount;
            BlockSize = blockSize;
        }

        /// <summary>
        /// Gets the size of each logical block.
        /// </summary>
        public int BlockSize { get; }

        /// <summary>
        /// Gets the number of logical blocks in the LUN.
        /// </summary>
        public long LogicalBlockCount { get; }

        /// <summary>
        /// Gets the capacity (in bytes) as a string.
        /// </summary>
        /// <returns>A string containing an integer.</returns>
        public override string ToString()
        {
            return (BlockSize * LogicalBlockCount).ToString(CultureInfo.InvariantCulture);
        }
    }
}
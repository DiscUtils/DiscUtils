//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils
{
    internal abstract class SparseStream : Stream
    {
        /// <summary>
        /// Indicates how fine-grained the block / gap structure is.
        /// </summary>
        /// <remarks>If the blocks / gaps have to be aligned, this value indicates the
        /// alignment.  Otherwise, the value is 1.</remarks>
        public abstract int BlockBoundary
        {
            get;
        }

        /// <summary>
        /// Indicates if regions of the stream can be wiped.
        /// </summary>
        public abstract bool CanWipe();

        /// <summary>
        /// Marks a region, starting at the current file position, as sparse.
        /// </summary>
        /// <param name="length">The number of bytes to wipe</param>
        /// <remarks>This method may extend the stream, if the wipe extends beyond
        /// the end of the stream - it simply indicates the end of the stream is
        /// not stored.</remarks>
        public abstract void Wipe(long length);

        /// <summary>
        /// The relative location of the next stored block of data.
        /// </summary>
        /// <remarks>If the current position is in a block, 0 is returned.  If no
        /// further blocks remain, -1 is returned.</remarks>
        public abstract long NextBlock
        {
            get;
        }

        /// <summary>
        /// The location of the next stored block of data, relative to the start of the file.
        /// </summary>
        /// <remarks>If the current position is in a block, the next block is
        /// the current position.  If no further blocks remain, -1 is returned.</remarks>
        public abstract long NextBlockPosition
        {
            get;
        }

        /// <summary>
        /// The length of the next stored block of data.
        /// </summary>
        /// <remarks>If the current position is in a block, the length to the end of the
        /// current block is returned.  Otherwise, the length of the next block is returned.
        /// If no further blocks remain, -1 is returned.</remarks>
        public abstract long NextBlockLength
        {
            get;
        }

    }
}

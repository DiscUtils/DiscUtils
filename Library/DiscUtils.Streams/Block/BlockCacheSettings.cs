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
    /// Settings controlling BlockCache instances.
    /// </summary>
    public sealed class BlockCacheSettings
    {
        /// <summary>
        /// Initializes a new instance of the BlockCacheSettings class.
        /// </summary>
        public BlockCacheSettings()
        {
            BlockSize = (int)(4 * Sizes.OneKiB);
            ReadCacheSize = 4 * Sizes.OneMiB;
            LargeReadSize = 64 * Sizes.OneKiB;
            OptimumReadSize = (int)(64 * Sizes.OneKiB);
        }

        /// <summary>
        /// Initializes a new instance of the BlockCacheSettings class.
        /// </summary>
        /// <param name="settings">The cache settings.</param>
        internal BlockCacheSettings(BlockCacheSettings settings)
        {
            BlockSize = settings.BlockSize;
            ReadCacheSize = settings.ReadCacheSize;
            LargeReadSize = settings.LargeReadSize;
            OptimumReadSize = settings.OptimumReadSize;
        }

        /// <summary>
        /// Gets or sets the size (in bytes) of each cached block.
        /// </summary>
        public int BlockSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum read size that will be cached.
        /// </summary>
        /// <remarks>Large reads are not cached, on the assumption they will not
        /// be repeated.  This setting controls what is considered 'large'.
        /// Any read that is more than this many bytes will not be cached.</remarks>
        public long LargeReadSize { get; set; }

        /// <summary>
        /// Gets or sets the optimum size of a read to the wrapped stream.
        /// </summary>
        /// <remarks>This value must be a multiple of BlockSize.</remarks>
        public int OptimumReadSize { get; set; }

        /// <summary>
        /// Gets or sets the size (in bytes) of the read cache.
        /// </summary>
        public long ReadCacheSize { get; set; }
    }
}
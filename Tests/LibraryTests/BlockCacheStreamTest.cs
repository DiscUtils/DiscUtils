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

using System.IO;
using DiscUtils;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests
{
    internal sealed class BlockCacheStreamTest
    {
        [Fact]
        public void Bug5203_IncreaseSize()
        {
            MemoryStream ms = new MemoryStream();
            BlockCacheSettings settings = new BlockCacheSettings { BlockSize = 64, LargeReadSize = 128, OptimumReadSize = 64, ReadCacheSize = 1024 };
            BlockCacheStream bcs = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            // Pre-load read cache with a 'short' block
            bcs.Write(new byte[11], 0, 11);
            bcs.Position = 0;
            bcs.Read(new byte[11], 0, 11);

            // Extend stream
            for(int i = 0; i < 20; ++i)
            {
                bcs.Write(new byte[11], 0, 11);
            }

            // Try to read from first block beyond length of original cached short length
            // Bug was throwing exception here
            bcs.Position = 60;
            bcs.Read(new byte[20], 0, 20);
        }
    }
}

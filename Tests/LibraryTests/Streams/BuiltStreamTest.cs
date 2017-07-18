//
// Copyright (c) 2017, Bianco Veigel
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

using System.Collections.Generic;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests.Streams
{
    public class BuiltStreamTest
    {
        [Fact]
        public void BuildStreamLengthIsRespected()
        {
            int length = 1024;
            var extent = new BuilderSparseStreamExtent(0, new ZeroStream(2 * length));
            using (var stream = new BuiltStream(length, new List<BuilderExtent> { extent }))
            {
                Assert.Equal(0, stream.Position);
                Assert.Equal(length, stream.Length);
                var content = new byte[2 * length];
                var read = stream.Read(content, 0, content.Length);
                Assert.Equal(length, read);
                Assert.Equal(stream.Length, stream.Position);
            }
        }
    }
}

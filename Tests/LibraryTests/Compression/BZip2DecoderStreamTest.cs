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
using System.Text;
using DiscUtils;
using DiscUtils.Compression;
using DiscUtils.Streams;
using Xunit;

namespace LibraryTests.Compression
{
    public class BZip2DecoderStreamTest
    {
        private readonly byte[] ValidData =
            {
                0x42, 0x5A, 0x68, 0x39, 0x31, 0x41, 0x59, 0x26, 0x53, 0x59, 0xF7, 0x3C,
                0x46, 0x3E, 0x00, 0x00, 0x02, 0x13, 0x80, 0x40, 0x00, 0x04, 0x00, 0x22,
                0xE1, 0x1C, 0x00, 0x20, 0x00, 0x31, 0x00, 0xD3, 0x4D, 0x04, 0x4F, 0x53,
                0x6A, 0x7A, 0x4F, 0x23, 0xA8, 0x51, 0xB6, 0xA1, 0x71, 0x0A, 0x86, 0x01,
                0xA2, 0xEE, 0x48, 0xA7, 0x0A, 0x12, 0x1E, 0xE7, 0x88, 0xC7, 0xC0
            };

        private readonly byte[] InvalidBlockCrcData =
            {
                0x42, 0x5A, 0x68, 0x39, 0x31, 0x41, 0x59, 0x26, 0x53, 0x59, 0xFF, 0x3C,
                0x46, 0x3E, 0x00, 0x00, 0x02, 0x13, 0x80, 0x40, 0x00, 0x04, 0x00, 0x22,
                0xE1, 0x1C, 0x00, 0x20, 0x00, 0x31, 0x00, 0xD3, 0x4D, 0x04, 0x4F, 0x53,
                0x6A, 0x7A, 0x4F, 0x23, 0xA8, 0x51, 0xB6, 0xA1, 0x71, 0x0A, 0x86, 0x01,
                0xA2, 0xEE, 0x48, 0xA7, 0x0A, 0x12, 0x1E, 0xE7, 0x88, 0xC7, 0xC0
            };

        private readonly byte[] InvalidCombinedCrcData =
            {
                0x42, 0x5A, 0x68, 0x39, 0x31, 0x41, 0x59, 0x26, 0x53, 0x59, 0xF7, 0x3C,
                0x46, 0x3E, 0x00, 0x00, 0x02, 0x13, 0x80, 0x40, 0x00, 0x04, 0x00, 0x22,
                0xE1, 0x1C, 0x00, 0x20, 0x00, 0x31, 0x00, 0xD3, 0x4D, 0x04, 0x4F, 0x53,
                0x6A, 0x7A, 0x4F, 0x23, 0xA8, 0x51, 0xB6, 0xA1, 0x71, 0x0A, 0x86, 0x01,
                0xA2, 0xEE, 0x48, 0xA7, 0x0A, 0x12, 0x1E, 0xE7, 0x88, 0xCF, 0xC0
            };

        [Fact]
        public void TestValidStream()
        {
            BZip2DecoderStream decoder = new BZip2DecoderStream(new MemoryStream(ValidData), Ownership.Dispose);

            byte[] buffer = new byte[1024];
            int numRead = decoder.Read(buffer, 0, 1024);
            Assert.Equal(21, numRead);

            string s = Encoding.ASCII.GetString(buffer, 0, numRead);
            Assert.Equal("This is a test string", s);
        }

        [Fact]
        public void TestInvalidBlockCrcStream()
        {
            BZip2DecoderStream decoder = new BZip2DecoderStream(new MemoryStream(InvalidBlockCrcData), Ownership.Dispose);

            byte[] buffer = new byte[1024];
            Assert.Throws<InvalidDataException>(() => decoder.Read(buffer, 0, 1024));
        }

        [Fact]
        public void TestCombinedCrcStream()
        {
            BZip2DecoderStream decoder = new BZip2DecoderStream(new MemoryStream(InvalidCombinedCrcData), Ownership.Dispose);

            byte[] buffer = new byte[1024];
            Assert.Throws<InvalidDataException>(() => decoder.Read(buffer, 0, 1024));
        }

        [Fact]
        public void TestCombinedCrcStream_ExactLengthRead()
        {
            BZip2DecoderStream decoder = new BZip2DecoderStream(new MemoryStream(InvalidCombinedCrcData), Ownership.Dispose);

            byte[] buffer = new byte[21];
            Assert.Throws<InvalidDataException>(() => decoder.Read(buffer, 0, 21));
        }
    }
}

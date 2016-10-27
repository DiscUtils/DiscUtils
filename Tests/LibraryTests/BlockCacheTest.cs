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

using System;
using System.IO;
using NUnit.Framework;

namespace DiscUtils
{
    [TestFixture]
    public class BlockCacheTest
    {
        [Test]
        public void Dispose()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose);
            cacheStream.Dispose();

            try
            {
                cacheStream.Position = 0;
                cacheStream.ReadByte();
                Assert.Fail("Cache stream should have failed - disposed");
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                ms.Position = 0;
                ms.ReadByte();
                Assert.Fail("Cache stream should have failed - disposed");
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [Test]
        public void LargeRead()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[40];
            cacheStream.Position = 0;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 0);
            Assert.AreEqual(1, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);
        }

        [Test]
        public void ReadThrough()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[20];
            cacheStream.Position = 0;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 0);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);
        }

        [Test]
        public void CachedRead()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[20];
            cacheStream.Position = 0;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 0);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);

            buffer = new byte[buffer.Length];
            cacheStream.Position = 0;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 0);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(1, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(2, cacheStream.Statistics.TotalReadsIn);
        }

        [Test]
        public void UnalignedRead()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[20];
            cacheStream.Position = 3;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 3);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);
        }

        [Test]
        public void UnalignedCachedRead()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[20];
            cacheStream.Position = 3;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 3);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);

            buffer = new byte[buffer.Length];
            cacheStream.Position = 3;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 3);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(1, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(2, cacheStream.Statistics.TotalReadsIn);
        }


        [Test]
        public void Overread()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[20];
            cacheStream.Position = 90;
            int numRead = cacheStream.Read(buffer, 0, buffer.Length);

            Assert.AreEqual(10, numRead);
            AssertSequenced(buffer, 0, 10, 90);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);
        }


        [Test]
        public void CachedOverread()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[20];
            cacheStream.Position = 90;
            int numRead = cacheStream.Read(buffer, 0, buffer.Length);

            Assert.AreEqual(10, numRead);
            AssertSequenced(buffer, 0, 10, 90);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);

            buffer = new byte[buffer.Length];
            cacheStream.Position = 90;
            numRead = cacheStream.Read(buffer, 0, buffer.Length);

            Assert.AreEqual(10, numRead);
            AssertSequenced(buffer, 0, 10, 90);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(1, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(2, cacheStream.Statistics.TotalReadsIn);
        }

        [Test]
        public void CacheBlockRecycle()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 50, LargeReadSize = 100 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[50];
            cacheStream.Position = 10;
            int numRead = cacheStream.Read(buffer, 0, buffer.Length);

            Assert.AreEqual(50, numRead);
            AssertSequenced(buffer, 10);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(0, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(1, cacheStream.Statistics.TotalReadsIn);

            buffer = new byte[40];
            cacheStream.Position = 50;
            numRead = cacheStream.Read(buffer, 0, buffer.Length);

            Assert.AreEqual(40, numRead);
            AssertSequenced(buffer, 50);
            Assert.AreEqual(0, cacheStream.Statistics.LargeReadsIn);
            Assert.AreEqual(1, cacheStream.Statistics.ReadCacheHits);
            Assert.AreEqual(2, cacheStream.Statistics.TotalReadsIn);
        }

        [Test]
        public void Write()
        {
            MemoryStream ms = CreateSequencedMemStream(100, true);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[20];
            cacheStream.Position = 10;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 10);

            cacheStream.Position = 20;
            cacheStream.Write(new byte[10], 0, 10);
            Assert.AreEqual(30, cacheStream.Position);

            cacheStream.Position = 10;
            buffer = new byte[30];
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 0, 10, 10);
            AssertSequenced(buffer, 20, 10, 30);
            Assert.AreEqual(0, buffer[10]);
            Assert.AreEqual(0, buffer[19]);
        }

        [Test]
        public void FailWrite()
        {
            MemoryStream ms = CreateSequencedMemStream(100, false);
            BlockCacheSettings settings = new BlockCacheSettings() { BlockSize = 10, OptimumReadSize = 20, ReadCacheSize = 100, LargeReadSize = 30 };
            BlockCacheStream cacheStream = new BlockCacheStream(SparseStream.FromStream(ms, Ownership.Dispose), Ownership.Dispose, settings);

            byte[] buffer = new byte[25];
            cacheStream.Position = 0;
            cacheStream.Read(buffer, 0, buffer.Length);

            AssertSequenced(buffer, 0);

            int freeBefore = cacheStream.Statistics.FreeReadBlocks;

            cacheStream.Position = 11;
            try
            {
                cacheStream.Write(new byte[10], 0, 10);
            }
            catch(NotSupportedException)
            {
                Assert.AreEqual(freeBefore + 2, cacheStream.Statistics.FreeReadBlocks);
            }
        }

        private MemoryStream CreateSequencedMemStream(int length, bool writable)
        {
            byte[] buffer = new byte[length];
            for (int i = 0; i < length; ++i)
            {
                buffer[i] = (byte)i;
            }

            return new MemoryStream(buffer, writable);
        }

        private void AssertSequenced(byte[] buffer, int seqOffset)
        {
            AssertSequenced(buffer, 0, buffer.Length, seqOffset);
        }

        private void AssertSequenced(byte[] buffer, int offset, int count, int seqOffset)
        {
            for (int i = 0; i < count; ++i)
            {
                if (buffer[i + offset] != (byte)(i + seqOffset))
                {
                    Assert.Fail("Expected {0} at index {1}, was {2}", (byte)(i + seqOffset), i + offset, buffer[i + offset]);
                }
            }
        }
    }
}

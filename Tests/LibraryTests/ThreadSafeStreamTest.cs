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

using NUnit.Framework;


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DiscUtils
{
    [TestFixture]
    public class ThreadSafeStreamTest
    {
        [Test]
        public void OpenView()
        {
            ThreadSafeStream tss = new ThreadSafeStream(SparseStream.FromStream(Stream.Null, Ownership.None));
            SparseStream view = tss.OpenView();
        }

        [Test]
        public void ViewIO()
        {
            SparseMemoryStream memStream = new SparseMemoryStream();
            memStream.SetLength(1024);

            ThreadSafeStream tss = new ThreadSafeStream(memStream);

            SparseStream altView = tss.OpenView();

            // Check positions are independant
            tss.Position = 100;
            Assert.AreEqual(0, altView.Position);
            Assert.AreEqual(100, tss.Position);

            // Check I/O is synchronous
            byte[] buffer = new byte[200];
            tss.WriteByte(99);
            altView.Read(buffer, 0, 200);
            Assert.AreEqual(99, buffer[100]);

            // Check positions are updated correctly
            Assert.AreEqual(200, altView.Position);
            Assert.AreEqual(101, tss.Position);
        }

        [Test]
        public void ChangeLengthFails()
        {
            SparseMemoryStream memStream = new SparseMemoryStream();
            memStream.SetLength(2);

            ThreadSafeStream tss = new ThreadSafeStream(memStream);
            Assert.AreEqual(2, tss.Length);

            try
            {
                tss.SetLength(10);
                Assert.Fail("SetLength should fail");
            }
            catch (NotSupportedException)
            {
            }
        }

        [Test]
        public void Extents()
        {
            SparseMemoryStream memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.ReadWrite);
            memStream.SetLength(1024);

            ThreadSafeStream tss = new ThreadSafeStream(memStream);

            SparseStream altView = tss.OpenView();

            tss.Position = 100;
            tss.WriteByte(99);

            List<StreamExtent> extents = new List<StreamExtent>(altView.Extents);
            Assert.AreEqual(1, extents.Count);
            Assert.AreEqual(100, extents[0].Start);
            Assert.AreEqual(1, extents[0].Length);

            extents = new List<StreamExtent>(altView.GetExtentsInRange(10, 300));
            Assert.AreEqual(1, extents.Count);
            Assert.AreEqual(100, extents[0].Start);
            Assert.AreEqual(1, extents[0].Length);
        }

        [Test]
        public void DisposeView()
        {
            SparseMemoryStream memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.ReadWrite);
            memStream.SetLength(1024);

            ThreadSafeStream tss = new ThreadSafeStream(memStream);

            SparseStream altView = tss.OpenView();
            altView.Dispose();

            tss.ReadByte();

            SparseStream altView2 = tss.OpenView();
            altView2.ReadByte();
        }

        [Test]
        public void Dispose_StopsView()
        {
            SparseMemoryStream memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.ReadWrite);
            memStream.SetLength(1024);

            ThreadSafeStream tss = new ThreadSafeStream(memStream);

            SparseStream altView = tss.OpenView();
            tss.Dispose();

            try
            {
                altView.ReadByte();
                Assert.Fail("Disposed stream didn't stop view");
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [Test]
        public void Seek()
        {
            SparseMemoryStream memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.ReadWrite);
            memStream.SetLength(1024);

            ThreadSafeStream tss = new ThreadSafeStream(memStream);

            tss.Seek(10, SeekOrigin.Begin);
            Assert.AreEqual(10, tss.Position);

            tss.Seek(10, SeekOrigin.Current);
            Assert.AreEqual(20, tss.Position);

            tss.Seek(-10, SeekOrigin.End);
            Assert.AreEqual(1014, tss.Position);
        }

        [Test]
        public void CanWrite()
        {
            SparseMemoryStream memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.ReadWrite);
            ThreadSafeStream tss = new ThreadSafeStream(memStream);
            Assert.AreEqual(true, tss.CanWrite);

            memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.Read);
            tss = new ThreadSafeStream(memStream);
            Assert.AreEqual(false, tss.CanWrite);
        }

        [Test]
        public void CanRead()
        {
            SparseMemoryStream memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.ReadWrite);
            ThreadSafeStream tss = new ThreadSafeStream(memStream);
            Assert.AreEqual(true, tss.CanRead);

            memStream = new SparseMemoryStream(new SparseMemoryBuffer(1), FileAccess.Write);
            tss = new ThreadSafeStream(memStream);
            Assert.AreEqual(false, tss.CanRead);
        }
    }
}

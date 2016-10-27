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


namespace DiscUtils
{
    [TestFixture]
    public class ChsAddressTest
    {
        [Test]
        public void Create()
        {
            ChsAddress g = new ChsAddress(100, 16, 63);
            Assert.AreEqual(100, g.Cylinder);
            Assert.AreEqual(16, g.Head);
            Assert.AreEqual(63, g.Sector);
        }

        [Test]
        public void Equals()
        {
            Assert.AreEqual(new ChsAddress(333, 22, 11), new ChsAddress(333, 22, 11));
        }

        [Test]
        public void TestToString()
        {
            Assert.AreEqual("(333/22/11)", new ChsAddress(333, 22, 11).ToString());
        }
    }
}

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

using DiscUtils;
using Xunit;

namespace LibraryTests
{
    public class GeometryTest
    {
        [Fact]
        public void Create()
        {
            Geometry g = new Geometry(100, 16, 63);
            Assert.Equal(100, g.Cylinders);
            Assert.Equal(16, g.HeadsPerCylinder);
            Assert.Equal(63, g.SectorsPerTrack);
        }

        [Fact]
        public void LBARoundTrip()
        {
            Geometry g = new Geometry(100, 16, 63);

            const int TestCylinder = 54;
            const int TestHead = 15;
            const int TestSector = 63;

            long lba = g.ToLogicalBlockAddress(TestCylinder, TestHead, TestSector);
            ChsAddress chs = g.ToChsAddress(lba);

            Assert.Equal(TestCylinder, chs.Cylinder);
            Assert.Equal(TestHead, chs.Head);
            Assert.Equal(TestSector, chs.Sector);
        }

        [Fact]
        public void TotalSectors()
        {
            Geometry g = new Geometry(333, 22, 11);
            Assert.Equal(333 * 22 * 11, g.TotalSectorsLong);
        }

        [Fact]
        public void Capacity()
        {
            Geometry g = new Geometry(333, 22, 11);
            Assert.Equal(333 * 22 * 11 * 512, g.Capacity);
        }

        [Fact]
        public void FromCapacity()
        {
            // Check the capacity calculated is no greater than requested, and off by no more than 10%
            const long ThreeTwentyMB = 1024 * 1024 * 320;
            Geometry g = Geometry.FromCapacity(ThreeTwentyMB);
            Assert.True(g.Capacity <= ThreeTwentyMB && g.Capacity > ThreeTwentyMB * 0.9);

            // Check exact sizes are maintained - do one pass to allow for finding a geometry that matches
            // the algorithm - then expect identical results each time.
            Geometry startGeometry = new Geometry(333,22,11);
            Geometry trip1 = Geometry.FromCapacity(startGeometry.Capacity);
            Assert.Equal(trip1, Geometry.FromCapacity(trip1.Capacity));
        }

        [Fact]
        public void GeometryEquals()
        {
            Assert.Equal(Geometry.FromCapacity(1024 * 1024 * 32), Geometry.FromCapacity(1024 * 1024 * 32));
        }

        [Fact]
        public void TestToString()
        {
            Assert.Equal("(333/22/11)", new Geometry(333, 22, 11).ToString());
        }
    }
}
